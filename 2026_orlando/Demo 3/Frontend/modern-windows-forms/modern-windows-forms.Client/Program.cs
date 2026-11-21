using ApexCharts;
using IgniteUI.Blazor.Controls;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using modern_windows_forms.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Ignite UI for Blazor (full trial). Feature modules that nothing requests on render must be listed explicitly.
builder.Services.AddIgniteUIBlazor(typeof(IgbComboModule));

// Blazor-ApexCharts: brand defaults from the ApexCharts design system (chart-1…5, Inter, horizontal gridlines only).
builder.Services.AddApexCharts(e =>
{
    e.GlobalOptions = new ApexChartBaseOptions
    {
        Chart = new Chart { FontFamily = "Inter, -apple-system, 'Segoe UI', sans-serif", Toolbar = new Toolbar { Show = false } },
        Colors = ["#008ffb", "#00e396", "#feb019", "#ff4560", "#775dd0"],
        Grid = new Grid { BorderColor = "#e5e7eb", StrokeDashArray = 0, Xaxis = new GridXAxis { Lines = new Lines { Show = false } } },
        DataLabels = new DataLabels { Enabled = false },
        Legend = new Legend { FontSize = "12px" },
        Stroke = new Stroke { Width = 2 }
    };
});

// BusinessApp Web API (Backend/BusinessApp.Api). Override with "ApiBaseUrl" in wwwroot/appsettings.json.
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5080/";
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(apiBase) });
builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<NotificationService>();

await builder.Build().RunAsync();
