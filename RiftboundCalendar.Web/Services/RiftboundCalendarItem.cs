using Heron.MudCalendar;

namespace RiftboundCalendar.Web.Services;

public sealed class RiftboundCalendarItem : CalendarItem
{
    public RiftboundEventDto Event { get; init; } = null!;
}
