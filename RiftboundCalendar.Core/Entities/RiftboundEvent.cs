namespace RiftboundCalendar.Core.Entities;

public sealed record RiftboundEvent
{
    public string Id { get; }
    public DateTimeOffset StartDate { get; }
    public DateTimeOffset EndDate { get; }
    public EventLocation Location { get; }
    public EventInfo Info { get; }

    public RiftboundEvent(
        string id,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        EventLocation location,
        EventInfo info)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (endDate < startDate)
            throw new ArgumentException("End date cannot be before start date.", nameof(endDate));

        Id = id;
        StartDate = startDate;
        EndDate = endDate;
        Location = location;
        Info = info;
    }
}
