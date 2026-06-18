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
    client.DefaultRequestHeaders.UserAgent.ParseAdd("RiftboundCalendar/1.0"));
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
