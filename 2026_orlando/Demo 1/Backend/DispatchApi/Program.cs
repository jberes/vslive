using System.Text.Json.Serialization;
using DispatchApi.Data;
using DispatchApi.Infrastructure;
using DispatchApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- data
var dbPath = builder.Configuration["Database:Path"] ?? "../database/dispatch.db";
if (!Path.IsPathRooted(dbPath)) dbPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, dbPath));
builder.Services.AddDbContext<DispatchDbContext>(o => o.UseSqlite($"Data Source={dbPath};Cache=Shared"));

// ---------------------------------------------------------------- services
var clockOptions = builder.Configuration.GetSection("Clock").Get<ClockOptions>() ?? new ClockOptions();
builder.Services.AddSingleton<IClock>(new DispatchClock(clockOptions));
builder.Services.AddScoped<JobService>();
builder.Services.AddScoped<SchedulingService>();
builder.Services.AddScoped<AnalyticsService>();

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// OpenAPI document (served at /openapi/v1.json) + Swagger UI (/swagger)
builder.Services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info.Title = "Rail Maintenance Dispatch API";
    doc.Info.Version = "v1";
    doc.Info.Description = "Backend for the dispatch console: jobs, technicians, scheduling, SLA risk and dashboards. " +
                           "All times are operations-local (no time zone). See GET /api/meta/clock for the current operations time.";
    return Task.CompletedTask;
}));

// Demo 1 dispatchers (3101-3103) and anything else listed in Cors:Origins
var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (origins.Length == 0 || origins.Contains("*")) p.AllowAnyOrigin();
    else p.SetIsOriginAllowed(origin =>
    {
        if (origins.Contains(origin, StringComparer.OrdinalIgnoreCase)) return true;
        // Claude Design canvases (claude.ai and their sandboxed preview frames) use the Live data toggle.
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps) return false;
        string[] hosts = ["claude.ai", "claude.com", "claudeusercontent.com"];
        return hosts.Any(h => u.Host == h || u.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));
    });
    p.AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Location");
}));

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration, app.Logger);

app.UseExceptionHandler();
app.UseCors();

app.MapOpenApi();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/openapi/v1.json", "Dispatch API v1");
    o.RoutePrefix = "swagger";
    o.DocumentTitle = "Dispatch API - Swagger";
    o.DisplayRequestDuration();
    o.EnableTryItOutByDefault();
});
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).ExcludeFromDescription();

app.MapControllers();
app.Run();
