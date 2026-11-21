using System.Text.Json.Serialization;
using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using BusinessApp.Api.Data.Seed;
using BusinessApp.Api.Endpoints;
using BusinessApp.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("BusinessApp") ?? "Data Source=App_Data/businessapp.db"));

builder.Services.AddSingleton(builder.Configuration.GetSection("Seed").Get<SeedOptions>() ?? new SeedOptions());
builder.Services.AddScoped<DataSeeder>();
builder.Services.AddScoped<ScheduleService>();

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "http://127.0.0.1:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Content-Disposition")));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();

await DatabaseInitializer.InitializeAsync(app.Services);

app.MapGet("/", () => Results.Redirect("/api"));
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", time = DateTime.UtcNow }));

var api = app.MapGroup("/api");
api.MapGet("/", ApiIndex.Describe);

api.MapCategoryEndpoints();
api.MapSupplierEndpoints();
api.MapProductEndpoints();
api.MapCustomerEndpoints();
api.MapEmployeeEndpoints();
api.MapShipperEndpoints();
api.MapOrderEndpoints();
api.MapReportEndpoints();
api.MapDashboardEndpoints();
api.MapProjectEndpoints();
api.MapTaskEndpoints();
api.MapResourceEndpoints();
api.MapCampaignEndpoints();
api.MapLeadEndpoints();
api.MapMarketingEndpoints();
api.MapSearchEndpoints();
api.MapAvatarEndpoints();
api.MapAdminEndpoints();

app.Run();

public partial class Program;
