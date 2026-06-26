using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RiftboundCalendar.Core.Entities;
using RiftboundCalendar.Core.Interfaces;
using RiftboundCalendar.Infrastructure.Caching;
using RiftboundCalendar.Infrastructure.Configuration;
using RiftboundCalendar.Infrastructure.Filtering;
using RiftboundCalendar.Infrastructure.Notifications;

namespace RiftboundCalendar.Infrastructure.BackgroundServices;

public sealed class EventRefreshBackgroundService : BackgroundService
{
    private readonly IEventFetcher _fetcher;
    private readonly EventCacheRepository _cache;
    private readonly EventRefreshObservers _observers;
    private readonly RiftboundOptions _options;
    private readonly ILogger<EventRefreshBackgroundService> _logger;

    private static readonly TimeSpan StartupRetryDelay = TimeSpan.FromSeconds(45);
    private const int MaxStartupRetries = 4;

    private bool _isFirstAttempt = true;
    private int _startupRetries;
    private HashSet<string>? _seenEventIds;
    private Dictionary<string, RegistrationStatus>? _seenStatuses;

    public EventRefreshBackgroundService(
        IEventFetcher fetcher,
        EventCacheRepository cache,
        EventRefreshObservers observers,
        IOptions<RiftboundOptions> options,
        ILogger<EventRefreshBackgroundService> logger)
    {
        _fetcher = fetcher;
        _cache = cache;
        _observers = observers;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var retry = await RunOneCycleAsync(stoppingToken);
            if (!retry)
                await Task.Delay(
                        TimeSpan.FromMinutes(_options.RefreshIntervalMinutes),
                        stoppingToken)
                    .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task<bool> RunOneCycleAsync(CancellationToken stoppingToken)
    {
        try
        {
            var allEvents = await _fetcher.FetchAllEventsAsync(stoppingToken);
            var filtered = HaversineFilter.Filter(
                allEvents,
                _options.BudapestLatitude,
                _options.BudapestLongitude,
                _options.RadiusKm);

            if (filtered.Count > 0)
            {
                if (_seenEventIds is not null)
                {
                    await NotifyNewEventsAsync(filtered, _seenEventIds, stoppingToken);
                    await NotifyStatusChangesAsync(filtered, _seenStatuses!, stoppingToken);
                }

                _seenEventIds = filtered.Select(e => e.Id).ToHashSet();
                _seenStatuses = filtered.ToDictionary(e => e.Id, e => e.Stats.GetRegistrationStatus());
                _cache.UpdateCache(filtered);
                _startupRetries = 0;
                return false;
            }

            if (!_cache.HasEvents && _startupRetries < MaxStartupRetries)
            {
                _startupRetries++;
                _logger.LogWarning(
                    "No events found, retrying in {Delay}s (attempt {Retry}/{Max})",
                    StartupRetryDelay.TotalSeconds, _startupRetries, MaxStartupRetries);
                await Task.Delay(StartupRetryDelay, stoppingToken)
                    .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                return true;
            }

            _logger.LogWarning("No events found — keeping cached data");
            return false;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during event refresh");
            return false;
        }
        finally
        {
            if (_isFirstAttempt)
            {
                _isFirstAttempt = false;
                _observers.Readiness.Signal();
            }
        }
    }

    private async Task NotifyNewEventsAsync(
        IReadOnlyList<RiftboundEvent> current,
        HashSet<string> seenIds,
        CancellationToken ct)
    {
        var newEvents = current.Where(e => !seenIds.Contains(e.Id)).ToList();
        if (newEvents.Count == 0) return;

        _logger.LogInformation("Notifying {Count} new event(s) via Discord", newEvents.Count);
        await _observers.Notifier.NotifyNewEventsAsync(newEvents, ct);
    }

    private async Task NotifyStatusChangesAsync(
        IReadOnlyList<RiftboundEvent> current,
        Dictionary<string, RegistrationStatus> previousStatuses,
        CancellationToken ct)
    {
        var changes = current
            .Where(e => previousStatuses.TryGetValue(e.Id, out var prev)
                        && prev != e.Stats.GetRegistrationStatus()
                        && e.Stats.GetRegistrationStatus() == RegistrationStatus.Open)
            .Select(e => new StatusChange(e, previousStatuses[e.Id], e.Stats.GetRegistrationStatus()))
            .ToList();

        if (changes.Count == 0) return;

        _logger.LogInformation("Notifying {Count} status change(s) via Discord", changes.Count);
        await _observers.Notifier.NotifyStatusChangedAsync(changes, ct);
    }
}
