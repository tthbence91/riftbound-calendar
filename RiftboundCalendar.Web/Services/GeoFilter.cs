namespace RiftboundCalendar.Web.Services;

public static class GeoFilter
{
    private const double EarthRadiusKm = 6371.0;

    public static List<RiftboundCalendarItem> Apply(
        IEnumerable<RiftboundCalendarItem> items,
        double centerLat,
        double centerLng,
        double radiusKm)
    {
        return items
            .Where(i => Distance(centerLat, centerLng, i.Event.Latitude, i.Event.Longitude) <= radiusKm)
            .ToList();
    }

    private static double Distance(double lat1, double lng1, double lat2, double lng2)
    {
        var phi1 = lat1 * Math.PI / 180.0;
        var phi2 = lat2 * Math.PI / 180.0;
        var dPhiHalf = (lat2 - lat1) * Math.PI / 360.0;
        var dLambdaHalf = (lng2 - lng1) * Math.PI / 360.0;

        var a = Math.Sin(dPhiHalf) * Math.Sin(dPhiHalf)
              + Math.Cos(phi1) * Math.Cos(phi2)
              * Math.Sin(dLambdaHalf) * Math.Sin(dLambdaHalf);

        return EarthRadiusKm * 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
    }
}
