using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace RiftboundCalendar.Web.Services;

public sealed class EventApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EventApiClient> _logger;

    public EventApiClient(HttpClient httpClient, ILogger<EventApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RiftboundEventDto>> GetEventsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var events = await _httpClient.GetFromJsonAsync<List<RiftboundEventDto>>(
                "api/events", cancellationToken);
            return events ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to fetch events from API");
            return [];
        }
    }
}
