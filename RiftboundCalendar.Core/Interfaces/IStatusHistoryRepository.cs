namespace RiftboundCalendar.Core.Interfaces;

using RiftboundCalendar.Core.Entities;

public interface IStatusHistoryRepository
{
    Task AppendAsync(IReadOnlyList<EventStatusHistoryEntry> entries, CancellationToken ct);
    Task<IReadOnlyList<EventStatusHistoryEntry>> GetByEventIdAsync(string eventId, CancellationToken ct);
    Task DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken ct);
}
