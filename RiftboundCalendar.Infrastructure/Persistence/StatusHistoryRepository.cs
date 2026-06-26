using Microsoft.EntityFrameworkCore;
using RiftboundCalendar.Core.Entities;
using RiftboundCalendar.Core.Interfaces;

namespace RiftboundCalendar.Infrastructure.Persistence;

public sealed class StatusHistoryRepository(IDbContextFactory<RiftboundDbContext> factory)
    : IStatusHistoryRepository
{
    public async Task AppendAsync(IReadOnlyList<EventStatusHistoryEntry> entries, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        db.StatusHistory.AddRange(entries);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EventStatusHistoryEntry>> GetByEventIdAsync(
        string eventId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.StatusHistory
            .AsNoTracking()
            .Where(e => e.EventId == eventId)
            .OrderByDescending(e => e.ChangedAt)
            .ToListAsync(ct);
    }

    public async Task DeleteExpiredAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.StatusHistory
            .Where(e => e.EventEndDate < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
