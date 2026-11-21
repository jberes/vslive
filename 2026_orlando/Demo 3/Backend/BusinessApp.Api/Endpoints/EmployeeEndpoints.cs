using System.ComponentModel.DataAnnotations;
using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public sealed class EmployeeDto
{
    public int Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string FullName => $"{FirstName} {LastName}";
    public string? Title { get; init; }
    public string? TitleOfCourtesy { get; init; }
    public DateTime BirthDate { get; init; }
    public DateTime HireDate { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? Region { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? HomePhone { get; init; }
    public string? Extension { get; init; }
    public string? Notes { get; init; }
    public int? ReportsToId { get; init; }
    public string? ReportsToName { get; init; }
    public int OrderCount { get; init; }
    public double TotalSales { get; init; }
    public string AvatarUrl => Avatars.Url("employees", Id);
}

public sealed class EmployeeInput
{
    [Required, StringLength(64)] public string FirstName { get; set; } = "";
    [Required, StringLength(64)] public string LastName { get; set; } = "";
    [StringLength(64)] public string? Title { get; set; }
    [StringLength(16)] public string? TitleOfCourtesy { get; set; }
    public DateTime BirthDate { get; set; }
    public DateTime HireDate { get; set; }
    [EmailAddress] public string? Email { get; set; }
    [StringLength(128)] public string? Address { get; set; }
    [StringLength(64)] public string? City { get; set; }
    [StringLength(64)] public string? Region { get; set; }
    [StringLength(16)] public string? PostalCode { get; set; }
    [StringLength(64)] public string? Country { get; set; }
    [StringLength(32)] public string? HomePhone { get; set; }
    [StringLength(8)] public string? Extension { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
    public int? ReportsToId { get; set; }
}

public static class EmployeeEndpoints
{
    public static void MapEmployeeEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/employees").WithTags("Employees");

        g.MapGet("/", async ([AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await Project(db.Employees).ToPagedResultAsync(q, s => e =>
                e.FirstName.ToLower().Contains(s) || e.LastName.ToLower().Contains(s) ||
                (e.Title ?? "").ToLower().Contains(s) || (e.City ?? "").ToLower().Contains(s), "lastName", ct));

        g.MapGet("/lookup", async (AppDbContext db, CancellationToken ct) =>
            await db.Employees.OrderBy(e => e.LastName)
                .Select(e => new { e.Id, Name = e.FirstName + " " + e.LastName, e.LastName, e.Title }).ToListAsync(ct));

        g.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
            await Project(db.Employees.Where(e => e.Id == id)).FirstOrDefaultAsync(ct) is { } dto ? Results.Ok(dto) : Results.NotFound());

        g.MapGet("/{id:int}/orders", async (int id, [AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await OrderEndpoints.Project(db.Orders.Where(o => o.EmployeeId == id)).ToPagedResultAsync(q, OrderEndpoints.Search, "-orderDate", ct));

        g.MapGet("/{id:int}/dashboard", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var employee = await Project(db.Employees.Where(e => e.Id == id)).FirstOrDefaultAsync(ct);
            if (employee is null) return Results.NotFound();
            var lines = db.OrderDetails.Where(d => d.Order!.EmployeeId == id);
            var byYear = await lines.GroupBy(d => d.Order!.OrderDate.Year)
                .Select(g => new { Year = g.Key, Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)) })
                .OrderBy(x => x.Year).ToListAsync(ct);
            var topCustomers = await lines.GroupBy(d => d.Order!.Customer!.CompanyName)
                .Select(g => new { g.Key, Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)) })
                .OrderByDescending(x => x.Sales).Take(10).ToListAsync(ct);
            var byCategory = await lines.GroupBy(d => d.Product!.Category!.Name)
                .Select(g => new { g.Key, Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)) })
                .OrderByDescending(x => x.Sales).ToListAsync(ct);
            var directReports = await db.Employees.Where(e => e.ReportsToId == id)
                .Select(e => new LookupItem(e.Id, e.FirstName + " " + e.LastName)).ToListAsync(ct);
            return Results.Ok(new
            {
                employee,
                directReports,
                salesByYear = byYear.Select(x => new NameValue(x.Year.ToString(), Money.Round(x.Sales))),
                topCustomers = topCustomers.Select(x => new NameValue(x.Key, Money.Round(x.Sales))),
                salesByCategory = byCategory.Select(x => new NameValue(x.Key, Money.Round(x.Sales))),
            });
        });

        g.MapPost("/", async (EmployeeInput input, AppDbContext db, CancellationToken ct) =>
        {
            if (input.ReportsToId is int r && !await db.Employees.AnyAsync(e => e.Id == r, ct))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["reportsToId"] = ["Manager not found."] });
            var entity = new Employee { FirstName = input.FirstName, LastName = input.LastName };
            Apply(entity, input);
            db.Employees.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/employees/{entity.Id}", await Project(db.Employees.Where(e => e.Id == entity.Id)).FirstAsync(ct));
        });

        g.MapPut("/{id:int}", async (int id, EmployeeInput input, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Employees.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();
            if (input.ReportsToId == id)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["reportsToId"] = ["An employee cannot report to themselves."] });
            if (input.ReportsToId is int r && !await db.Employees.AnyAsync(e => e.Id == r, ct))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["reportsToId"] = ["Manager not found."] });
            Apply(entity, input);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await Project(db.Employees.Where(e => e.Id == id)).FirstAsync(ct));
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            if (await db.Orders.AnyAsync(o => o.EmployeeId == id, ct))
                return Results.Problem("Employee has orders and cannot be deleted.", statusCode: 409);
            return await db.Employees.Where(e => e.Id == id).ExecuteDeleteAsync(ct) > 0 ? Results.NoContent() : Results.NotFound();
        });
    }

    private static IQueryable<EmployeeDto> Project(IQueryable<Employee> q) => q.Select(e => new EmployeeDto
    {
        Id = e.Id,
        FirstName = e.FirstName,
        LastName = e.LastName,
        Title = e.Title,
        TitleOfCourtesy = e.TitleOfCourtesy,
        BirthDate = e.BirthDate,
        HireDate = e.HireDate,
        Email = e.Email,
        Address = e.Address,
        City = e.City,
        Region = e.Region,
        PostalCode = e.PostalCode,
        Country = e.Country,
        HomePhone = e.HomePhone,
        Extension = e.Extension,
        Notes = e.Notes,
        ReportsToId = e.ReportsToId,
        ReportsToName = e.ReportsTo == null ? null : e.ReportsTo.FirstName + " " + e.ReportsTo.LastName,
        OrderCount = e.Orders.Count,
        TotalSales = Math.Round(e.Orders.SelectMany(o => o.Details).Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)), 2),
    });

    private static void Apply(Employee e, EmployeeInput i)
    {
        e.FirstName = i.FirstName.Trim();
        e.LastName = i.LastName.Trim();
        e.Title = i.Title;
        e.TitleOfCourtesy = i.TitleOfCourtesy;
        e.BirthDate = i.BirthDate;
        e.HireDate = i.HireDate;
        e.Email = i.Email;
        e.Address = i.Address;
        e.City = i.City;
        e.Region = i.Region;
        e.PostalCode = i.PostalCode;
        e.Country = i.Country;
        e.HomePhone = i.HomePhone;
        e.Extension = i.Extension;
        e.Notes = i.Notes;
        e.ReportsToId = i.ReportsToId;
    }
}
