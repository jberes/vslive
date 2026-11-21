using System.Diagnostics;
using BusinessApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

/// <summary>Development helpers. There is no auth in this API, so don't expose it publicly as-is.</summary>
public static class AdminEndpoints
{
    private static readonly SemaphoreSlim ResetLock = new(1, 1);

    public static void MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/admin").WithTags("Admin");

        g.MapGet("/stats", async (AppDbContext db, CancellationToken ct) => new
        {
            database = Path.GetFullPath(db.Database.GetDbConnection().DataSource),
            counts = new Dictionary<string, int>
            {
                ["categories"] = await db.Categories.CountAsync(ct),
                ["suppliers"] = await db.Suppliers.CountAsync(ct),
                ["products"] = await db.Products.CountAsync(ct),
                ["customers"] = await db.Customers.CountAsync(ct),
                ["employees"] = await db.Employees.CountAsync(ct),
                ["shippers"] = await db.Shippers.CountAsync(ct),
                ["orders"] = await db.Orders.CountAsync(ct),
                ["orderDetails"] = await db.OrderDetails.CountAsync(ct),
                ["projects"] = await db.Projects.CountAsync(ct),
                ["tasks"] = await db.ProjectTasks.CountAsync(ct),
                ["dependencies"] = await db.TaskDependencies.CountAsync(ct),
                ["resources"] = await db.Resources.CountAsync(ct),
                ["assignments"] = await db.TaskAssignments.CountAsync(ct),
                ["campaigns"] = await db.Campaigns.CountAsync(ct),
                ["leads"] = await db.Leads.CountAsync(ct),
                ["leadActivities"] = await db.LeadActivities.CountAsync(ct),
            },
        });

        // Drops the database and regenerates the (deterministic) seed data.
        g.MapPost("/reset-database", async (IServiceProvider services, CancellationToken ct) =>
        {
            if (!await ResetLock.WaitAsync(0, ct)) return Results.Problem("A reset is already running.", statusCode: 409);
            try
            {
                var sw = Stopwatch.StartNew();
                await DatabaseInitializer.InitializeAsync(services, recreate: true, ct);
                return Results.Ok(new { reset = true, elapsedMs = sw.ElapsedMilliseconds });
            }
            finally
            {
                ResetLock.Release();
            }
        });
    }
}

/// <summary>Self-describing catalog of every API route, served at GET /api.</summary>
public static class ApiIndex
{
    public static object Describe(EndpointDataSource endpoints) => new
    {
        name = "BusinessApp API",
        version = "1.0",
        gridQuery = new
        {
            page = "1-based page number (default 1)",
            pageSize = "rows per page, 1-1000 (default 25)",
            sort = "comma separated fields, '-' prefix or ':desc' suffix for descending, e.g. sort=-orderDate,companyName",
            search = "free text search over the grid's main text columns",
            filter = "repeatable field:op:value; ops eq, ne, contains, startswith, endswith, gt, ge, lt, le, in (a|b|c), null, notnull",
        },
        endpoints = endpoints.Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api") == true)
            .Select(e => new
            {
                methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [],
                route = e.RoutePattern.RawText,
                tag = e.Metadata.GetMetadata<Microsoft.AspNetCore.Http.Metadata.ITagsMetadata>()?.Tags.FirstOrDefault(),
            })
            .OrderBy(e => e.tag).ThenBy(e => e.route)
            .ToList(),
    };
}
