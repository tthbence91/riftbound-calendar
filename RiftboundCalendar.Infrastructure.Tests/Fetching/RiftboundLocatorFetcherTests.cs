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

    private const string EmptyResultsHtml = """
        <html><body>
        <script>self.__next_f.push([1,"34:{\"page_size\":25,\"count\":0,\"total\":0,\"current_page_number\":1,\"next_page_number\":null,\"next\":null,\"previous\":null,\"previous_page_number\":null,\"results\":[]}\n"])</script>
        </body></html>
        """;

    private const string OneEventHtml = """
        <html><body>
        <script>self.__next_f.push([1,"34:{\"page_size\":25,\"count\":1,\"total\":1,\"current_page_number\":1,\"next_page_number\":null,\"next\":null,\"previous\":null,\"previous_page_number\":null,\"results\":[{\"id\":12345,\"name\":\"Test Tournament\",\"game\":3,\"start_datetime\":\"2026-07-01T14:00:00+00:00\",\"end_datetime\":\"2026-07-01T18:00:00+00:00\",\"url\":null,\"format_pretty\":\"Constructed\",\"store\":{\"id\":999,\"name\":\"Test Store\",\"full_address\":\"Test Street 1\",\"latitude\":47.5,\"longitude\":19.05}}]}\n"])</script>
        </body></html>
        """;

    private const string EventWithUrlHtml = """
        <html><body>
        <script>self.__next_f.push([1,"34:{\"page_size\":25,\"count\":1,\"total\":1,\"current_page_number\":1,\"next_page_number\":null,\"next\":null,\"previous\":null,\"previous_page_number\":null,\"results\":[{\"id\":99999,\"name\":\"URL Event\",\"game\":3,\"start_datetime\":\"2026-08-01T10:00:00+00:00\",\"end_datetime\":\"2026-08-01T18:00:00+00:00\",\"url\":\"https://example.com/register\",\"format_pretty\":\"Draft\",\"store\":{\"id\":1,\"name\":\"My Store\",\"full_address\":\"Addr\",\"latitude\":47.0,\"longitude\":19.0}}]}\n"])</script>
        </body></html>
        """;

    [Fact]
    public async Task FetchAllEventsAsync_ReturnsEmpty_WhenRscHasZeroResults()
    {
        var sut = CreateSut(EmptyResultsHtml);

        var result = await sut.FetchAllEventsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task FetchAllEventsAsync_ReturnsMappedEvent_WhenRscHasOneEvent()
    {
        var sut = CreateSut(OneEventHtml);

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
        var sut = CreateSut(EventWithUrlHtml);

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
    public async Task FetchAllEventsAsync_FetchesBothPages_WhenNextUrlPresent()
    {
        var firstPageHtml = """
            <html><body>
            <script>self.__next_f.push([1,"34:{\"page_size\":1,\"count\":2,\"total\":2,\"current_page_number\":1,\"next_page_number\":2,\"next\":\"https://api.example.com/api/magic-events/?page=2\",\"previous\":null,\"previous_page_number\":null,\"results\":[{\"id\":1,\"name\":\"Event 1\",\"game\":3,\"start_datetime\":\"2026-07-01T14:00:00+00:00\",\"end_datetime\":\"2026-07-01T18:00:00+00:00\",\"url\":null,\"format_pretty\":\"Constructed\",\"store\":{\"id\":1,\"name\":\"Store 1\",\"full_address\":\"Addr 1\",\"latitude\":47.5,\"longitude\":19.0}}]}\n"])</script>
            </body></html>
            """;
        const string secondPageJson =
            """{"page_size":1,"count":2,"total":2,"current_page_number":2,"next_page_number":null,"next":null,"previous":"...","previous_page_number":1,"results":[{"id":2,"name":"Event 2","game":3,"start_datetime":"2026-07-02T14:00:00+00:00","end_datetime":"2026-07-02T18:00:00+00:00","url":null,"format_pretty":"Constructed","store":{"id":2,"name":"Store 2","full_address":"Addr 2","latitude":47.6,"longitude":19.1}}]}""";

        var sut = CreateSut(firstPageHtml, secondPageJson);

        var result = await sut.FetchAllEventsAsync();

        result.Should().HaveCount(2);
        result[0].Id.Should().Be("1");
        result[1].Id.Should().Be("2");
    }

    private RiftboundLocatorFetcher CreateSut(string firstResponse, string? secondResponse = null)
    {
        var handler = secondResponse is null
            ? new FakeHttpMessageHandler(firstResponse)
            : new FakeHttpMessageHandler(firstResponse, secondResponse);
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
