using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RiftboundCalendar.Api.Controllers;
using RiftboundCalendar.Api.Dtos;
using RiftboundCalendar.Core.Entities;
using RiftboundCalendar.Core.Interfaces;

namespace RiftboundCalendar.Api.Tests.Controllers;

public class EventsControllerUnitTests
{
    private static readonly RiftboundEvent SampleEvent = new(
        id: "1",
        startDate: new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero),
        endDate: new DateTimeOffset(2026, 7, 1, 18, 0, 0, TimeSpan.Zero),
        location: new EventLocation("Test Store", 47.5, 19.05),
        info: new EventInfo("Test Tournament", "Constructed", new Uri("https://example.com")));

    [Fact]
    public async Task GetEvents_Returns200WithDtoList_WhenRepositoryHasEvents()
    {
        var mockRepo = new Mock<IEventRepository>();
        mockRepo.Setup(r => r.GetEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([SampleEvent]);
        var controller = new EventsController(mockRepo.Object);

        var result = await controller.GetEvents(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var dtos = ok.Value.Should().BeAssignableTo<IList<RiftboundEventDto>>().Subject;
        dtos.Should().HaveCount(1);
        dtos[0].Id.Should().Be("1");
        dtos[0].Title.Should().Be("Test Tournament");
    }

    [Fact]
    public async Task GetEvents_Returns200WithEmptyList_WhenRepositoryIsEmpty()
    {
        var mockRepo = new Mock<IEventRepository>();
        mockRepo.Setup(r => r.GetEventsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var controller = new EventsController(mockRepo.Object);

        var result = await controller.GetEvents(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IList<RiftboundEventDto>>()
            .Which.Should().BeEmpty();
    }
}

public class EventsControllerIntegrationTests
{
    [Fact]
    public async Task GetEvents_Returns200JsonArray_ViaTestServer()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                    services.AddSingleton<IEventRepository>(new StubEventRepository([]))));

        var response = await factory.CreateClient().GetAsync("/api/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<List<RiftboundEventDto>>();
        dtos.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetEvents_ReturnsMappedDtos_ViaTestServer()
    {
        var events = new List<RiftboundEvent>
        {
            new("42",
                new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 1, 18, 0, 0, TimeSpan.Zero),
                new EventLocation("My Store", 47.5, 19.0),
                new EventInfo("Big Tournament", "Draft", new Uri("https://example.com/bt")))
        };

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                    services.AddSingleton<IEventRepository>(new StubEventRepository(events))));

        var response = await factory.CreateClient().GetAsync("/api/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dtos = await response.Content.ReadFromJsonAsync<List<RiftboundEventDto>>();
        dtos.Should().HaveCount(1);
        dtos![0].Id.Should().Be("42");
        dtos[0].Title.Should().Be("Big Tournament");
        dtos[0].LocationName.Should().Be("My Store");
    }

    private sealed class StubEventRepository(IReadOnlyList<RiftboundEvent> events) : IEventRepository
    {
        public Task<IReadOnlyList<RiftboundEvent>> GetEventsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(events);
    }
}
