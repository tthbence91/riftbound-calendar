namespace RiftboundCalendar.Web.Services;

public sealed class RiftboundEventDto
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public DateTimeOffset StartDate { get; init; }
    public DateTimeOffset EndDate { get; init; }
    public string LocationName { get; init; } = "";
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string Format { get; init; } = "";
    public string Url { get; init; } = "";
    public string? StoreId { get; init; }
    public int? CostInCents { get; init; }
    public string? Currency { get; init; }
    public int? Capacity { get; init; }
    public int? RegisteredCount { get; init; }
}
