using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RiftboundCalendar.Core.Entities;
using RiftboundCalendar.Core.Interfaces;
using RiftboundCalendar.Infrastructure.BackgroundServices;
using RiftboundCalendar.Infrastructure.Caching;
using RiftboundCalendar.Infrastructure.Configuration;
using IReadOnlyDict = System.Collections.Generic.IReadOnlyDictionary<string, RiftboundCalendar.Core.Entities.RegistrationStatus>;

namespace RiftboundCalendar.Infrastructure.Tests.BackgroundServices;

public class EventRefreshBackgroundServiceTests : IDisposable
{
    private readonly Mock<IEventFetcher> _mockFetcher = new();
    private readonly Mock<IEventNotifier> _mockNotifier = new();
    private readonly Mock<INotificationStateRepository> _mockStateRepo = new();
    private readonly IMemoryCache _memoryCache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
    private readonly EventCacheRepository _cacheRepo;

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
        _mockStateRepo
            .Setup(r => r.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, RegistrationStatus>());
        _mockStateRepo
            .Setup(r => r.SaveAsync(It.IsAny<IReadOnlyDict>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
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
        await Task.Delay(50);
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
        await Task.Delay(50);
        await cts.CancelAsync();

        Func<Task> stop = () => sut.StopAsync(CancellationToken.None);
        await stop.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStatusChangesToOpen_NotifiesStatusChange()
    {
        var (sut, secondCycleTcs) = CreateTwoCycleSut(
            firstStatus: "REGISTRATION_CLOSED",
            secondStatus: "REGISTRATION_OPEN");

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await secondCycleTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        _mockNotifier.Verify(n => n.NotifyStatusChangedAsync(
            It.Is<IReadOnlyList<StatusChange>>(c => c.Count > 0),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStatusUnchanged_DoesNotNotifyStatusChange()
    {
        var (sut, secondCycleTcs) = CreateTwoCycleSut(
            firstStatus: "REGISTRATION_OPEN",
            secondStatus: "REGISTRATION_OPEN",
            capacity: 32, registeredCount: 10);

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await secondCycleTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        _mockNotifier.Verify(n => n.NotifyStatusChangedAsync(
            It.IsAny<IReadOnlyList<StatusChange>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStatusChangesToFull_DoesNotNotifyStatusChange()
    {
        var nearBudapest = new EventLocation("Test", 47.4600, 18.9283);
        var openEvent = MakeEvent("evt1", nearBudapest,
            new EventStats { LifecycleStatus = "REGISTRATION_OPEN", Capacity = 32, RegisteredCount = 10 });
        var fullEvent = MakeEvent("evt1", nearBudapest,
            new EventStats { LifecycleStatus = "REGISTRATION_OPEN", Capacity = 32, RegisteredCount = 32 });

        var (sut, secondCycleTcs) = CreateTwoCycleSutFromEvents(openEvent, fullEvent);

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await secondCycleTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        _mockNotifier.Verify(n => n.NotifyStatusChangedAsync(
            It.IsAny<IReadOnlyList<StatusChange>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStatusChangesToClosed_DoesNotNotifyStatusChange()
    {
        var (sut, secondCycleTcs) = CreateTwoCycleSut(
            firstStatus: "REGISTRATION_OPEN",
            secondStatus: "REGISTRATION_CLOSED");

        using var cts = new CancellationTokenSource();
        await sut.StartAsync(cts.Token);
        await secondCycleTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);
        await cts.CancelAsync();
        await sut.StopAsync(CancellationToken.None);

        _mockNotifier.Verify(n => n.NotifyStatusChangedAsync(
            It.IsAny<IReadOnlyList<StatusChange>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private EventRefreshBackgroundService CreateSut() =>
        new(_mockFetcher.Object, _cacheRepo,
            new EventRefreshObservers(new StartupReadiness(), _mockNotifier.Object, _mockStateRepo.Object),
            _options, NullLogger<EventRefreshBackgroundService>.Instance);

    private (EventRefreshBackgroundService sut, TaskCompletionSource secondCycleTcs) CreateTwoCycleSut(
        string firstStatus, string secondStatus,
        int? capacity = null, int? registeredCount = null)
    {
        var nearBudapest = new EventLocation("Test", 47.4600, 18.9283);
        var first  = MakeEvent("evt1", nearBudapest, new EventStats { LifecycleStatus = firstStatus,  Capacity = capacity, RegisteredCount = registeredCount });
        var second = MakeEvent("evt1", nearBudapest, new EventStats { LifecycleStatus = secondStatus, Capacity = capacity, RegisteredCount = registeredCount });
        return CreateTwoCycleSutFromEvents(first, second);
    }

    private (EventRefreshBackgroundService sut, TaskCompletionSource secondCycleTcs) CreateTwoCycleSutFromEvents(
        RiftboundEvent first, RiftboundEvent second)
    {
        var callCount = 0;
        var tcs = new TaskCompletionSource();
        _mockFetcher
            .Setup(f => f.FetchAllEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => { callCount++; return callCount == 1 ? [first] : [second]; })
            .Callback(() => { if (callCount >= 2) tcs.TrySetResult(); });

        var sut = new EventRefreshBackgroundService(
            _mockFetcher.Object, _cacheRepo,
            new EventRefreshObservers(new StartupReadiness(), _mockNotifier.Object, _mockStateRepo.Object),
            Options.Create(new RiftboundOptions
            {
                RefreshIntervalMinutes = 0,
                BudapestLatitude = 47.4979, BudapestLongitude = 19.0402, RadiusKm = 50.0
            }),
            NullLogger<EventRefreshBackgroundService>.Instance);

        return (sut, tcs);
    }

    private static RiftboundEvent MakeEvent(string id, EventLocation location, EventStats stats) =>
        new RiftboundEvent(id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(4),
            location, new EventInfo("Test", "Constructed", new Uri("https://example.com")))
            { Stats = stats };

    private static RiftboundEvent CreateEvent(string id, double lat, double lng) =>
        new(id, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2),
            new EventLocation("Test", lat, lng),
            new EventInfo("Test Event", "Constructed", new Uri("https://example.com")));

    public void Dispose() => _memoryCache.Dispose();
}
