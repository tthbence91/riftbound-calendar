using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Api.Dtos;

public sealed class StatusHistoryEntryDto
{
    public string OldStatus { get; init; } = "";
    public string NewStatus { get; init; } = "";
    public DateTimeOffset ChangedAt { get; init; }

    public static StatusHistoryEntryDto FromDomain(EventStatusHistoryEntry e) => new()
    {
        OldStatus = e.OldStatus.ToString(),
        NewStatus = e.NewStatus.ToString(),
        ChangedAt = e.ChangedAt
    };
}
