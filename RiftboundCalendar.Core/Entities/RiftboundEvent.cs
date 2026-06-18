namespace RiftboundCalendar.Core.Entities;

public sealed record RiftboundEvent
{
    public string Id { get; }
    public string Title { get; }
    public DateTimeOffset StartDate { get; }
    public DateTimeOffset EndDate { get; }
    public string LocationName { get; }
    public double Latitude { get; }
    public double Longitude { get; }
    public string Format { get; }
    public Uri Url { get; }

    public RiftboundEvent(
        string id,
        string title,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string locationName,
        double latitude,
        double longitude,
        string format,
        Uri url)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        if (endDate < startDate)
            throw new ArgumentException("End date cannot be before start date.", nameof(endDate));

        Id = id;
        Title = title;
        StartDate = startDate;
        EndDate = endDate;
        LocationName = locationName;
        Latitude = latitude;
        Longitude = longitude;
        Format = format;
        Url = url;
    }
}
