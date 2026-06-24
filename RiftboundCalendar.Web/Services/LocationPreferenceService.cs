using System.Text.Json;
using Microsoft.JSInterop;

namespace RiftboundCalendar.Web.Services;

public sealed record LocationPreference(double Lat, double Lng, int RadiusKm);

public sealed class LocationPreferenceService(IJSRuntime js)
{
    private const string StorageKey = "riftbound_location";

    public static readonly LocationPreference Default = new(47.4979, 19.0402, 50);

    private LocationPreference? _cache;

    public async Task<LocationPreference> GetAsync()
    {
        if (_cache is not null) return _cache;
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            _cache = json is null
                ? Default
                : JsonSerializer.Deserialize<LocationPreference>(json) ?? Default;
        }
        catch
        {
            _cache = Default;
        }
        return _cache;
    }

    public async Task SaveAsync(LocationPreference preference)
    {
        _cache = preference;
        var json = JsonSerializer.Serialize(preference);
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }
}
