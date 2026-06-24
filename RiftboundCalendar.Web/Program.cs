using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using RiftboundCalendar.Web;
using RiftboundCalendar.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("hu-HU");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("hu-HU");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"];
var baseAddress = string.IsNullOrEmpty(apiBaseUrl)
    ? builder.HostEnvironment.BaseAddress
    : apiBaseUrl;
builder.Services.AddHttpClient<EventApiClient>(client =>
    client.BaseAddress = new Uri(baseAddress));

builder.Services.AddMudServices();
builder.Services.AddSingleton<FavoritesService>();
builder.Services.AddSingleton<LocationPreferenceService>();

await builder.Build().RunAsync();
