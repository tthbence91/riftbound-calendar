namespace RiftboundCalendar.Web.Services;

public enum RegistrationStatus { Open, Full, Closed }

public static class RegistrationStatusHelper
{
    private const string RegistrationOpen = "REGISTRATION_OPEN";

    public static RegistrationStatus Get(RiftboundEventDto evt)
    {
        if (evt.Capacity.HasValue && evt.RegisteredCount.HasValue && evt.RegisteredCount >= evt.Capacity)
            return RegistrationStatus.Full;
        if (evt.LifecycleStatus == RegistrationOpen)
            return RegistrationStatus.Open;
        return RegistrationStatus.Closed;
    }
}
