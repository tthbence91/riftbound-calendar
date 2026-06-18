namespace RiftboundCalendar.Web.Services;

public sealed record RiftboundEventDto(
    string Id,
    string Title,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string LocationName,
    string Format,
    string Url);
