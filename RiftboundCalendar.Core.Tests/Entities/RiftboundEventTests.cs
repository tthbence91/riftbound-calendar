using FluentAssertions;
using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Core.Tests.Entities;

public class RiftboundEventTests
{
    private static readonly DateTimeOffset ValidStart = new(2025, 10, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ValidEnd = new(2025, 10, 1, 18, 0, 0, TimeSpan.Zero);
    private static readonly Uri ValidUrl = new("https://locator.riftbound.uvsgames.com/events/1");
    private static readonly EventLocation ValidLocation = new("Budapest", 47.4979, 19.0402);
    private static readonly EventInfo ValidInfo = new("Summoner Skirmish", "Constructed", ValidUrl);

    [Fact]
    public void ValidEvent_CreatesSuccessfully()
    {
        var ev = new RiftboundEvent("abc123", ValidStart, ValidEnd, ValidLocation, ValidInfo);

        ev.Id.Should().Be("abc123");
        ev.StartDate.Should().Be(ValidStart);
        ev.EndDate.Should().Be(ValidEnd);
        ev.Location.Should().Be(ValidLocation);
        ev.Info.Should().Be(ValidInfo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceId_ThrowsArgumentException(string id)
    {
        var act = () => new RiftboundEvent(id, ValidStart, ValidEnd, ValidLocation, ValidInfo);

        act.Should().Throw<ArgumentException>().WithParameterName("id");
    }

    [Fact]
    public void EndBeforeStart_ThrowsArgumentException()
    {
        var act = () => new RiftboundEvent("id1", ValidEnd, ValidStart, ValidLocation, ValidInfo);

        act.Should().Throw<ArgumentException>().WithParameterName("endDate");
    }

    [Fact]
    public void EqualStartAndEnd_CreatesSuccessfully()
    {
        var act = () => new RiftboundEvent("id1", ValidStart, ValidStart, ValidLocation, ValidInfo);

        act.Should().NotThrow();
    }
}
