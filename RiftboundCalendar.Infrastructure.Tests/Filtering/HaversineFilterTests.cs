using FluentAssertions;
using RiftboundCalendar.Core.Entities;
using RiftboundCalendar.Infrastructure.Filtering;

namespace RiftboundCalendar.Infrastructure.Tests.Filtering;

public class HaversineFilterTests
{
    private const double BudapestLat = 47.4979;
    private const double BudapestLng = 19.0402;

    // Budaörs: ~11km from Budapest — well within 50km
    private const double BudaorsLat = 47.4600;
    private const double BudaorsLng = 18.9283;

    // Győr: ~116km from Budapest — well outside 50km
    private const double GyorLat = 47.6875;
    private const double GyorLng = 17.6504;

    [Fact]
    public void BudapestToBudaors_IsWithin50km()
    {
        var events = new[] { CreateEvent(BudaorsLat, BudaorsLng) };

        var result = HaversineFilter.Filter(events, BudapestLat, BudapestLng, radiusKm: 50.0);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void BudapestToGyor_IsOutside50km()
    {
        var events = new[] { CreateEvent(GyorLat, GyorLng) };

        var result = HaversineFilter.Filter(events, BudapestLat, BudapestLng, radiusKm: 50.0);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ExactBoundaryPoint_IsIncluded()
    {
        // Compute the exact Haversine distance from Budapest to Budaörs using the same
        // formula the filter uses. A point at distance d filtered with radius = d must be
        // included — this verifies the boundary condition is <= (not <).
        var exactRadius = HaversineDistance(BudapestLat, BudapestLng, BudaorsLat, BudaorsLng);
        var events = new[] { CreateEvent(BudaorsLat, BudaorsLng) };

        var result = HaversineFilter.Filter(events, BudapestLat, BudapestLng, exactRadius);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void EmptyList_ReturnsEmpty()
    {
        var result = HaversineFilter.Filter([], BudapestLat, BudapestLng, radiusKm: 50.0);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ZeroDistance_SamePoint_IsIncluded()
    {
        var events = new[] { CreateEvent(BudapestLat, BudapestLng) };

        var result = HaversineFilter.Filter(events, BudapestLat, BudapestLng, radiusKm: 50.0);

        result.Should().HaveCount(1);
    }

    private static double HaversineDistance(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusKm = 6371.0;
        var phi1 = lat1 * Math.PI / 180.0;
        var phi2 = lat2 * Math.PI / 180.0;
        var dPhiHalf = (lat2 - lat1) * Math.PI / 360.0;
        var dLambdaHalf = (lng2 - lng1) * Math.PI / 360.0;
        var a = Math.Sin(dPhiHalf) * Math.Sin(dPhiHalf)
              + Math.Cos(phi1) * Math.Cos(phi2)
              * Math.Sin(dLambdaHalf) * Math.Sin(dLambdaHalf);
        return earthRadiusKm * 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
    }

    private static RiftboundEvent CreateEvent(double lat, double lng) =>
        new("test-id", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2),
            new EventLocation("Test Location", lat, lng),
            new EventInfo("Test Event", "Constructed", new Uri("https://example.com")));
}
