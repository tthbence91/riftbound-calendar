using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Core.Interfaces;

public interface IEventNotifier
{
    Task NotifyNewEventsAsync(IReadOnlyList<RiftboundEvent> events, CancellationToken cancellationToken);
    Task NotifyStatusChangedAsync(IReadOnlyList<StatusChange> changes, CancellationToken cancellationToken);
}
