using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Core.Interfaces;

public interface INotificationStateRepository
{
    Task<IReadOnlyDictionary<string, RegistrationStatus>> LoadAsync(CancellationToken ct);
    Task SaveAsync(IReadOnlyDictionary<string, RegistrationStatus> states, CancellationToken ct);
}
