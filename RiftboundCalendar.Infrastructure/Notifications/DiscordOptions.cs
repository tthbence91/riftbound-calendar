namespace RiftboundCalendar.Infrastructure.Notifications;

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";
    public string WebhookUrl { get; set; } = "";
}
