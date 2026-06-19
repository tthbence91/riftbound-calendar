using System.Globalization;
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

    private const string ApiBaseUrl = "https://api.riftbound.uvsgames.com";
    private const string GameSlug = "riftbound";
    private const int PageSize = 25;
    private const double KmPerMile = 1.60934;
    private static readonly TimeSpan DefaultEventDuration = TimeSpan.FromHours(4);

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
            // Integer miles required by API; 50 km ≈ 31 miles
            var numMiles = (int)Math.Round(_options.RadiusKm / KmPerMile);
            // Budapest midnight = UTC-2h, start from yesterday 22:00 UTC to include all of today
            var startDateAfter = DateTime.UtcNow.Date.AddHours(-2)
                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var allEvents = new List<RiftboundEvent>();
            int? nextPage = 1;

            while (nextPage.HasValue)
            {
                var url = BuildUrl(startDateAfter, numMiles, nextPage.Value);
                var json = await _httpClient.GetStringAsync(url, cancellationToken);
                var page = JsonSerializer.Deserialize<EventPageDto>(json, JsonOptions);

                if (page?.Results is null or { Count: 0 }) break;

                foreach (var dto in page.Results)
                {
                    var evt = MapToEvent(dto);
                    if (evt is not null) allEvents.Add(evt);
                }

                _logger.LogInformation(
                    "Fetched page {Page}: {Fetched}/{Total} events",
                    nextPage, allEvents.Count, page.Count);

                nextPage = page.NextPageNumber;
            }

            _logger.LogInformation("Fetched {Count} events from Riftbound API", allEvents.Count);
            return allEvents;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to fetch events from Riftbound API");
            return [];
        }
    }

    private string BuildUrl(string startDateAfter, int numMiles, int page)
    {
        var lat = _options.BudapestLatitude.ToString("F4", CultureInfo.InvariantCulture);
        var lng = _options.BudapestLongitude.ToString("F4", CultureInfo.InvariantCulture);
        return $"{ApiBaseUrl}/api/v2/events/" +
            $"?start_date_after={Uri.EscapeDataString(startDateAfter)}" +
            $"&display_statuses=upcoming&display_statuses=inProgress" +
            $"&game_slug={GameSlug}" +
            $"&latitude={lat}&longitude={lng}" +
            $"&num_miles={numMiles}" +
            $"&upcoming_only=true" +
            $"&page={page}&page_size={PageSize}";
    }

    private RiftboundEvent? MapToEvent(EventDto dto)
    {
        if (!DateTimeOffset.TryParse(dto.StartDatetime, out var startDate)) return null;
        var endDate = DateTimeOffset.TryParse(dto.EndDatetime, out var parsed)
            ? parsed
            : startDate + DefaultEventDuration;
        var url = Uri.TryCreate(dto.Url, UriKind.Absolute, out var eventUri)
            ? eventUri
            : new Uri(_options.BaseUrl);
        return new RiftboundEvent(
            id: dto.Id.ToString(),
            startDate: startDate,
            endDate: endDate,
            location: new EventLocation(dto.Store?.Name ?? "Unknown", dto.Latitude ?? 0.0, dto.Longitude ?? 0.0),
            info: new EventInfo(dto.Name, dto.GameplayFormat?.Name ?? "Unknown", url));
    }

    private sealed record EventPageDto(
        [property: JsonPropertyName("count")] int Count,
        [property: JsonPropertyName("next_page_number")] int? NextPageNumber,
        [property: JsonPropertyName("results")] List<EventDto> Results);

    private sealed record EventDto(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("start_datetime")] string StartDatetime,
        [property: JsonPropertyName("end_datetime")] string EndDatetime,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("latitude")] double? Latitude,
        [property: JsonPropertyName("longitude")] double? Longitude,
        [property: JsonPropertyName("store")] StoreDto? Store,
        [property: JsonPropertyName("gameplay_format")] GameplayFormatDto? GameplayFormat);

    private sealed record StoreDto(
        [property: JsonPropertyName("name")] string Name);

    private sealed record GameplayFormatDto(
        [property: JsonPropertyName("name")] string Name);
}
