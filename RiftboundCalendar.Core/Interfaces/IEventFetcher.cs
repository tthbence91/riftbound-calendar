using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Core.Interfaces;

public interface IEventFetcher
{
    Task<IReadOnlyList<RiftboundEvent>> FetchAllEventsAsync(CancellationToken cancellationToken = default);
}
