using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RiftboundCalendar.Infrastructure.Configuration;
using RiftboundCalendar.Infrastructure.Fetching;

namespace RiftboundCalendar.Infrastructure.Tests.Fetching;

public class RiftboundLocatorFetcherTests
{
    private static readonly RiftboundOptions DefaultOptions = new()
    {
        BaseUrl = "https://locator.example.com",
        RefreshIntervalMinutes = 30,
        BudapestLatitude = 47.4979,
        BudapestLongitude = 19.0402,
        RadiusKm = 50.0
    };

    private const string SingleEventPageJson = """
        {
            "count": 1,
            "next_page_number": null,
            "results": [{
                "id": 12345,
                "name": "Test Tournament",
                "start_datetime": "2026-07-01T14:00:00+00:00",
                "end_datetime": "2026-07-01T18:00:00+00:00",
                "url": null,
                "latitude": 47.5,
                "longitude": 19.05,
                "store": {"name": "Test Store"},
                "gameplay_format": {"name": "Constructed"}
            }]
        }
        """;

    private const string TwoEventPageJson = """
        {
            "count": 2,
            "next_page_number": null,
            "results": [
                {
                    "id": 12345,
                    "name": "Test Tournament",
                    "start_datetime": "2026-07-01T14:00:00+00:00",
                    "end_datetime": "2026-07-01T18:00:00+00:00",
                    "url": null,
                    "latitude": 47.5,
                    "longitude": 19.05,
                    "store": {"name": "Test Store"},
                    "gameplay_format": {"name": "Constructed"}
                },
                {
                    "id": 67890,
                    "name": "Event 2",
                    "start_datetime": "2026-07-02T14:00:00+00:00",
                    "end_datetime": "2026-07-02T18:00:00+00:00",
                    "url": null,
                    "latitude": 47.6,
                    "longitude": 19.1,
                    "store": {"name": "Store 2"},
                    "gameplay_format": {"name": "Standard"}
                }
            ]
        }
        """;

    private const string PageOneOfTwoJson = """
        {
            "count": 2,
            "next_page_number": 2,
            "results": [{
                "id": 12345,
                "name": "Test Tournament",
                "start_datetime": "2026-07-01T14:00:00+00:00",
                "end_datetime": "2026-07-01T18:00:00+00:00",
                "url": null,
                "latitude": 47.5,
                "longitude": 19.05,
                "store": {"name": "Test Store"},
                "gameplay_format": {"name": "Constructed"}
            }]
        }
        """;

    private const string PageTwoOfTwoJson = """
        {
            "count": 2,
            "next_page_number": null,
            "results": [{
                "id": 67890,
                "name": "Event 2",
                "start_datetime": "2026-07-02T14:00:00+00:00",
                "end_datetime": "2026-07-02T18:00:00+00:00",
                "url": null,
                "latitude": 47.6,
                "longitude": 19.1,
                "store": {"name": "Store 2"},
                "gameplay_format": {"name": "Standard"}
            }]
        }
        """;

    private const string EmptyResultsJson = """
        {"count": 0, "next_page_number": null, "results": []}
        """;

    private const string EventWithUrlJson = """
        {
            "count": 1,
            "next_page_number": null,
            "results": [{
                "id": 99999,
                "name": "URL Event",
                "start_datetime": "2026-08-01T10:00:00+00:00",
                "end_datetime": "2026-08-01T18:00:00+00:00",
                "url": "https://example.com/register",
                "latitude": 47.0,
                "longitude": 19.0,
                "store": {"name": "My Store"},
                "gameplay_format": {"name": "Draft"}
            }]
        }
        """;

    private const string EventWithMissingFieldsJson = """
        {
            "count": 1,
            "next_page_number": null,
            "results": [{
                "id": 11111,
                "name": "Minimal Event",
                "start_datetime": "2026-07-01T14:00:00+00:00",
                "end_datetime": "2026-07-01T18:00:00+00:00",
                "url": null,
                "latitude": 47.5,
                "longitude": 19.0,
                "store": null,
                "gameplay_format": null
            }]
        }
        """;

    private const string EventWithBadDateJson = """
        {
            "count": 1,
            "next_page_number": null,
            "results": [{
                "id": 22222,
                "name": "Bad Date Event",
                "start_datetime": "not-a-date",
                "end_datetime": "2026-07-01T18:00:00+00:00",
                "url": null,
                "latitude": 47.5,
                "longitude": 19.0,
                "store": {"name": "Store"},
                "gameplay_format": {"name": "Constructed"}
            }]
        }
        """;

