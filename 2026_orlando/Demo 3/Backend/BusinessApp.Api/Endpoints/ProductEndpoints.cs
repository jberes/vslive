using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public sealed class ProductDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public int CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public int SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public string? QuantityPerUnit { get; init; }
    public decimal UnitPrice { get; init; }
    public int UnitsInStock { get; init; }
    public int UnitsOnOrder { get; init; }
    public int ReorderLevel { get; init; }
    public bool Discontinued { get; init; }
    public bool NeedsReorder { get; init; }
    public double InventoryValue { get; init; }
    public int UnitsSold { get; init; }
    public double TotalSales { get; init; }
}

public sealed class ProductInput
{
    [Required, StringLength(128)] public string Name { get; set; } = "";
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
    [Range(1, int.MaxValue)] public int SupplierId { get; set; }
    [StringLength(64)] public string? QuantityPerUnit { get; set; }
    [Range(0, 1_000_000)] public decimal UnitPrice { get; set; }
    [Range(0, 1_000_000)] public int UnitsInStock { get; set; }
    [Range(0, 1_000_000)] public int UnitsOnOrder { get; set; }
    [Range(0, 1_000_000)] public int ReorderLevel { get; set; }
    public bool Discontinued { get; set; }
}

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/products").WithTags("Products");

        g.MapGet("/", async ([AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await Project(db.Products).ToPagedResultAsync(q, Search, "name", ct));

        g.MapGet("/lookup", async (bool? includeDiscontinued, AppDbContext db, CancellationToken ct) =>
            await db.Products.Where(p => includeDiscontinued == true || !p.Discontinued).OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name, p.UnitPrice, p.UnitsInStock, p.CategoryId, p.Discontinued }).ToListAsync(ct));

        g.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
            await Project(db.Products.Where(p => p.Id == id)).FirstOrDefaultAsync(ct) is { } dto ? Results.Ok(dto) : Results.NotFound());

        g.MapGet("/{id:int}/dashboard", Dashboard);

        g.MapPost("/", async (ProductInput input, AppDbContext db, CancellationToken ct) =>
        {
            if (await ValidateRefs(input, db, ct) is { } problem) return problem;
            var entity = new Product { Name = input.Name };
            Apply(entity, input);
            db.Products.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/products/{entity.Id}", await Project(db.Products.Where(p => p.Id == entity.Id)).FirstAsync(ct));
        });

        g.MapPut("/{id:int}", async (int id, ProductInput input, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Products.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();
            if (await ValidateRefs(input, db, ct) is { } problem) return problem;
            Apply(entity, input);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await Project(db.Products.Where(p => p.Id == id)).FirstAsync(ct));
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            if (await db.OrderDetails.AnyAsync(d => d.ProductId == id, ct))
                return Results.Problem("Product appears on existing orders. Mark it as discontinued instead.", statusCode: 409);
            return await db.Products.Where(p => p.Id == id).ExecuteDeleteAsync(ct) > 0 ? Results.NoContent() : Results.NotFound();
        });
    }

    internal static Expression<Func<ProductDto, bool>> Search(string s) => p =>
        p.Name.ToLower().Contains(s) || (p.CategoryName ?? "").ToLower().Contains(s) ||
        (p.SupplierName ?? "").ToLower().Contains(s) || (p.QuantityPerUnit ?? "").ToLower().Contains(s);

    internal static IQueryable<ProductDto> Project(IQueryable<Product> q) => q.Select(p => new ProductDto
    {
        Id = p.Id,
        Name = p.Name,
        CategoryId = p.CategoryId,
        CategoryName = p.Category!.Name,
        SupplierId = p.SupplierId,
        SupplierName = p.Supplier!.CompanyName,
        QuantityPerUnit = p.QuantityPerUnit,
        UnitPrice = p.UnitPrice,
        UnitsInStock = p.UnitsInStock,
        UnitsOnOrder = p.UnitsOnOrder,
        ReorderLevel = p.ReorderLevel,
        Discontinued = p.Discontinued,
        NeedsReorder = !p.Discontinued && p.UnitsInStock + p.UnitsOnOrder <= p.ReorderLevel,
        InventoryValue = Math.Round((double)p.UnitPrice * p.UnitsInStock, 2),
        UnitsSold = p.OrderDetails.Sum(d => d.Quantity),
        TotalSales = Math.Round(p.OrderDetails.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)), 2),
    });

    private static void Apply(Product e, ProductInput i)
    {
        e.Name = i.Name.Trim();
        e.CategoryId = i.CategoryId;
        e.SupplierId = i.SupplierId;
        e.QuantityPerUnit = i.QuantityPerUnit;
        e.UnitPrice = i.UnitPrice;
        e.UnitsInStock = i.UnitsInStock;
        e.UnitsOnOrder = i.UnitsOnOrder;
        e.ReorderLevel = i.ReorderLevel;
        e.Discontinued = i.Discontinued;
    }

    private static async Task<IResult?> ValidateRefs(ProductInput i, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (!await db.Categories.AnyAsync(c => c.Id == i.CategoryId, ct)) errors["categoryId"] = ["Category not found."];
        if (!await db.Suppliers.AnyAsync(s => s.Id == i.SupplierId, ct)) errors["supplierId"] = ["Supplier not found."];
        return errors.Count > 0 ? Results.ValidationProblem(errors) : null;
    }

    /// <summary>"Product Information" charts: compare the selected product with similar products in its category.</summary>
    private static async Task<IResult> Dashboard(int id, AppDbContext db, CancellationToken ct)
    {
        var product = await Project(db.Products.Where(p => p.Id == id)).FirstOrDefaultAsync(ct);
        if (product is null) return Results.NotFound();

        var peers = await db.OrderDetails.Where(d => d.Product!.CategoryId == product.CategoryId)
            .GroupBy(d => new { d.ProductId, d.Product!.Name })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Name,
                Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)),
                Units = g.Sum(d => d.Quantity),
                Orders = g.Count(),
            })
            .OrderByDescending(x => x.Sales).ToListAsync(ct);

        var lines = db.OrderDetails.Where(d => d.ProductId == id);
        var byYear = await lines.GroupBy(d => d.Order!.OrderDate.Year)
            .Select(g => new { Year = g.Key, Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)), Units = g.Sum(d => d.Quantity) })
            .OrderBy(x => x.Year).ToListAsync(ct);

        var since = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
        var monthly = await lines.Where(d => d.Order!.OrderDate >= since)
            .GroupBy(d => new { d.Order!.OrderDate.Year, d.Order.OrderDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Units = g.Sum(d => d.Quantity), Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)) })
            .ToListAsync(ct);

        var topCustomers = await lines.GroupBy(d => new { d.Order!.CustomerId, d.Order.Customer!.CompanyName })
            .Select(g => new { g.Key.CustomerId, g.Key.CompanyName, Units = g.Sum(d => d.Quantity), Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)) })
            .OrderByDescending(x => x.Sales).Take(10).ToListAsync(ct);

        var rank = peers.FindIndex(p => p.ProductId == id) + 1;
        var categoryTotal = peers.Sum(p => p.Sales);

        return Results.Ok(new
        {
            product,
            rankInCategory = rank,
            productsInCategory = peers.Count,
            shareOfCategorySales = categoryTotal == 0 ? 0 : Math.Round(product.TotalSales / categoryTotal * 100, 2),
            similarProducts = peers.Select(p => new { productId = p.ProductId, name = p.Name, sales = Money.Round(p.Sales), units = p.Units, orders = p.Orders, isSelected = p.ProductId == id }),
            salesByYear = byYear.Select(x => new { year = x.Year, sales = Money.Round(x.Sales), units = x.Units }),
            last12Months = Enumerable.Range(0, 12).Select(i => since.AddMonths(i)).Select(m =>
            {
                var row = monthly.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month);
                return new { month = m.ToString("yyyy-MM"), units = row?.Units ?? 0, sales = Money.Round(row?.Sales ?? 0) };
            }),
            topCustomers = topCustomers.Select(c => new { customerId = c.CustomerId, companyName = c.CompanyName, units = c.Units, sales = Money.Round(c.Sales) }),
        });
    }
}
