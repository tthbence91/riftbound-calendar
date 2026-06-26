using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RiftboundCalendar.Infrastructure.Persistence;

public sealed class RiftboundDbContextFactory : IDesignTimeDbContextFactory<RiftboundDbContext>
{
    public RiftboundDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RiftboundDbContext>()
            .UseNpgsql("Host=localhost;Database=riftbound_dev;Username=postgres")
            .Options;
        return new RiftboundDbContext(options);
    }
}
