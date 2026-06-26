using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Infrastructure.Notifications;

public sealed record StatusChange(RiftboundEvent Event, RegistrationStatus OldStatus, RegistrationStatus NewStatus);
