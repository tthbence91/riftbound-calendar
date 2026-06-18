using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using RiftboundCalendar.Web;
using RiftboundCalendar.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7219";
builder.Services.AddHttpClient<EventApiClient>(client =>
    client.BaseAddress = new Uri(apiBaseUrl));

await builder.Build().RunAsync();
