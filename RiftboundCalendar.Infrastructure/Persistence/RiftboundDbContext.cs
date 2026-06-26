using Microsoft.EntityFrameworkCore;
using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Infrastructure.Persistence;

public sealed class RiftboundDbContext(DbContextOptions<RiftboundDbContext> options) : DbContext(options)
{
    public DbSet<NotificationStateEntry> NotificationStates => Set<NotificationStateEntry>();
    public DbSet<EventStatusHistoryEntry> StatusHistory => Set<EventStatusHistoryEntry>();

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

        modelBuilder.Entity<EventStatusHistoryEntry>(e =>
        {
            e.ToTable("event_status_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityColumn();
            e.Property(x => x.EventId).HasColumnName("event_id");
            e.Property(x => x.EventEndDate).HasColumnName("event_end_date");
            e.Property(x => x.OldStatus).HasColumnName("old_status").HasConversion<string>();
            e.Property(x => x.NewStatus).HasColumnName("new_status").HasConversion<string>();
            e.Property(x => x.ChangedAt).HasColumnName("changed_at");
            e.HasIndex(x => x.EventId);
            e.HasIndex(x => x.EventEndDate);
        });
    }
}
