using FluentAssertions;
using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Core.Tests.Entities;

public class RiftboundEventTests
{
    private static readonly DateTimeOffset ValidStart = new(2025, 10, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ValidEnd = new(2025, 10, 1, 18, 0, 0, TimeSpan.Zero);
    private static readonly Uri ValidUrl = new("https://locator.riftbound.uvsgames.com/events/1");

    [Fact]
    public void ValidEvent_CreatesSuccessfully()
    {
        var ev = new RiftboundEvent("abc123", "Summoner Skirmish", ValidStart, ValidEnd,
            "Budapest", 47.4979, 19.0402, "Constructed", ValidUrl);

        ev.Id.Should().Be("abc123");
        ev.Title.Should().Be("Summoner Skirmish");
        ev.StartDate.Should().Be(ValidStart);
        ev.EndDate.Should().Be(ValidEnd);
        ev.LocationName.Should().Be("Budapest");
        ev.Latitude.Should().Be(47.4979);
        ev.Longitude.Should().Be(19.0402);
        ev.Format.Should().Be("Constructed");
        ev.Url.Should().Be(ValidUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceId_ThrowsArgumentException(string id)
    {
        var act = () => new RiftboundEvent(id, "Title", ValidStart, ValidEnd,
            "Budapest", 47.4979, 19.0402, "Constructed", ValidUrl);

        act.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact]
    public void EndBeforeStart_ThrowsArgumentException()
    {
        var act = () => new RiftboundEvent("id1", "Title", ValidEnd, ValidStart,
            "Budapest", 47.4979, 19.0402, "Constructed", ValidUrl);

        act.Should().Throw<ArgumentException>().WithParameterName("endDate");
    }

    [Fact]
    public void EqualStartAndEnd_CreatesSuccessfully()
    {
        var act = () => new RiftboundEvent("id1", "Title", ValidStart, ValidStart,
            "Budapest", 47.4979, 19.0402, "Constructed", ValidUrl);

        act.Should().NotThrow();
    }
}
