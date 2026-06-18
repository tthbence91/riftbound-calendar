using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Core.Interfaces;

public interface IEventRepository
{
    Task<IReadOnlyList<RiftboundEvent>> GetEventsAsync(CancellationToken cancellationToken = default);
}
