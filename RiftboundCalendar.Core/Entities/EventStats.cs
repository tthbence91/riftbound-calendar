namespace RiftboundCalendar.Core.Entities;

public sealed class EventStats
{
    public static readonly EventStats Empty = new();
    public int? CostInCents { get; init; }
    public string? Currency { get; init; }
    public int? Capacity { get; init; }
    public int? RegisteredCount { get; init; }
}
