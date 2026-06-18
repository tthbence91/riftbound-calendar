using FluentAssertions;
using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Core.Tests.Entities;

public class EventInfoTests
{
    private static readonly Uri ValidUrl = new("https://locator.riftbound.uvsgames.com/events/1");

    [Fact]
    public void EventInfo_StoresValues()
    {
        var info = new EventInfo("Summoner Skirmish", "Constructed", ValidUrl);

        info.Title.Should().Be("Summoner Skirmish");
        info.Format.Should().Be("Constructed");
        info.Url.Should().Be(ValidUrl);
    }

    [Fact]
    public void EventInfo_ValueEquality()
    {
        var a = new EventInfo("Summoner Skirmish", "Constructed", ValidUrl);
        var b = new EventInfo("Summoner Skirmish", "Constructed", ValidUrl);

        a.Should().Be(b);
    }
}
