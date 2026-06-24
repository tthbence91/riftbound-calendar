using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RiftboundCalendar.Core.Interfaces;
using RiftboundCalendar.Infrastructure.Caching;
using RiftboundCalendar.Infrastructure.Configuration;
using RiftboundCalendar.Infrastructure.Filtering;

namespace RiftboundCalendar.Infrastructure.BackgroundServices;

public sealed class EventRefreshBackgroundService : BackgroundService
{
    private readonly IEventFetcher _fetcher;
    private readonly EventCacheRepository _cache;
    private readonly StartupReadiness _readiness;
    private readonly RiftboundOptions _options;
    private readonly ILogger<EventRefreshBackgroundService> _logger;

    public EventRefreshBackgroundService(
        IEventFetcher fetcher,
        EventCacheRepository cache,
        StartupReadiness readiness,
        IOptions<RiftboundOptions> options,
        ILogger<EventRefreshBackgroundService> logger)
    {
        _fetcher = fetcher;
        _cache = cache;
        _readiness = readiness;
        _options = options.Value;
        _logger = logger;
    }

    private static readonly TimeSpan StartupRetryDelay = TimeSpan.FromSeconds(45);
    private const int MaxStartupRetries = 4;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var startupRetries = 0;
        var isFirstAttempt = true;

        while (!stoppingToken.IsCancellationRequested)
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
                    _cache.UpdateCache(filtered);
                    startupRetries = 0;
                }
                else if (!_cache.HasEvents && startupRetries < MaxStartupRetries)
                {
                    startupRetries++;
                    _logger.LogWarning(
                        "No events found, retrying in {Delay}s (attempt {Retry}/{Max})",
                        StartupRetryDelay.TotalSeconds, startupRetries, MaxStartupRetries);
                    await Task.Delay(StartupRetryDelay, stoppingToken)
                        .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                    continue;
                }
                else
                {
                    _logger.LogWarning("No events found — keeping cached data");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during event refresh");
            }
            finally
            {
                if (isFirstAttempt)
                {
                    isFirstAttempt = false;
                    _readiness.Signal();
                }
            }

            await Task.Delay(
                    TimeSpan.FromMinutes(_options.RefreshIntervalMinutes),
                    stoppingToken)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}
