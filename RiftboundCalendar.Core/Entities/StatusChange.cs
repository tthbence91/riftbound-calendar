namespace RiftboundCalendar.Core.Entities;

public sealed record StatusChange(RiftboundEvent Event, RegistrationStatus OldStatus, RegistrationStatus NewStatus);
