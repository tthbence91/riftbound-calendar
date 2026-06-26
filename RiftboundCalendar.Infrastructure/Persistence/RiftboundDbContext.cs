using Microsoft.EntityFrameworkCore;
using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Infrastructure.Persistence;

public sealed class RiftboundDbContext(DbContextOptions<RiftboundDbContext> options) : DbContext(options)
{
    public DbSet<NotificationStateEntry> NotificationStates => Set<NotificationStateEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationStateEntry>(e =>
        {
            e.ToTable("notification_states");
            e.HasKey(x => x.EventId);
            e.Property(x => x.EventId).HasColumnName("event_id");
            e.Property(x => x.LastStatus).HasColumnName("last_status").HasConversion<string>();
            e.Property(x => x.FirstSeenAt).HasColumnName("first_seen_at");
            e.Property(x => x.LastUpdatedAt).HasColumnName("last_updated_at");
        });
    }
}
