using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RiftboundCalendar.Api.Dtos;
using RiftboundCalendar.Core.Entities;
using RiftboundCalendar.Core.Interfaces;

namespace RiftboundCalendar.Api.Tests.Integration;

/// <summary>
/// Full pipeline tests: stub fetcher → real BackgroundService → real HaversineFilter
/// → real EventCacheRepository → real EventsController → HTTP response.
/// Verifies the end-to-end wiring that unit tests cannot cover.
/// </summary>
public class EventPipelineIntegrationTests
{
    private static readonly RiftboundEvent BudapestEvent = new(
        id: "bud-1",
        startDate: new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero),
        endDate: new DateTimeOffset(2026, 7, 1, 18, 0, 0, TimeSpan.Zero),
        location: new EventLocation("Budapest Store", 47.4979, 19.0402),
        info: new EventInfo("Budapest Tournament", "Constructed", new Uri("https://example.com/bud")));

    private static readonly RiftboundEvent GyorEvent = new(
        id: "gyor-1",
        startDate: new DateTimeOffset(2026, 7, 2, 10, 0, 0, TimeSpan.Zero),
        endDate: new DateTimeOffset(2026, 7, 2, 18, 0, 0, TimeSpan.Zero),
        location: new EventLocation("Győr Store", 47.6875, 17.6504),
        info: new EventInfo("Győr Tournament", "Draft", new Uri("https://example.com/gyor")));

    [Fact]
    public async Task FullPipeline_ServesOnlyLocalEvents_AfterHaversineFilter()
    {
        var fetched = new TaskCompletionSource();
        await using var factory = CreateFactory([BudapestEvent, GyorEvent], fetched);
        var client = factory.CreateClient();

        await fetched.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(200);

        var response = await client.GetAsync("/api/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<List<RiftboundEventDto>>();
        dtos.Should().ContainSingle();
        dtos![0].Id.Should().Be("bud-1");
        dtos[0].Title.Should().Be("Budapest Tournament");
        dtos[0].LocationName.Should().Be("Budapest Store");
        dtos[0].Format.Should().Be("Constructed");
    }

    [Fact]
    public async Task FullPipeline_MapsDatesAndUrl_Correctly()
    {
        var fetched = new TaskCompletionSource();
        await using var factory = CreateFactory([BudapestEvent], fetched);
        var client = factory.CreateClient();

        await fetched.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(200);

        var response = await client.GetAsync("/api/events");
        var dtos = await response.Content.ReadFromJsonAsync<List<RiftboundEventDto>>();

        dtos.Should().ContainSingle();
        dtos![0].StartDate.Should().Be(new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero));
        dtos[0].EndDate.Should().Be(new DateTimeOffset(2026, 7, 1, 18, 0, 0, TimeSpan.Zero));
        dtos[0].Url.Should().Be("https://example.com/bud");
    }

    [Fact]
    public async Task FullPipeline_ReturnsEmpty_WhenAllFetchedEventsBeyondRadius()
    {
        var fetched = new TaskCompletionSource();
        await using var factory = CreateFactory([GyorEvent], fetched);
        var client = factory.CreateClient();

        // Wait for the first fetch cycle to complete, then check the endpoint
        await fetched.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(200);

        var response = await client.GetAsync("/api/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<List<RiftboundEventDto>>();
        dtos.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task FullPipeline_ReturnsCorrectCount_WithMultipleLocalEvents()
    {
        var secondBudapestEvent = new RiftboundEvent(
            id: "bud-2",
            startDate: new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero),
            endDate: new DateTimeOffset(2026, 7, 3, 16, 0, 0, TimeSpan.Zero),
            location: new EventLocation("Pest Store", 47.5200, 19.0700),
            info: new EventInfo("Pest Tournament", "Standard", new Uri("https://example.com/bud2")));

        var fetched = new TaskCompletionSource();
        await using var factory = CreateFactory(
            [BudapestEvent, GyorEvent, secondBudapestEvent], fetched);
        var client = factory.CreateClient();

        await fetched.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(200);

        var response = await client.GetAsync("/api/events");
        var dtos = await response.Content.ReadFromJsonAsync<List<RiftboundEventDto>>();

        dtos.Should().HaveCount(2);
        dtos!.Select(d => d.Id).Should().BeEquivalentTo(["bud-1", "bud-2"]);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IReadOnlyList<RiftboundEvent> events,
        TaskCompletionSource onFetched) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IEventFetcher>(
                    new SignalingStubFetcher(events, onFetched))));

    private sealed class SignalingStubFetcher(
        IReadOnlyList<RiftboundEvent> events,
        TaskCompletionSource signal) : IEventFetcher
    {
        public Task<IReadOnlyList<RiftboundEvent>> FetchAllEventsAsync(
            CancellationToken cancellationToken = default)
        {
            signal.TrySetResult();
            return Task.FromResult(events);
        }
    }
}
