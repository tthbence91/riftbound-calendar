using Microsoft.Extensions.Caching.Memory;
using RiftboundCalendar.Core.Entities;
using RiftboundCalendar.Core.Interfaces;

namespace RiftboundCalendar.Infrastructure.Caching;

public sealed class EventCacheRepository : IEventRepository
{
    private const string CacheKey = "riftbound_events";
    private readonly IMemoryCache _cache;

    public EventCacheRepository(IMemoryCache cache)
    {
        _cache = cache;
    }

    public bool HasEvents => _cache.TryGetValue(CacheKey, out IReadOnlyList<RiftboundEvent>? events)
                            && events is { Count: > 0 };

    public Task<IReadOnlyList<RiftboundEvent>> GetEventsAsync(CancellationToken cancellationToken = default)
    {
        var events = _cache.Get<IReadOnlyList<RiftboundEvent>>(CacheKey)
                     ?? Array.Empty<RiftboundEvent>();
        return Task.FromResult(events);
    }

    public void UpdateCache(IReadOnlyList<RiftboundEvent> events)
    {
        _cache.Set(CacheKey, events);
    }
}
