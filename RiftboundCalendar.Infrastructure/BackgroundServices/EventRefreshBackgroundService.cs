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
    private readonly RiftboundOptions _options;
    private readonly ILogger<EventRefreshBackgroundService> _logger;

    public EventRefreshBackgroundService(
        IEventFetcher fetcher,
        EventCacheRepository cache,
        IOptions<RiftboundOptions> options,
        ILogger<EventRefreshBackgroundService> logger)
    {
        _fetcher = fetcher;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                _cache.UpdateCache(filtered);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during event refresh");
            }

            await Task.Delay(
                    TimeSpan.FromMinutes(_options.RefreshIntervalMinutes),
                    stoppingToken)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }
}
