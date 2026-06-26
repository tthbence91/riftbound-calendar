using FluentAssertions;
using RiftboundCalendar.Core.Entities;

namespace RiftboundCalendar.Core.Tests.Entities;

public class EventStatsTests
{
    [Fact]
    public void GetRegistrationStatus_WhenLifecycleOpen_ReturnsOpen()
    {
        var stats = new EventStats { LifecycleStatus = "REGISTRATION_OPEN", Capacity = 32, RegisteredCount = 10 };
        stats.GetRegistrationStatus().Should().Be(RegistrationStatus.Open);
    }

    [Fact]
    public void GetRegistrationStatus_WhenRegisteredEqualsCapacity_ReturnsFull()
    {
        var stats = new EventStats { LifecycleStatus = "REGISTRATION_OPEN", Capacity = 32, RegisteredCount = 32 };
        stats.GetRegistrationStatus().Should().Be(RegistrationStatus.Full);
    }

    [Fact]
    public void GetRegistrationStatus_WhenRegisteredExceedsCapacity_ReturnsFull()
    {
        var stats = new EventStats { LifecycleStatus = "REGISTRATION_OPEN", Capacity = 32, RegisteredCount = 33 };
        stats.GetRegistrationStatus().Should().Be(RegistrationStatus.Full);
    }

    [Fact]
    public void GetRegistrationStatus_WhenFullTakesPriorityOverOpen()
    {
        var stats = new EventStats { LifecycleStatus = "REGISTRATION_OPEN", Capacity = 1, RegisteredCount = 1 };
        stats.GetRegistrationStatus().Should().Be(RegistrationStatus.Full);
    }

    [Fact]
    public void GetRegistrationStatus_WhenLifecycleNotOpen_ReturnsClosed()
    {
        var stats = new EventStats { LifecycleStatus = "REGISTRATION_CLOSED" };
        stats.GetRegistrationStatus().Should().Be(RegistrationStatus.Closed);
    }

    [Fact]
    public void GetRegistrationStatus_WhenEmpty_ReturnsClosed()
    {
        EventStats.Empty.GetRegistrationStatus().Should().Be(RegistrationStatus.Closed);
    }
}
