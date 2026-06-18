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

    // RSC chunk with zero results — no IDs extracted
    private const string EmptyResultsHtml = """
        <html><body>
        <script>self.__next_f.push([1,"34:{\"page_size\":25,\"count\":0,\"results\":[]}\n"])</script>
        </body></html>
        """;

    // Single-line RSC chunk with one event ID
    private const string OneIdHtml = """
        <html><body>
        <script>self.__next_f.push([1,"33:{\"page_size\":25,\"count\":1,\"results\":[{\"id\":12345}]}\n"])</script>
        </body></html>
        """;

    // Multi-line RSC: outer chunk 17:[] followed by local chunk 33: with two IDs
    private const string TwoIdMultiLineHtml = """
        <html><body>
        <script>self.__next_f.push([1,"17:[]\n33:{\"page_size\":25,\"count\":2,\"results\":[{\"id\":12345},{\"id\":67890}]}\n"])</script>
        </body></html>
        """;

    // Global chunk with high count (>500) — must be skipped
    private const string GlobalChunkHtml = """
        <html><body>
        <script>self.__next_f.push([1,"31:{\"page_size\":25,\"count\":60813,\"results\":[{\"id\":1},{\"id\":2}]}\n"])</script>
        </body></html>
        """;

    // RSC chunk with one ID for the URL event
    private const string OneIdWithUrlHtml = """
        <html><body>
        <script>self.__next_f.push([1,"33:{\"page_size\":25,\"count\":1,\"results\":[{\"id\":99999}]}\n"])</script>
        </body></html>
        """;

    private const string OneEventApiJson =
        """{"id":12345,"name":"Test Tournament","start_datetime":"2026-07-01T14:00:00+00:00","end_datetime":"2026-07-01T18:00:00+00:00","url":null,"format_pretty":"Constructed","latitude":47.5,"longitude":19.05,"store":{"name":"Test Store"}}""";

    private const string SecondEventApiJson =
        """{"id":67890,"name":"Event 2","start_datetime":"2026-07-02T14:00:00+00:00","end_datetime":"2026-07-02T18:00:00+00:00","url":null,"format_pretty":"Standard","latitude":47.6,"longitude":19.1,"store":{"name":"Store 2"}}""";

    private const string EventWithUrlApiJson =
        """{"id":99999,"name":"URL Event","start_datetime":"2026-08-01T10:00:00+00:00","end_datetime":"2026-08-01T18:00:00+00:00","url":"https://example.com/register","format_pretty":"Draft","latitude":47.0,"longitude":19.0,"store":{"name":"My Store"}}""";

    [Fact]
    public async Task FetchAllEventsAsync_ReturnsEmpty_WhenRscHasZeroResults()
    {
        var sut = CreateSut(EmptyResultsHtml);

        var result = await sut.FetchAllEventsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAllEventsAsync_ReturnsMappedEvent_WhenRscHasOneId()
    {
        var sut = CreateSut(OneIdHtml, OneEventApiJson);

        var result = await sut.FetchAllEventsAsync();

        result.Should().HaveCount(1);
        var evt = result[0];
        evt.Id.Should().Be("12345");
        evt.Info.Title.Should().Be("Test Tournament");
        evt.Info.Format.Should().Be("Constructed");
        evt.Info.Url.Should().Be(new Uri("https://locator.example.com"));
        evt.Location.Name.Should().Be("Test Store");
        evt.Location.Latitude.Should().Be(47.5);
        evt.Location.Longitude.Should().Be(19.05);
        evt.StartDate.Should().Be(DateTimeOffset.Parse("2026-07-01T14:00:00+00:00"));
        evt.EndDate.Should().Be(DateTimeOffset.Parse("2026-07-01T18:00:00+00:00"));
    }

    [Fact]
    public async Task FetchAllEventsAsync_UsesEventUrl_WhenNotNull()
    {
        var sut = CreateSut(OneIdWithUrlHtml, EventWithUrlApiJson);

        var result = await sut.FetchAllEventsAsync();

        result.Should().HaveCount(1);
        result[0].Info.Url.Should().Be(new Uri("https://example.com/register"));
    }

    [Fact]
    public async Task FetchAllEventsAsync_ReturnsEmpty_WhenHttpThrows()
    {
        var sut = CreateSutWithError();

        var result = await sut.FetchAllEventsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAllEventsAsync_ReturnsTwoEvents_WhenRscHasTwoIds()
    {
        var sut = CreateSut(TwoIdMultiLineHtml, OneEventApiJson, SecondEventApiJson);

        var result = await sut.FetchAllEventsAsync();

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("12345");
        result[1].Id.Should().Be("67890");
    }

    [Fact]
    public async Task FetchAllEventsAsync_ReturnsEmpty_WhenRscChunkExceedsMaxLocalCount()
    {
        var sut = CreateSut(GlobalChunkHtml);

        var result = await sut.FetchAllEventsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAllEventsAsync_PrefersHigherCountChunk_WhenMultiplePushCallsPresent()
    {
        // chunk 32: (count=3) comes first, chunk 33: (count=21) second — must pick 33:
        const string html = """
            <html><body>
            <script>self.__next_f.push([1,"32:{\"page_size\":25,\"count\":3,\"results\":[{\"id\":1},{\"id\":2},{\"id\":3}]}\n"])</script>
            <script>self.__next_f.push([1,"33:{\"page_size\":25,\"count\":21,\"results\":[{\"id\":12345}]}\n"])</script>
            </body></html>
            """;
        var sut = CreateSut(html, OneEventApiJson);

        var result = await sut.FetchAllEventsAsync();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("12345");
    }

    [Fact]
    public async Task FetchAllEventsAsync_SkipsFailedEvent_AndReturnsSuccessful()
    {
        // Only one API response provided — second event fetch throws
        var sut = CreateSut(TwoIdMultiLineHtml, OneEventApiJson);

        var result = await sut.FetchAllEventsAsync();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("12345");
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
