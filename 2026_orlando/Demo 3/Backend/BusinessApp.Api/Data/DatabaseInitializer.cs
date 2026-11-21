using BusinessApp.Api.Data.Seed;
using BusinessApp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, bool recreate = false, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (recreate) await db.Database.EnsureDeletedAsync(ct);

        var dataSource = db.Database.GetDbConnection().DataSource;
        if (!string.IsNullOrEmpty(dataSource) && Path.GetDirectoryName(Path.GetFullPath(dataSource)) is { } dir)
            Directory.CreateDirectory(dir);

        await db.Database.MigrateAsync(ct);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);

        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        if (!await seeder.SeedAsync(ct)) return;

        // Roll up summary tasks (dates, duration, % complete) for every seeded project.
        var scheduler = scope.ServiceProvider.GetRequiredService<ScheduleService>();
        foreach (var projectId in await db.Projects.Select(p => p.Id).ToListAsync(ct))
            await scheduler.RecalculateAsync(projectId, ct);
    }
}
