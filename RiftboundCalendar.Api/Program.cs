using Microsoft.AspNetCore.StaticFiles;
using RiftboundCalendar.Core.Interfaces;
using RiftboundCalendar.Infrastructure.BackgroundServices;
using RiftboundCalendar.Infrastructure.Caching;
using RiftboundCalendar.Infrastructure.Configuration;
using RiftboundCalendar.Infrastructure.Fetching;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RiftboundOptions>(
    builder.Configuration.GetSection(RiftboundOptions.SectionName));

var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<EventCacheRepository>();
builder.Services.AddSingleton<IEventRepository>(
    sp => sp.GetRequiredService<EventCacheRepository>());

builder.Services.AddHttpClient<RiftboundLocatorFetcher>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Add("Accept-Language", "hu-HU,hu;q=0.9,en;q=0.8");
    client.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
});
builder.Services.AddSingleton<IEventFetcher>(
    sp => sp.GetRequiredService<RiftboundLocatorFetcher>());

builder.Services.AddHostedService<EventRefreshBackgroundService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();
}

var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".dat"] = "application/octet-stream";

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider,
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream",
    OnPrepareResponse = ctx =>
    {
        var path = ctx.File.Name;
        var headers = ctx.Context.Response.Headers;
        if (path == "index.html")
        {
            headers.CacheControl = "no-store, no-cache, must-revalidate";
        }
        else if (path.Contains('.') && !path.EndsWith(".html"))
        {
            // Fingerprinted assets: cache indefinitely
            headers.CacheControl = "public, max-age=31536000, immutable";
        }
    }
});
app.UseCors();
app.MapControllers();

app.MapGet("/api/debug/fetch", async (
    IHttpClientFactory httpClientFactory,
    Microsoft.Extensions.Options.IOptions<RiftboundOptions> opts) =>
{
    var options = opts.Value;
    var numMiles = options.RadiusKm / 1.60934;
    var startDateAfter = DateTime.UtcNow.Date.AddHours(-2).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    var url = $"https://api.cloudflare.riftbound.uvsgames.com/hydraproxy/api/v2/events/" +
        $"?start_date_after={Uri.EscapeDataString(startDateAfter)}" +
        $"&display_statuses=upcoming&display_statuses=inProgress" +
        $"&game_slug=riftbound" +
        $"&latitude={options.BudapestLatitude}&longitude={options.BudapestLongitude}" +
        $"&num_miles={numMiles:F4}" +
        $"&upcoming_only=true&page=1&page_size=5";

    var client = httpClientFactory.CreateClient();
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");

    try
    {
        var response = await client.GetAsync(url);
        var body = await response.Content.ReadAsStringAsync();
        return Results.Ok(new
        {
            requestUrl = url,
            statusCode = (int)response.StatusCode,
            bodyPreview = body.Length > 500 ? body[..500] : body
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new { error = ex.Message, requestUrl = url });
    }
});

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
