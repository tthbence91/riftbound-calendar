using FluentAssertions;
using RiftboundCalendar.Infrastructure.Configuration;

namespace RiftboundCalendar.Infrastructure.Tests.Configuration;

public class RiftboundOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new RiftboundOptions();

        options.BaseUrl.Should().Be("https://locator.riftbound.uvsgames.com");
        options.RefreshIntervalMinutes.Should().Be(30);
        options.BudapestLatitude.Should().BeApproximately(47.4979, precision: 0.0001);
        options.BudapestLongitude.Should().BeApproximately(19.0402, precision: 0.0001);
        options.RadiusKm.Should().BeApproximately(50.0, precision: 0.001);
    }
}
