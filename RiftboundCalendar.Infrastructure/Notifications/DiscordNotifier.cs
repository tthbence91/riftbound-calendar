using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Infrastructure.Notifications;

public sealed class DiscordNotifier
{
    private const int MaxEmbedsPerMessage = 10;
    private const int EmbedColor = 0x3498DB;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly DiscordOptions _options;
    private readonly ILogger<DiscordNotifier> _logger;

    public DiscordNotifier(
        IHttpClientFactory httpFactory,
        IOptions<DiscordOptions> options,
        ILogger<DiscordNotifier> logger)
    {
        _httpFactory = httpFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyNewEventsAsync(
        IReadOnlyList<RiftboundEvent> events,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.WebhookUrl)) return;

        foreach (var batch in events.Chunk(MaxEmbedsPerMessage))
            await SendBatchAsync(batch, cancellationToken);
    }

    private async Task SendBatchAsync(RiftboundEvent[] events, CancellationToken cancellationToken)
    {
        var payload = new { embeds = events.Select(BuildEmbed).ToArray() };
        try
        {
            using var http = _httpFactory.CreateClient();
            var response = await http.PostAsJsonAsync(
                _options.WebhookUrl, payload, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Discord webhook returned {Status}", response.StatusCode);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to send Discord notification");
        }
    }

    private static object BuildEmbed(RiftboundEvent e)
    {
        var start = e.StartDate.ToLocalTime();
        var end = e.EndDate.ToLocalTime();
        var date = $"{start:yyyy. MM. dd.} {start:HH:mm}–{end:HH:mm}";

        return new
        {
            title = e.Info.Title,
            url = e.Info.Url.ToString(),
            color = EmbedColor,
            fields = new[]
            {
                new { name = "📍 Helyszín", value = e.Location.Name, inline = true },
                new { name = "📅 Időpont", value = date, inline = true },
                new { name = "🎮 Formátum", value = e.Info.Format, inline = true }
            },
            footer = new { text = "Riftbound Eseménynaptár" }
        };
    }
}
