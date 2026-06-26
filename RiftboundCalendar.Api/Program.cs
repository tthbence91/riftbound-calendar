using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using RiftboundCalendar.Core.Interfaces;
using RiftboundCalendar.Infrastructure.BackgroundServices;
using RiftboundCalendar.Infrastructure.Caching;
using RiftboundCalendar.Infrastructure.Configuration;
using RiftboundCalendar.Infrastructure.Fetching;
using RiftboundCalendar.Infrastructure.Notifications;
using RiftboundCalendar.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RiftboundOptions>(
    builder.Configuration.GetSection(RiftboundOptions.SectionName));
builder.Services.Configure<DiscordOptions>(
    builder.Configuration.GetSection(DiscordOptions.SectionName));
builder.Services.AddSingleton<IRetryPolicy, DiscordRetryPolicy>();
builder.Services.AddSingleton<IEventNotifier, DiscordNotifier>();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<RiftboundDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddSingleton<INotificationStateRepository, NotificationStateRepository>();

var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<StartupReadiness>();
builder.Services.AddSingleton<EventRefreshObservers>();
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

if (!string.IsNullOrEmpty(connectionString))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<RiftboundDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection();
    app.MapPost("/api/debug/notify-discord", async (
        IEventRepository repo,
        IEventNotifier notifier,
        CancellationToken ct) =>
    {
        var events = await repo.GetEventsAsync(ct);
        if (events.Count == 0)
            return Results.Ok(new { sent = 0, message = "No cached events." });
        await notifier.NotifyNewEventsAsync(events, ct);
        return Results.Ok(new { sent = events.Count });
    });
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

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
