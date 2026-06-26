namespace RiftboundCalendar.Core.Entities;

public sealed class EventStats
{
    private const string RegistrationOpenStatus = "REGISTRATION_OPEN";

    public static readonly EventStats Empty = new();
    public string? StoreId { get; init; }
    public string? LifecycleStatus { get; init; }
    public int? CostInCents { get; init; }
    public string? Currency { get; init; }
    public int? Capacity { get; init; }
    public int? RegisteredCount { get; init; }

    public RegistrationStatus GetRegistrationStatus()
    {
        if (Capacity.HasValue && RegisteredCount.HasValue && RegisteredCount >= Capacity)
            return RegistrationStatus.Full;
        if (LifecycleStatus == RegistrationOpenStatus)
            return RegistrationStatus.Open;
        return RegistrationStatus.Closed;
    }
}
