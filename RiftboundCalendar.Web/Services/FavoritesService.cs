using System.Text.Json;
using Microsoft.JSInterop;

namespace RiftboundCalendar.Web.Services;

public sealed record FavoriteLocation(string StoreId, string StoreName);

public sealed class FavoritesService(IJSRuntime js)
{
    private const string StorageKey = "riftbound_favorites";
    private List<FavoriteLocation>? _cache;

    public async Task<bool> IsFavoriteAsync(string storeId)
    {
        var all = await GetAllAsync();
        return all.Any(f => f.StoreId == storeId);
    }

    public async Task ToggleAsync(string storeId, string storeName)
    {
        var all = await GetAllAsync();
        var existing = all.FirstOrDefault(f => f.StoreId == storeId);
        if (existing is not null)
            all.Remove(existing);
        else
            all.Add(new FavoriteLocation(storeId, storeName));
        await SaveAsync(all);
    }

    public async Task RemoveAsync(string storeId)
    {
        var all = await GetAllAsync();
        all.RemoveAll(f => f.StoreId == storeId);
        await SaveAsync(all);
    }

    public async Task<List<FavoriteLocation>> GetAllAsync()
    {
        if (_cache is not null) return _cache;
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            _cache = json is null
                ? []
                : JsonSerializer.Deserialize<List<FavoriteLocation>>(json) ?? [];
        }
        catch
        {
            _cache = [];
        }
        return _cache;
    }

    private async Task SaveAsync(List<FavoriteLocation> favorites)
    {
        _cache = favorites;
        var json = JsonSerializer.Serialize(favorites);
        await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }
}
