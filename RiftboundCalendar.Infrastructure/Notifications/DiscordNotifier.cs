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
            await SendAsync(new { embeds = batch.Select(BuildNewEventEmbed).ToArray() }, cancellationToken);
    }

    public async Task NotifyStatusChangedAsync(
        IReadOnlyList<StatusChange> changes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.WebhookUrl)) return;
        if (changes.Count == 0) return;

        foreach (var batch in changes.Chunk(MaxEmbedsPerMessage))
            await SendAsync(new { embeds = batch.Select(BuildStatusEmbed).ToArray() }, cancellationToken);
    }

    private async Task SendAsync(object payload, CancellationToken cancellationToken)
    {
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

    private static object BuildNewEventEmbed(RiftboundEvent e)
    {
        var (start, end, date) = FormatDate(e);
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

    private static object BuildStatusEmbed(StatusChange change)
    {
        var (headline, color) = change.NewStatus switch
        {
            RegistrationStatus.Open when change.OldStatus == RegistrationStatus.Full
                => ("🔓 Ismét van szabad hely!", 0x2ECC71),
            RegistrationStatus.Open  => ("🟢 Megnyílt a jelentkezés!", 0x2ECC71),
            RegistrationStatus.Full  => ("🔴 Betelt az esemény", 0xE74C3C),
            RegistrationStatus.Closed => ("🔒 Lezárult a jelentkezés", 0x95A5A6),
            _ => ("🔄 Státusz változás", EmbedColor)
        };

        var (_, _, date) = FormatDate(change.Event);
        var e = change.Event;
        return new
        {
            title = $"{headline} — {e.Info.Title}",
            url = e.Info.Url.ToString(),
            color,
            fields = new[]
            {
                new { name = "📍 Helyszín", value = e.Location.Name, inline = true },
                new { name = "📅 Időpont", value = date, inline = true }
            },
            footer = new { text = $"Riftbound Eseménynaptár  ·  {StatusLabel(change.OldStatus)} → {StatusLabel(change.NewStatus)}" }
        };
    }

    private static (DateTime start, DateTime end, string formatted) FormatDate(RiftboundEvent e)
    {
        var start = e.StartDate.ToLocalTime().DateTime;
        var end = e.EndDate.ToLocalTime().DateTime;
        return (start, end, $"{start:yyyy. MM. dd.} {start:HH:mm}–{end:HH:mm}");
    }

    private static string StatusLabel(RegistrationStatus status) => status switch
    {
        RegistrationStatus.Open   => "Nyitott",
        RegistrationStatus.Full   => "Betelt",
        RegistrationStatus.Closed => "Lezárt",
        _                         => status.ToString()
    };
}
