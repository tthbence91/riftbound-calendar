namespace RiftboundCalendar.Web.Services;

public sealed class StatusHistoryEntryDto
{
    public string OldStatus { get; init; } = "";
    public string NewStatus { get; init; } = "";
    public DateTimeOffset ChangedAt { get; init; }
}
