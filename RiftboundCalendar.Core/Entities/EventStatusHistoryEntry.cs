namespace RiftboundCalendar.Core.Entities;

public sealed class EventStatusHistoryEntry
{
    public int Id { get; set; }
    public string EventId { get; set; } = "";
    public DateTimeOffset EventEndDate { get; set; }
    public RegistrationStatus OldStatus { get; set; }
    public RegistrationStatus NewStatus { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
}
