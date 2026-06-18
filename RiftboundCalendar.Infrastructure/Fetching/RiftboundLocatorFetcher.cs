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
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string PushStart = "self.__next_f.push([1,\"";
    private const string PushEnd = "\\n\"";
    private const string PageSizeMarker = "page_size";
    private const int MaxChunkIdLength = 10;
    private const int MaxLocalEventCount = 500;
    private const string BackendApiBaseUrl = "https://api.riftbound.uvsgames.com/api/magic-events/";

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
            var eventIds = ExtractEventIds(html);

            if (eventIds.Count == 0)
            {
                _logger.LogWarning("No event IDs found in locator RSC data");
                return [];
            }

            _logger.LogInformation("Found {Count} event IDs from locator", eventIds.Count);

            var events = new List<RiftboundEvent>(eventIds.Count);
            foreach (var id in eventIds)
            {
                var evt = await FetchEventByIdAsync(id, cancellationToken);
                if (evt is not null) events.Add(evt);
            }

            _logger.LogInformation("Fetched {Count} full events from backend API", events.Count);
            return events;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to fetch events from Riftbound locator");
            return [];
        }
    }

    private static IReadOnlyList<int> ExtractEventIds(string html)
    {
        List<int>? bestIds = null;
        var bestCount = 0;
        var pos = 0;

        while (pos < html.Length)
        {
            var start = html.IndexOf(PushStart, pos, StringComparison.Ordinal);
            if (start < 0) break;

            var contentStart = start + PushStart.Length;
            var end = html.IndexOf(PushEnd, contentStart, StringComparison.Ordinal);
            if (end < 0) { pos = contentStart; continue; }

            var escaped = html[contentStart..end];
            if (escaped.Contains(PageSizeMarker, StringComparison.Ordinal))
            {
                var unescaped = UnescapeJsString(escaped);
                if (unescaped is not null)
                    CollectBestChunk(unescaped, ref bestIds, ref bestCount);
            }

            pos = end + PushEnd.Length;
        }

        return bestIds ?? [];
    }

    private static void CollectBestChunk(string content, ref List<int>? bestIds, ref int bestCount)
    {
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!line.Contains(PageSizeMarker, StringComparison.Ordinal)) continue;

            var colonIdx = line.IndexOf(':');
            if (colonIdx < 0 || colonIdx > MaxChunkIdLength) continue;

            var json = line[(colonIdx + 1)..];
            if (!json.StartsWith('{')) continue;

            try
            {
                var page = JsonSerializer.Deserialize<LocalEventPageDto>(json, JsonOptions);
                if (page?.Results is { Count: > 0 } && page.Count is > 0 and <= MaxLocalEventCount
                    && page.Count > bestCount)
                {
                    bestCount = page.Count;
                    bestIds = page.Results.Select(r => r.Id).ToList();
                }
            }
            catch (JsonException) { }
        }
    }

    private async Task<RiftboundEvent?> FetchEventByIdAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            var json = await _httpClient.GetStringAsync(
                $"{BackendApiBaseUrl}{id}/?format=json", cancellationToken);
            var dto = JsonSerializer.Deserialize<BackendEventDto>(json, JsonOptions);
            return dto is null ? null : MapToEvent(dto);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch event {Id} from backend API", id);
            return null;
        }
    }

    private static readonly TimeSpan DefaultEventDuration = TimeSpan.FromHours(4);

    private RiftboundEvent? MapToEvent(BackendEventDto dto)
    {
        if (!DateTimeOffset.TryParse(dto.StartDatetime, out var startDate)) return null;
        var endDate = DateTimeOffset.TryParse(dto.EndDatetime, out var parsed)
            ? parsed
            : startDate + DefaultEventDuration;
        var url = !string.IsNullOrEmpty(dto.Url) ? new Uri(dto.Url) : new Uri(_options.BaseUrl);
        return new RiftboundEvent(
            id: dto.Id.ToString(),
            startDate: startDate,
            endDate: endDate,
            location: new EventLocation(dto.Store?.Name ?? "Unknown", dto.Latitude ?? 0.0, dto.Longitude ?? 0.0),
            info: new EventInfo(dto.Name, dto.FormatPretty ?? "Unknown", url));
    }

    private static string? UnescapeJsString(string escaped)
    {
        try { return JsonSerializer.Deserialize<string>("\"" + escaped + "\""); }
        catch (JsonException) { return null; }
    }

    private sealed record LocalEventPageDto(
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("results")] List<IdDto> Results);

    private sealed record IdDto(
        [property: JsonPropertyName("id")] int Id);

    private sealed record BackendEventDto(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("start_datetime")] string StartDatetime,
        [property: JsonPropertyName("end_datetime")] string EndDatetime,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("format_pretty")] string? FormatPretty,
        [property: JsonPropertyName("latitude")] double? Latitude,
        [property: JsonPropertyName("longitude")] double? Longitude,
        [property: JsonPropertyName("store")] BackendStoreDto? Store);

    private sealed record BackendStoreDto(
        [property: JsonPropertyName("name")] string Name);
}