    [Fact]
    public async Task FetchAllEventsAsync_ReturnsEmpty_WhenApiReturnsZeroResults()
    {
        var sut = CreateSut(EmptyResultsJson);

        var result = await sut.FetchAllEventsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAllEventsAsync_ReturnsMappedEvent_WhenApiReturnsOneEvent()
    {
        var sut = CreateSut(SingleEventPageJson);

        var result = await sut.FetchAllEventsAsync();

        result.Should().HaveCount(1);
        var evt = result[0];
        evt.Id.Should().Be("12345");
        evt.Info.Title.Should().Be("Test Tournament");
        evt.Info.Format.Should().Be("Constructed");
        evt.Info.Url.Should().Be(new Uri("https://locator.example.com/events/12345"));
        evt.Location.Name.Should().Be("Test Store");
        evt.Location.Latitude.Should().Be(47.5);
        evt.Location.Longitude.Should().Be(19.05);
        evt.StartDate.Should().Be(DateTimeOffset.Parse("2026-07-01T14:00:00+00:00"));
        evt.EndDate.Should().Be(DateTimeOffset.Parse("2026-07-01T18:00:00+00:00"));
    }

    [Fact]
    public async Task FetchAllEventsAsync_ReturnsTwoEvents_WhenApiReturnsTwoOnOnePage()
    {
        var sut = CreateSut(TwoEventPageJson);

        var result = await sut.FetchAllEventsAsync();

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("12345");
        result[1].Id.Should().Be("67890");
    }

    [Fact]
    public async Task FetchAllEventsAsync_PaginatesCorrectly_WhenMultiplePagesExist()
    {
        var sut = CreateSut(PageOneOfTwoJson, PageTwoOfTwoJson);

        var result = await sut.FetchAllEventsAsync();

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("12345");
        result[1].Id.Should().Be("67890");
    }

    [Fact]
    public async Task FetchAllEventsAsync_UsesEventUrl_WhenNotNull()
    {
        var sut = CreateSut(EventWithUrlJson);

        var result = await sut.FetchAllEventsAsync();

        result.Should().HaveCount(1);
        result[0].Info.Url.Should().Be(new Uri("https://example.com/register"));
    }

    [Fact]
    public async Task FetchAllEventsAsync_ConstructsEventPageUrl_WhenEventUrlIsNull()
    {
        var sut = CreateSut(SingleEventPageJson);

        var result = await sut.FetchAllEventsAsync();

        result[0].Info.Url.Should().Be(new Uri("https://locator.example.com/events/12345"));
    }

    [Fact]
    public async Task FetchAllEventsAsync_ReturnsEmpty_WhenHttpThrows()
    {
        var sut = CreateSutWithError();

        var result = await sut.FetchAllEventsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAllEventsAsync_UsesUnknownFallbacks_WhenStoreAndFormatAreNull()
    {
        var sut = CreateSut(EventWithMissingFieldsJson);

        var result = await sut.FetchAllEventsAsync();

        result.Should().HaveCount(1);
        result[0].Location.Name.Should().Be("Unknown");
        result[0].Info.Format.Should().Be("Unknown");
    }

    [Fact]
    public async Task FetchAllEventsAsync_SkipsEvent_WhenStartDatetimeIsInvalid()
    {
        var sut = CreateSut(EventWithBadDateJson);

        var result = await sut.FetchAllEventsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAllEventsAsync_UsesDefaultDuration_WhenEndDatetimeIsNull()
    {
        const string json = """
            {
                "count": 1,
                "next_page_number": null,
                "results": [{
                    "id": 33333,
                    "name": "No End Event",
                    "start_datetime": "2026-07-01T14:00:00+00:00",
                    "end_datetime": null,
                    "url": null,
                    "latitude": 47.5,
                    "longitude": 19.0,
                    "store": {"name": "Store"},
                    "gameplay_format": {"name": "Constructed"}
                }]
            }
            """;
        var sut = CreateSut(json);

        var result = await sut.FetchAllEventsAsync();

        result.Should().HaveCount(1);
        result[0].EndDate.Should().Be(DateTimeOffset.Parse("2026-07-01T18:00:00+00:00"));
    }

    private RiftboundLocatorFetcher CreateSut(string firstResponse, params string[] additionalResponses)
    {
        var handler = new FakeHttpMessageHandler([firstResponse, .. additionalResponses]);
        return new RiftboundLocatorFetcher(
            new HttpClient(handler),
            Options.Create(DefaultOptions),
            NullLogger<RiftboundLocatorFetcher>.Instance);
    }

    private RiftboundLocatorFetcher CreateSutWithError()
    {
        return new RiftboundLocatorFetcher(
            new HttpClient(new FakeHttpMessageHandler()),
            Options.Create(DefaultOptions),
            NullLogger<RiftboundLocatorFetcher>.Instance);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<string?> _responses;

        public FakeHttpMessageHandler(params string?[] responses)
        {
            _responses = new Queue<string?>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = _responses.TryDequeue(out var r) ? r : null;
            if (content is null) throw new HttpRequestException("Simulated network error");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }
}
