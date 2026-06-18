using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RiftboundCalendar.Core.Entities;
using RiftboundCalendar.Core.Interfaces;
using RiftboundCalendar.Infrastructure.Configuration;

namespace RiftboundCalendar.Infrastructure.Fetching;

public sealed class RiftboundLocatorFetcher : IEventFetcher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string PushStart = "self.__next_f.push([1,\"";
    private const string PushEnd = "\\n\"";
    private const string PageSizeMarker = "page_size";
    private const int MaxChunkIdLength = 10;

    private readonly HttpClient _httpClient;
    private readonly RiftboundOptions _options;
    private readonly ILogger<RiftboundLocatorFetcher> _logger;

    public RiftboundLocatorFetcher(
        HttpClient httpClient,
        IOptions<RiftboundOptions> options,
        ILogger<RiftboundLocatorFetcher> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RiftboundEvent>> FetchAllEventsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(_options.BaseUrl, cancellationToken);
            var firstPage = ExtractFromPage(html);
            if (firstPage is null || firstPage.Results.Count == 0) return [];

            var allDtos = new List<EventDto>(firstPage.Results);
            var nextUrl = firstPage.Next;

            while (nextUrl is not null && !cancellationToken.IsCancellationRequested)
            {
                var json = await _httpClient.GetStringAsync(nextUrl, cancellationToken);
                var page = JsonSerializer.Deserialize<PaginatedResponseDto>(json, JsonOptions);
                if (page is null) break;
                allDtos.AddRange(page.Results);
                nextUrl = page.Next;
            }

            return [.. allDtos.Select(MapToEvent)];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to fetch events from Riftbound locator");
            return [];
        }
    }

    private static PaginatedResponseDto? ExtractFromPage(string html)
    {
        var pos = 0;
        while (pos < html.Length)
        {
            var start = html.IndexOf(PushStart, pos, StringComparison.Ordinal);
            if (start < 0) break;

            var contentStart = start + PushStart.Length;
            var end = html.IndexOf(PushEnd, contentStart, StringComparison.Ordinal);
            if (end < 0) { pos = contentStart; continue; }

            var escaped = html[contentStart..end];
            if (!escaped.Contains(PageSizeMarker, StringComparison.Ordinal))
            {
                pos = end + PushEnd.Length;
                continue;
            }

            var unescaped = UnescapeJsString(escaped);
            if (unescaped is null) { pos = end + PushEnd.Length; continue; }

            var colonIdx = unescaped.IndexOf(':');
            if (colonIdx < 0 || colonIdx > MaxChunkIdLength)
            {
                pos = end + PushEnd.Length;
                continue;
            }

            var json = unescaped[(colonIdx + 1)..];
            try
            {
                var result = JsonSerializer.Deserialize<PaginatedResponseDto>(json, JsonOptions);
                if (result is not null) return result;
            }
            catch (JsonException) { }

            pos = end + PushEnd.Length;
        }
        return null;
    }

    private static string? UnescapeJsString(string escaped)
    {
        try
        {
            return JsonSerializer.Deserialize<string>("\"" + escaped + "\"");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private RiftboundEvent MapToEvent(EventDto dto)
    {
        var url = dto.Url is not null
            ? new Uri(dto.Url)
            : new Uri(_options.BaseUrl);

        return new RiftboundEvent(
            id: dto.Id.ToString(),
            startDate: DateTimeOffset.Parse(dto.StartDatetime),
            endDate: DateTimeOffset.Parse(dto.EndDatetime),
            location: new EventLocation(dto.Store.Name, dto.Store.Latitude, dto.Store.Longitude),
            info: new EventInfo(dto.Name, dto.FormatPretty ?? "Unknown", url));
    }

    private sealed record PaginatedResponseDto(
        [property: JsonPropertyName("results")] List<EventDto> Results,
        [property: JsonPropertyName("next")] string? Next);

    private sealed record EventDto(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("start_datetime")] string StartDatetime,
        [property: JsonPropertyName("end_datetime")] string EndDatetime,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("format_pretty")] string? FormatPretty,
        [property: JsonPropertyName("store")] StoreDto Store);

    private sealed record StoreDto(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude);
}
