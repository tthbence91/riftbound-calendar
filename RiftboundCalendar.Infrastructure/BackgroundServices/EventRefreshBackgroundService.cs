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
    private IReadOnlyDictionary<string, RegistrationStatus>? _previousStates;

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
        _previousStates = await LoadStateAsync(stoppingToken);

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

    private async Task<IReadOnlyDictionary<string, RegistrationStatus>> LoadStateAsync(CancellationToken ct)
    {
        try
        {
            return await _observers.StateRepository.LoadAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load notification state from DB — starting fresh");
            return new Dictionary<string, RegistrationStatus>();
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
                if (_previousStates!.Count > 0)
                {
                    await NotifyNewEventsAsync(filtered, _previousStates, stoppingToken);
                    await NotifyStatusChangesAsync(filtered, _previousStates, stoppingToken);
                }

                var newStates = filtered.ToDictionary(e => e.Id, e => e.Stats.GetRegistrationStatus());
                await SaveStateAsync(newStates, stoppingToken);
                _previousStates = newStates;

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

    private async Task SaveStateAsync(Dictionary<string, RegistrationStatus> states, CancellationToken ct)
    {
        try
        {
            await _observers.StateRepository.SaveAsync(states, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save notification state to DB");
        }
    }

    private async Task NotifyNewEventsAsync(
        IReadOnlyList<RiftboundEvent> current,
        IReadOnlyDictionary<string, RegistrationStatus> previousStates,
        CancellationToken ct)
    {
        var newEvents = current.Where(e => !previousStates.ContainsKey(e.Id)).ToList();
        if (newEvents.Count == 0) return;

        _logger.LogInformation("Notifying {Count} new event(s) via Discord", newEvents.Count);
        await _observers.Notifier.NotifyNewEventsAsync(newEvents, ct);
    }

    private async Task NotifyStatusChangesAsync(
        IReadOnlyList<RiftboundEvent> current,
        IReadOnlyDictionary<string, RegistrationStatus> previousStates,
        CancellationToken ct)
    {
        var changes = current
            .Where(e => previousStates.TryGetValue(e.Id, out var prev)
                        && prev != e.Stats.GetRegistrationStatus()
                        && e.Stats.GetRegistrationStatus() == RegistrationStatus.Open)
            .Select(e => new StatusChange(e, previousStates[e.Id], e.Stats.GetRegistrationStatus()))
            .ToList();

        if (changes.Count == 0) return;

        _logger.LogInformation("Notifying {Count} status change(s) via Discord", changes.Count);
        await _observers.Notifier.NotifyStatusChangedAsync(changes, ct);
    }
}
