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
}

app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

app.Run();

public partial class Program { }
