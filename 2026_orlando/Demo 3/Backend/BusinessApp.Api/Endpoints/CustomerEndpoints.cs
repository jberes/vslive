using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public sealed class CustomerDto
{
    public int Id { get; init; }
    public required string Code { get; init; }
    public required string CompanyName { get; init; }
    public string? ContactName { get; init; }
    public string? ContactTitle { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? Region { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string? Phone { get; init; }
    public string? Fax { get; init; }
    public DateTime CreatedAt { get; init; }
    public int OrderCount { get; init; }
    public double TotalSales { get; init; }
    public DateTime? LastOrderDate { get; init; }
    public string AvatarUrl => Avatars.Url("customers", Id);
}

public sealed class CustomerInput
{
    [StringLength(8, MinimumLength = 3)] public string? Code { get; set; }
    [Required, StringLength(128)] public string CompanyName { get; set; } = "";
    [StringLength(64)] public string? ContactName { get; set; }
    [StringLength(64)] public string? ContactTitle { get; set; }
    [EmailAddress, StringLength(128)] public string? Email { get; set; }
    [StringLength(128)] public string? Address { get; set; }
    [StringLength(64)] public string? City { get; set; }
    [StringLength(64)] public string? Region { get; set; }
    [StringLength(16)] public string? PostalCode { get; set; }
    [StringLength(64)] public string? Country { get; set; }
    [StringLength(32)] public string? Phone { get; set; }
    [StringLength(32)] public string? Fax { get; set; }
}

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/customers").WithTags("Customers");

        g.MapGet("/", async ([AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await Project(db.Customers).ToPagedResultAsync(q, Search, "companyName", ct));

        g.MapGet("/lookup", async (string? search, AppDbContext db, CancellationToken ct) =>
        {
            var s = search?.Trim().ToLower();
            return await db.Customers
                .Where(c => s == null || c.CompanyName.ToLower().Contains(s) || c.Code.ToLower().Contains(s))
                .OrderBy(c => c.CompanyName)
                .Select(c => new { c.Id, c.Code, c.CompanyName, c.City, c.Country })
                .ToListAsync(ct);
        });

        g.MapGet("/countries", async (AppDbContext db, CancellationToken ct) =>
            await db.Customers.Where(c => c.Country != null).GroupBy(c => c.Country!)
                .Select(x => new { country = x.Key, customers = x.Count() })
                .OrderByDescending(x => x.customers).ToListAsync(ct));

        g.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
            await Project(db.Customers.Where(c => c.Id == id)).FirstOrDefaultAsync(ct) is { } dto ? Results.Ok(dto) : Results.NotFound());

        g.MapGet("/{id:int}/orders", async (int id, [AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await OrderEndpoints.Project(db.Orders.Where(o => o.CustomerId == id)).ToPagedResultAsync(q, OrderEndpoints.Search, "-orderDate", ct));

        g.MapGet("/{id:int}/dashboard", Dashboard);

        g.MapPost("/", async (CustomerInput input, AppDbContext db, CancellationToken ct) =>
        {
            var code = string.IsNullOrWhiteSpace(input.Code) ? await GenerateCode(input.CompanyName, db, ct) : input.Code.Trim().ToUpperInvariant();
            if (await db.Customers.AnyAsync(c => c.Code == code, ct))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["code"] = [$"Code '{code}' is already in use."] });

            var entity = new Customer { Code = code, CompanyName = input.CompanyName, CreatedAt = DateTime.UtcNow };
            Apply(entity, input);
            db.Customers.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/customers/{entity.Id}", await Project(db.Customers.Where(c => c.Id == entity.Id)).FirstAsync(ct));
        });

        g.MapPut("/{id:int}", async (int id, CustomerInput input, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Customers.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();
            if (!string.IsNullOrWhiteSpace(input.Code))
            {
                var code = input.Code.Trim().ToUpperInvariant();
                if (await db.Customers.AnyAsync(c => c.Code == code && c.Id != id, ct))
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["code"] = [$"Code '{code}' is already in use."] });
                entity.Code = code;
            }
            Apply(entity, input);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await Project(db.Customers.Where(c => c.Id == id)).FirstAsync(ct));
        });

        // Ribbon "Duplicate" action.
        g.MapPost("/{id:int}/duplicate", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var src = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
            if (src is null) return Results.NotFound();
            var copy = new Customer
            {
                Code = await GenerateCode(src.CompanyName, db, ct),
                CompanyName = $"{src.CompanyName} (Copy)",
                ContactName = src.ContactName, ContactTitle = src.ContactTitle, Email = src.Email,
                Address = src.Address, City = src.City, Region = src.Region, PostalCode = src.PostalCode,
                Country = src.Country, Phone = src.Phone, Fax = src.Fax, CreatedAt = DateTime.UtcNow,
            };
            db.Customers.Add(copy);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/customers/{copy.Id}", await Project(db.Customers.Where(c => c.Id == copy.Id)).FirstAsync(ct));
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            if (await db.Orders.AnyAsync(o => o.CustomerId == id, ct))
                return Results.Problem("Customer has orders and cannot be deleted.", statusCode: 409);
            return await db.Customers.Where(c => c.Id == id).ExecuteDeleteAsync(ct) > 0 ? Results.NoContent() : Results.NotFound();
        });
    }

    internal static Expression<Func<CustomerDto, bool>> Search(string s) => c =>
        c.CompanyName.ToLower().Contains(s) || c.Code.ToLower().Contains(s) ||
        (c.ContactName ?? "").ToLower().Contains(s) || (c.City ?? "").ToLower().Contains(s) ||
        (c.Country ?? "").ToLower().Contains(s) || (c.Email ?? "").ToLower().Contains(s) || (c.Phone ?? "").Contains(s);

    internal static IQueryable<CustomerDto> Project(IQueryable<Customer> q) => q.Select(c => new CustomerDto
    {
        Id = c.Id,
        Code = c.Code,
        CompanyName = c.CompanyName,
        ContactName = c.ContactName,
        ContactTitle = c.ContactTitle,
        Email = c.Email,
        Address = c.Address,
        City = c.City,
        Region = c.Region,
        PostalCode = c.PostalCode,
        Country = c.Country,
        Phone = c.Phone,
        Fax = c.Fax,
        CreatedAt = c.CreatedAt,
        OrderCount = c.Orders.Count,
        TotalSales = Math.Round(c.Orders.SelectMany(o => o.Details).Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)), 2),
        LastOrderDate = c.Orders.Max(o => (DateTime?)o.OrderDate),
    });

    private static void Apply(Customer e, CustomerInput i)
    {
        e.CompanyName = i.CompanyName.Trim();
        e.ContactName = i.ContactName;
        e.ContactTitle = i.ContactTitle;
        e.Email = i.Email;
        e.Address = i.Address;
        e.City = i.City;
        e.Region = i.Region;
        e.PostalCode = i.PostalCode;
        e.Country = i.Country;
        e.Phone = i.Phone;
        e.Fax = i.Fax;
    }

    internal static async Task<string> GenerateCode(string companyName, AppDbContext db, CancellationToken ct)
    {
        var letters = new string(companyName.ToUpperInvariant().Where(char.IsAsciiLetterUpper).ToArray());
        var prefix = (letters + "XXXXX")[..5];
        var stem = prefix[..3];
        var existing = (await db.Customers.Where(c => c.Code.StartsWith(stem)).Select(c => c.Code).ToListAsync(ct)).ToHashSet();
        if (!existing.Contains(prefix)) return prefix;
        for (var i = 1; ; i++)
        {
            var candidate = prefix[..(5 - i.ToString().Length)] + i;
            if (!existing.Contains(candidate)) return candidate;
        }
    }

    /// <summary>"Customer Dashboard": what products/categories matter most to the selected customer.</summary>
    private static async Task<IResult> Dashboard(int id, AppDbContext db, CancellationToken ct)
    {
        var customer = await Project(db.Customers.Where(c => c.Id == id)).FirstOrDefaultAsync(ct);
        if (customer is null) return Results.NotFound();

        var lines = db.OrderDetails.Where(d => d.Order!.CustomerId == id);

        var topProducts = await lines.GroupBy(d => new { d.ProductId, d.Product!.Name, Category = d.Product.Category!.Name })
            .Select(g => new
            {
                g.Key.ProductId, g.Key.Name, g.Key.Category,
                Units = g.Sum(d => d.Quantity),
                Orders = g.Count(),
                Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)),
            })
            .OrderByDescending(x => x.Sales).Take(10).ToListAsync(ct);

        var byCategory = await lines.GroupBy(d => d.Product!.Category!.Name)
            .Select(g => new { g.Key, Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)) })
            .OrderByDescending(x => x.Sales).ToListAsync(ct);

        var byYearCategory = await lines.GroupBy(d => new { d.Order!.OrderDate.Year, Category = d.Product!.Category!.Name })
            .Select(g => new { g.Key.Year, g.Key.Category, Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)) })
            .ToListAsync(ct);

        var recentOrders = await OrderEndpoints.Project(db.Orders.Where(o => o.CustomerId == id))
            .OrderByDescending(o => o.OrderDate).Take(10).ToListAsync(ct);

        var years = byYearCategory.Select(x => x.Year).Distinct().Order().Select(y => y.ToString()).ToList();
        var topCats = byCategory.Take(3).Select(c => c.Key).ToList();
        var avgOrder = customer.OrderCount == 0 ? 0 : customer.TotalSales / customer.OrderCount;

        return Results.Ok(new
        {
            customer,
            kpis = new
            {
                totalSales = customer.TotalSales,
                orderCount = customer.OrderCount,
                averageOrderValue = Money.Round(avgOrder),
                firstOrderDate = await db.Orders.Where(o => o.CustomerId == id).MinAsync(o => (DateTime?)o.OrderDate, ct),
                lastOrderDate = customer.LastOrderDate,
                openOrders = await db.Orders.CountAsync(o => o.CustomerId == id && o.ShippedDate == null, ct),
            },
            topProducts = topProducts.Select(p => new { productId = p.ProductId, name = p.Name, category = p.Category, units = p.Units, orders = p.Orders, sales = Money.Round(p.Sales) }),
            salesByCategory = byCategory.Select(c => new NameValue(c.Key, Money.Round(c.Sales))),
            topCategoriesByYear = ChartData.From(years, topCats, byYearCategory.Where(x => topCats.Contains(x.Category)),
                x => x.Year.ToString(), x => x.Category, x => x.Sales),
            recentOrders,
        });
    }
}
