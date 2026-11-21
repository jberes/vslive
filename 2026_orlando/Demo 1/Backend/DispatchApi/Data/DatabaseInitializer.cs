using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DispatchApi.Data;

/// <summary>
/// Startup tasks: verify the database exists and shift every date so the seed "anchor" day becomes today.
/// Shifting is by whole days, so time-of-day relationships (shifts, windows, SLA due times) are preserved.
/// </summary>
public static class DatabaseInitializer
{
    private static readonly (string Table, string Column)[] DateTimeColumns =
    [
        ("Technicians", "LastLocationAt"), ("TimeOffRequests", "RequestedAt"), ("Customers", "CreatedAt"),
        ("Jobs", "CreatedAt"), ("Jobs", "WindowStart"), ("Jobs", "WindowEnd"), ("Jobs", "ScheduledStart"), ("Jobs", "ScheduledEnd"),
        ("Jobs", "ResponseDueAt"), ("Jobs", "ResolutionDueAt"), ("Jobs", "AssignedAt"), ("Jobs", "ActualStart"), ("Jobs", "ActualEnd"),
        ("Jobs", "UpdatedAt"), ("JobEvents", "OccurredAt"), ("JobNotes", "CreatedAt"), ("JobParts", "AddedAt"),
        ("Reschedules", "FromStart"), ("Reschedules", "ToStart"), ("Reschedules", "CreatedAt"),
    ];

    private static readonly (string Table, string Column)[] DateColumns =
    [
        ("Technicians", "HireDate"), ("TechnicianSkills", "CertifiedUntil"), ("Shifts", "ShiftDate"),
        ("TimeOffRequests", "StartDate"), ("TimeOffRequests", "EndDate"), ("Assets", "InstallDate"), ("Assets", "WarrantyExpires"),
        ("SeedMeta", "AnchorDate"),
    ];

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DispatchDbContext>();

        var conn = (SqliteConnection)db.Database.GetDbConnection();
        var path = new SqliteConnectionStringBuilder(conn.ConnectionString).DataSource;
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"SQLite database not found at '{Path.GetFullPath(path)}'. Run 'python3 Backend/database/generate_db.py' first.");

        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;");

        if (!config.GetValue("Database:RebaseDatesToToday", true)) return;

        var meta = await db.SeedMeta.AsNoTracking().SingleAsync();
        var offset = DateOnly.FromDateTime(DateTime.Today).DayNumber - meta.AnchorDate.DayNumber;
        if (offset == 0) return;

        logger.LogInformation("Rebasing all dates by {Days} day(s) so seed anchor {Anchor} becomes today", offset, meta.AnchorDate);
        var modifier = $"{(offset >= 0 ? "+" : "")}{offset} days";
        // Table/column names come from the constant lists above and the modifier is an integer - no user input reaches this SQL.
#pragma warning disable EF1002
        await using var tx = await db.Database.BeginTransactionAsync();
        foreach (var (t, c) in DateTimeColumns)
            await db.Database.ExecuteSqlRawAsync($"UPDATE \"{t}\" SET \"{c}\" = datetime(\"{c}\", '{modifier}') WHERE \"{c}\" IS NOT NULL");
        // Two passes: SQLite checks UNIQUE constraints row by row (e.g. Shifts(TechnicianId, ShiftDate, StartTime)), so shifting
        // consecutive days in place collides. Prefix first, then strip the prefix.
        foreach (var (t, c) in DateColumns)
            await db.Database.ExecuteSqlRawAsync($"UPDATE \"{t}\" SET \"{c}\" = '~' || date(\"{c}\", '{modifier}') WHERE \"{c}\" IS NOT NULL");
        foreach (var (t, c) in DateColumns)
            await db.Database.ExecuteSqlRawAsync($"UPDATE \"{t}\" SET \"{c}\" = substr(\"{c}\", 2) WHERE \"{c}\" LIKE '~%'");
#pragma warning restore EF1002
        await tx.CommitAsync();
        logger.LogInformation("Date rebase complete");
    }
}
