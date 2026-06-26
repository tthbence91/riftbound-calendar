namespace RiftboundCalendar.Core.Entities;

public sealed class NotificationStateEntry
{
    public string EventId { get; set; } = "";
    public RegistrationStatus LastStatus { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
}
