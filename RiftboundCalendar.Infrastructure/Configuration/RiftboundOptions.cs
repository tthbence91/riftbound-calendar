namespace RiftboundCalendar.Infrastructure.Configuration;

public sealed class RiftboundOptions
{
    public const string SectionName = "Riftbound";

    public string BaseUrl { get; set; } = "https://locator.riftbound.uvsgames.com";
    public int RefreshIntervalMinutes { get; set; } = 30;
    public double BudapestLatitude { get; set; } = 47.4979;
    public double BudapestLongitude { get; set; } = 19.0402;
    public double RadiusKm { get; set; } = 50.0;
}
