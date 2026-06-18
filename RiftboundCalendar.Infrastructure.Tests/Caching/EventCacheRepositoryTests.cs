using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RiftboundCalendar.Core.Entities;
using RiftboundCalendar.Infrastructure.Caching;

namespace RiftboundCalendar.Infrastructure.Tests.Caching;

public class EventCacheRepositoryTests : IDisposable
{
    private readonly IMemoryCache _cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
    private readonly EventCacheRepository _sut;

    public EventCacheRepositoryTests()
    {
        _sut = new EventCacheRepository(_cache);
    }

    [Fact]
    public async Task GetEventsAsync_BeforeUpdate_ReturnsEmptyList()
    {
        var result = await _sut.GetEventsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEventsAsync_AfterUpdate_ReturnsStoredEvents()
    {
        var events = new[] { CreateEvent("ev-1"), CreateEvent("ev-2") };
        _sut.UpdateCache(events);

        var result = await _sut.GetEventsAsync();

        result.Should().BeEquivalentTo(events);
    }

    [Fact]
    public async Task UpdateCache_ReplacesExistingEvents()
    {
        _sut.UpdateCache(new[] { CreateEvent("old-1") });
        var newEvents = new[] { CreateEvent("new-1"), CreateEvent("new-2") };

        _sut.UpdateCache(newEvents);
        var result = await _sut.GetEventsAsync();

        result.Should().BeEquivalentTo(newEvents);
    }

    public void Dispose() => _cache.Dispose();

    private static RiftboundEvent CreateEvent(string id) =>
        new(id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2),
            new EventLocation("Budapest", 47.4979, 19.0402),
            new EventInfo("Test Event", "Constructed", new Uri("https://example.com")));
}
