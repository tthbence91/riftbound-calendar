using FluentAssertions;
using RiftboundCalendar.Api.Dtos;
using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Api.Tests.Dtos;

public class RiftboundEventDtoTests
{
    [Fact]
    public void FromDomain_MapsAllFieldsCorrectly()
    {
        var startDate = new DateTimeOffset(2026, 7, 1, 14, 0, 0, TimeSpan.Zero);
        var endDate = new DateTimeOffset(2026, 7, 1, 18, 0, 0, TimeSpan.Zero);
        var domain = new RiftboundEvent(
            id: "42",
            startDate: startDate,
            endDate: endDate,
            location: new EventLocation("Test Store", 47.5, 19.05),
            info: new EventInfo("Test Tournament", "Constructed", new Uri("https://example.com/event")));

        var dto = RiftboundEventDto.FromDomain(domain);

        dto.Id.Should().Be("42");
        dto.Title.Should().Be("Test Tournament");
        dto.StartDate.Should().Be(startDate);
        dto.EndDate.Should().Be(endDate);
        dto.LocationName.Should().Be("Test Store");
        dto.Latitude.Should().Be(47.5);
        dto.Longitude.Should().Be(19.05);
        dto.Format.Should().Be("Constructed");
        dto.Url.Should().Be("https://example.com/event");
    }
}
