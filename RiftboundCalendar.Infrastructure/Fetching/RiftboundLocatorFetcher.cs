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
            var numMiles = (int)Math.Round(_options.RadiusKm / KmPerMile);
            var startDateAfter = DateTime.UtcNow.Date.AddHours(-2)
                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var allEvents = new List<RiftboundEvent>();
            int? nextPage = 1;

            while (nextPage.HasValue)
            {
                var page = await FetchPageAsync(startDateAfter, numMiles, nextPage.Value, cancellationToken);
                if (page is null) break;

                allEvents.AddRange(page.Events);
                _logger.LogInformation(
                    "Fetched page {Page}: {Fetched}/{Total} events",
                    nextPage, allEvents.Count, page.TotalCount);

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

    private async Task<PageResult?> FetchPageAsync(
        string startDateAfter, int numMiles, int page, CancellationToken cancellationToken)
    {
        var url = BuildUrl(startDateAfter, numMiles, page);
        var json = await _httpClient.GetStringAsync(url, cancellationToken);
        var dto = JsonSerializer.Deserialize<EventPageDto>(json, JsonOptions);

        if (dto?.Results is null or { Count: 0 }) return null;

        var events = dto.Results.Select(MapToEvent).OfType<RiftboundEvent>().ToList();
        return new PageResult(events, dto.Count, dto.NextPageNumber);
    }

    private sealed record PageResult(
        List<RiftboundEvent> Events,
        int TotalCount,
        int? NextPageNumber);

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
            : new Uri($"{_options.BaseUrl}/events/{dto.Id}");
        return new RiftboundEvent(
            id: dto.Id.ToString(),
            startDate: startDate,
            endDate: endDate,
            location: new EventLocation(dto.Store?.Name ?? "Unknown", dto.Latitude ?? 0.0, dto.Longitude ?? 0.0),
            info: new EventInfo(dto.Name, dto.GameplayFormat?.Name ?? "Unknown", url))
        {
            Stats = new EventStats
            {
                StoreId = dto.Store?.Id?.ToString(),
                LifecycleStatus = dto.Settings?.EventLifecycleStatus,
                CostInCents = dto.CostInCents,
                Currency = dto.Currency,
                Capacity = dto.Capacity,
                RegisteredCount = dto.RegisteredUserCount
            }
        };
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
        [property: JsonPropertyName("gameplay_format")] GameplayFormatDto? GameplayFormat,
        [property: JsonPropertyName("cost_in_cents")] int? CostInCents,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("capacity")] int? Capacity,
        [property: JsonPropertyName("registered_user_count")] int? RegisteredUserCount,
        [property: JsonPropertyName("settings")] SettingsDto? Settings);

    private sealed record SettingsDto(
        [property: JsonPropertyName("event_lifecycle_status")] string? EventLifecycleStatus);

    private sealed record StoreDto(
        [property: JsonPropertyName("id")] int? Id,
        [property: JsonPropertyName("name")] string Name);

    private sealed record GameplayFormatDto(
        [property: JsonPropertyName("name")] string Name);
}
