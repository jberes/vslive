using System.ComponentModel.DataAnnotations;
using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Sales;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public sealed class CategoryDto
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public int ProductCount { get; init; }
    public int ActiveProductCount { get; init; }
    public double TotalSales { get; init; }
}

public sealed class CategoryInput
{
    [Required, StringLength(64)] public string Name { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
}

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/categories").WithTags("Categories");

        g.MapGet("/", async ([AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await Project(db.Categories).ToPagedResultAsync(q,
                s => c => c.Name.ToLower().Contains(s) || (c.Description ?? "").ToLower().Contains(s), "name", ct));

        g.MapGet("/lookup", async (AppDbContext db, CancellationToken ct) =>
            await db.Categories.OrderBy(c => c.Name).Select(c => new LookupItem(c.Id, c.Name)).ToListAsync(ct));

        g.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
            await Project(db.Categories.Where(c => c.Id == id)).FirstOrDefaultAsync(ct) is { } dto ? Results.Ok(dto) : Results.NotFound());

        g.MapGet("/{id:int}/products", async (int id, [AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
            await ProductEndpoints.Project(db.Products.Where(p => p.CategoryId == id)).ToPagedResultAsync(q, ProductEndpoints.Search, "name", ct));

        g.MapGet("/{id:int}/dashboard", Dashboard);

        g.MapPost("/", async (CategoryInput input, AppDbContext db, CancellationToken ct) =>
        {
            var entity = new Category { Name = input.Name.Trim(), Description = input.Description };
            db.Categories.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/categories/{entity.Id}", await Project(db.Categories.Where(c => c.Id == entity.Id)).FirstAsync(ct));
        });

        g.MapPut("/{id:int}", async (int id, CategoryInput input, AppDbContext db, CancellationToken ct) =>
        {
            var entity = await db.Categories.FindAsync([id], ct);
            if (entity is null) return Results.NotFound();
            entity.Name = input.Name.Trim();
            entity.Description = input.Description;
            await db.SaveChangesAsync(ct);
            return Results.Ok(await Project(db.Categories.Where(c => c.Id == id)).FirstAsync(ct));
        });

        g.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            if (await db.Products.AnyAsync(p => p.CategoryId == id, ct))
                return Results.Problem("Category has products. Reassign or delete them first.", statusCode: 409);
            return await db.Categories.Where(c => c.Id == id).ExecuteDeleteAsync(ct) > 0 ? Results.NoContent() : Results.NotFound();
        });
    }

    internal static IQueryable<CategoryDto> Project(IQueryable<Category> q) => q.Select(c => new CategoryDto
    {
        Id = c.Id,
        Name = c.Name,
        Description = c.Description,
        ProductCount = c.Products.Count,
        ActiveProductCount = c.Products.Count(p => !p.Discontinued),
        TotalSales = Math.Round(c.Products.SelectMany(p => p.OrderDetails).Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)), 2),
    });

    /// <summary>"Categories" screen charts: product breakdown, yearly and quarterly trend for the selected category.</summary>
    private static async Task<IResult> Dashboard(int id, int? year, AppDbContext db, CancellationToken ct)
    {
        var category = await db.Categories.FindAsync([id], ct);
        if (category is null) return Results.NotFound();

        var lines = db.OrderDetails.Where(d => d.Product!.CategoryId == id);
        var y = year ?? await db.Orders.MaxAsync(o => o.OrderDate.Year, ct);

        var byProduct = await lines.Where(d => d.Order!.OrderDate.Year == y)
            .GroupBy(d => d.Product!.Name)
            .Select(g => new { g.Key, Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)), Units = g.Sum(d => d.Quantity) })
            .OrderByDescending(x => x.Sales).ToListAsync(ct);

        var byYear = await lines.GroupBy(d => d.Order!.OrderDate.Year)
            .Select(g => new { Year = g.Key, Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)) })
            .OrderBy(x => x.Year).ToListAsync(ct);

        var byQuarter = await lines.Where(d => d.Order!.OrderDate.Year == y)
            .GroupBy(d => (d.Order!.OrderDate.Month - 1) / 3 + 1)
            .Select(g => new { Quarter = g.Key, Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)) })
            .ToListAsync(ct);

        var allCategories = await db.OrderDetails.Where(d => d.Order!.OrderDate.Year == y)
            .GroupBy(d => d.Product!.Category!.Name)
            .Select(g => new { g.Key, Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)) })
            .ToListAsync(ct);
        var categoryTotal = allCategories.FirstOrDefault(c => c.Key == category.Name)?.Sales ?? 0;
        var grandTotal = allCategories.Sum(c => c.Sales);

        return Results.Ok(new
        {
            category = new { category.Id, category.Name, category.Description },
            year = y,
            totalSales = Money.Round(categoryTotal),
            shareOfTotalSales = grandTotal == 0 ? 0 : Math.Round(categoryTotal / grandTotal * 100, 2),
            salesByProduct = byProduct.Select(p => new { product = p.Key, sales = Money.Round(p.Sales), units = p.Units }),
            salesByYear = byYear.Select(x => new NameValue(x.Year.ToString(), Money.Round(x.Sales))),
            salesByQuarter = Enumerable.Range(1, 4).Select(qn => new NameValue($"Q{qn}", Money.Round(byQuarter.FirstOrDefault(x => x.Quarter == qn)?.Sales ?? 0))),
            categoryVsOthers = new[] { new NameValue(category.Name, Money.Round(categoryTotal)), new NameValue("Other categories", Money.Round(grandTotal - categoryTotal)) },
        });
    }
}
