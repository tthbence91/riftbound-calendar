using Microsoft.EntityFrameworkCore;
using RiftboundCalendar.Core.Entities;
using RiftboundCalendar.Core.Interfaces;

namespace RiftboundCalendar.Infrastructure.Persistence;

public sealed class NotificationStateRepository(IDbContextFactory<RiftboundDbContext> factory)
    : INotificationStateRepository
{
    public async Task<IReadOnlyDictionary<string, RegistrationStatus>> LoadAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var entries = await db.NotificationStates.AsNoTracking().ToListAsync(ct);
        return entries.ToDictionary(e => e.EventId, e => e.LastStatus);
    }

    public async Task SaveAsync(IReadOnlyDictionary<string, RegistrationStatus> states, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var existingIds = await db.NotificationStates
            .Select(e => e.EventId)
            .ToHashSetAsync(ct);

        foreach (var (eventId, status) in states)
        {
            if (existingIds.Contains(eventId))
            {
                await db.NotificationStates
                    .Where(e => e.EventId == eventId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(e => e.LastStatus, status)
                        .SetProperty(e => e.LastUpdatedAt, now), ct);
            }
            else
            {
                db.NotificationStates.Add(new NotificationStateEntry
                {
                    EventId = eventId,
                    LastStatus = status,
                    FirstSeenAt = now,
                    LastUpdatedAt = now
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
