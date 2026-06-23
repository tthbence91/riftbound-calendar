using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Api.Dtos;

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

    public static RiftboundEventDto FromDomain(RiftboundEvent e) =>
        new()
        {
            Id = e.Id,
            Title = e.Info.Title,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            LocationName = e.Location.Name,
            Latitude = e.Location.Latitude,
            Longitude = e.Location.Longitude,
            Format = e.Info.Format,
            Url = e.Info.Url.ToString()
        };
}
