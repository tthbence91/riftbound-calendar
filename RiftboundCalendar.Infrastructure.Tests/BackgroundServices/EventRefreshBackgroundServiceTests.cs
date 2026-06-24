using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RiftboundCalendar.Core.Entities;
using RiftboundCalendar.Core.Interfaces;
using Microsoft.Extensions.Http;
using RiftboundCalendar.Infrastructure.BackgroundServices;
using RiftboundCalendar.Infrastructure.Caching;
using RiftboundCalendar.Infrastructure.Configuration;
using RiftboundCalendar.Infrastructure.Notifications;

namespace RiftboundCalendar.Infrastructure.Tests.BackgroundServices;

public class EventRefreshBackgroundServiceTests : IDisposable
{
    private readonly Mock<IEventFetcher> _mockFetcher = new();
    private readonly IMemoryCache _memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
    private readonly EventCacheRepository _cacheRepo;

    // Long delay so service only runs once before the test cancels it
    private readonly IOptions<RiftboundOptions> _options = Options.Create(new RiftboundOptions
    {
        RefreshIntervalMinutes = 60,
        BudapestLatitude = 47.4979,
        BudapestLongitude = 19.0402,
        RadiusKm = 50.0
    });

    public EventRefreshBackgroundServiceTests()
    {
        _cacheRepo = new EventCacheRepository(_memoryCache);
    }

    [Fact]
    public async Task ExecuteAsync_CallsFetcherOnStart()
    {
        var fetched = new TaskCompletionSource();
        _mockFetcher
            .Setup(f => f.FetchAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([])
            .Callback(fetched.SetResult);

        using var cts = new CancellationTokenSource();
        using var sut = CreateSut();

        await sut.StartAsync(cts.Token);
        await fetched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        _mockFetcher.Verify(f => f.FetchAllEventsAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_StoresFetchedAndFilteredEvents_InCache()
    {
        var nearEvent = CreateEvent("near", lat: 47.4600, lng: 18.9283);  // Budaörs ~11km
        var farEvent  = CreateEvent("far",  lat: 47.6875, lng: 17.6504);  // Győr ~116km
        var fetched = new TaskCompletionSource();
        _mockFetcher
            .Setup(f => f.FetchAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([nearEvent, farEvent])
            .Callback(fetched.SetResult);

        using var cts = new CancellationTokenSource();
        using var sut = CreateSut();

        await sut.StartAsync(cts.Token);
        await fetched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50); // allow UpdateCache to complete
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        var cached = await _cacheRepo.GetEventsAsync();
        cached.Should().ContainSingle().Which.Id.Should().Be("near");
    }

    [Fact]
    public async Task ExecuteAsync_OnFetcherException_DoesNotThrow()
    {
        var fetched = new TaskCompletionSource();
        _mockFetcher
            .Setup(f => f.FetchAllEventsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Scraper failed"))
            .Callback(fetched.SetResult);

        using var cts = new CancellationTokenSource();
        using var sut = CreateSut();

        await sut.StartAsync(cts.Token);
        await fetched.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50); // allow exception handler to run
        await cts.CancelAsync();

        Func<Task> stop = () => sut.StopAsync(CancellationToken.None);
        await stop.Should().NotThrowAsync();
    }

    private EventRefreshBackgroundService CreateSut() =>
        new(_mockFetcher.Object, _cacheRepo, CreateNullObservers(), _options,
            NullLogger<EventRefreshBackgroundService>.Instance);

    private static EventRefreshObservers CreateNullObservers()
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());
        var notifier = new DiscordNotifier(
            factory.Object,
            Options.Create(new DiscordOptions()),
            NullLogger<DiscordNotifier>.Instance);
        return new EventRefreshObservers(new StartupReadiness(), notifier);
    }

    private static RiftboundEvent CreateEvent(string id, double lat, double lng) =>
        new(id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2),
            new EventLocation("Test", lat, lng),
            new EventInfo("Test Event", "Constructed", new Uri("https://example.com")));

    public void Dispose() => _memoryCache.Dispose();
}
