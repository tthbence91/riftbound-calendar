using FluentAssertions;
using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Core.Tests.Entities;

public class EventLocationTests
{
    [Fact]
    public void EventLocation_StoresValues()
    {
        var location = new EventLocation("Budapest", 47.4979, 19.0402);

        location.Name.Should().Be("Budapest");
        location.Latitude.Should().Be(47.4979);
        location.Longitude.Should().Be(19.0402);
    }

    [Fact]
    public void EventLocation_ValueEquality()
    {
        var a = new EventLocation("Budapest", 47.4979, 19.0402);
        var b = new EventLocation("Budapest", 47.4979, 19.0402);

        a.Should().Be(b);
    }
}
