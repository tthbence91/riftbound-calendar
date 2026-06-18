using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Api.Dtos;

public sealed record RiftboundEventDto(
    string Id,
    string Title,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string LocationName,
    string Format,
    string Url)
{
    public static RiftboundEventDto FromDomain(RiftboundEvent e) =>
        new(
            Id: e.Id,
            Title: e.Info.Title,
            StartDate: e.StartDate,
            EndDate: e.EndDate,
            LocationName: e.Location.Name,
            Format: e.Info.Format,
            Url: e.Info.Url.ToString());
}
