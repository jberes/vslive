using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public sealed class QuarterlyOrderRow
{
    public int CustomerId { get; init; }
    public required string CompanyName { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public int Orders { get; init; }
    public double Qtr1 { get; init; }
    public double Qtr2 { get; init; }
    public double Qtr3 { get; init; }
    public double Qtr4 { get; init; }
    public double Total { get; init; }
}

public sealed class ProductSalesRow
{
    public int ProductId { get; init; }
    public required string ProductName { get; init; }
    public string? CategoryName { get; init; }
    public int Units { get; init; }
    public double Qtr1 { get; init; }
    public double Qtr2 { get; init; }
    public double Qtr3 { get; init; }
    public double Qtr4 { get; init; }
    public double Sales { get; init; }
}

/// <summary>Read-only analytic endpoints that mirror the classic Northwind views in the WinForms navigation.</summary>
public static class ReportEndpoints
{
    private static readonly string[] MonthNames = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

    public static void MapReportEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/reports").WithTags("Reports");

        g.MapGet("/years", async (AppDbContext db, CancellationToken ct) =>
            await db.Orders.Select(o => o.OrderDate.Year).Distinct().OrderByDescending(y => y).ToListAsync(ct));

        // "Quarterly Orders": per-customer sales split by quarter.
        g.MapGet("/quarterly-orders", async (int? year, [AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
        {
            var y = year ?? await LatestYear(db, ct);
            var rows = db.OrderDetails.Where(d => d.Order!.OrderDate.Year == y)
                .GroupBy(d => new { d.Order!.CustomerId, d.Order.Customer!.CompanyName, d.Order.Customer.City, d.Order.Customer.Country })
                .Select(g => new QuarterlyOrderRow
                {
                    CustomerId = g.Key.CustomerId,
                    CompanyName = g.Key.CompanyName,
                    City = g.Key.City,
                    Country = g.Key.Country,
                    Orders = g.Select(d => d.OrderId).Distinct().Count(),
                    Qtr1 = Math.Round(g.Sum(d => d.Order!.OrderDate.Month <= 3 ? (double)d.UnitPrice * d.Quantity * (1 - d.Discount) : 0), 2),
                    Qtr2 = Math.Round(g.Sum(d => d.Order!.OrderDate.Month >= 4 && d.Order.OrderDate.Month <= 6 ? (double)d.UnitPrice * d.Quantity * (1 - d.Discount) : 0), 2),
                    Qtr3 = Math.Round(g.Sum(d => d.Order!.OrderDate.Month >= 7 && d.Order.OrderDate.Month <= 9 ? (double)d.UnitPrice * d.Quantity * (1 - d.Discount) : 0), 2),
                    Qtr4 = Math.Round(g.Sum(d => d.Order!.OrderDate.Month >= 10 ? (double)d.UnitPrice * d.Quantity * (1 - d.Discount) : 0), 2),
                    Total = Math.Round(g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)), 2),
                });
            var page = await rows.ToPagedResultAsync(q, s => r =>
                r.CompanyName.ToLower().Contains(s) || (r.City ?? "").ToLower().Contains(s) || (r.Country ?? "").ToLower().Contains(s), "companyName", ct);
            return new { year = y, page.Items, page.TotalCount, page.Page, page.PageSize, page.TotalPages };
        });

        // "Product Sales": per-product sales for a year split by quarter.
        g.MapGet("/product-sales", async (int? year, [AsParameters] GridQuery q, AppDbContext db, CancellationToken ct) =>
        {
            var y = year ?? await LatestYear(db, ct);
            var rows = db.OrderDetails.Where(d => d.Order!.OrderDate.Year == y)
                .GroupBy(d => new { d.ProductId, d.Product!.Name, Category = d.Product.Category!.Name })
                .Select(g => new ProductSalesRow
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.Name,
                    CategoryName = g.Key.Category,
                    Units = g.Sum(d => d.Quantity),
                    Qtr1 = Math.Round(g.Sum(d => d.Order!.OrderDate.Month <= 3 ? (double)d.UnitPrice * d.Quantity * (1 - d.Discount) : 0), 2),
                    Qtr2 = Math.Round(g.Sum(d => d.Order!.OrderDate.Month >= 4 && d.Order.OrderDate.Month <= 6 ? (double)d.UnitPrice * d.Quantity * (1 - d.Discount) : 0), 2),
                    Qtr3 = Math.Round(g.Sum(d => d.Order!.OrderDate.Month >= 7 && d.Order.OrderDate.Month <= 9 ? (double)d.UnitPrice * d.Quantity * (1 - d.Discount) : 0), 2),
                    Qtr4 = Math.Round(g.Sum(d => d.Order!.OrderDate.Month >= 10 ? (double)d.UnitPrice * d.Quantity * (1 - d.Discount) : 0), 2),
                    Sales = Math.Round(g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)), 2),
                });
            var page = await rows.ToPagedResultAsync(q, s => r =>
                r.ProductName.ToLower().Contains(s) || (r.CategoryName ?? "").ToLower().Contains(s), "-sales", ct);
            return new { year = y, page.Items, page.TotalCount, page.Page, page.PageSize, page.TotalPages };
        });

        // "Sales By Category": category totals plus the per-product breakdown.
        g.MapGet("/sales-by-category", async (int? year, AppDbContext db, CancellationToken ct) =>
        {
            var y = year ?? await LatestYear(db, ct);
            var rows = await db.OrderDetails.Where(d => d.Order!.OrderDate.Year == y)
                .GroupBy(d => new { d.Product!.CategoryId, Category = d.Product.Category!.Name, d.ProductId, Product = d.Product.Name })
                .Select(g => new
                {
                    g.Key.CategoryId, g.Key.Category, g.Key.ProductId, g.Key.Product,
                    Units = g.Sum(d => d.Quantity),
                    Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)),
                }).ToListAsync(ct);

            return new
            {
                year = y,
                categories = rows.GroupBy(r => new { r.CategoryId, r.Category })
                    .Select(c => new
                    {
                        categoryId = c.Key.CategoryId,
                        categoryName = c.Key.Category,
                        units = c.Sum(r => r.Units),
                        sales = Money.Round(c.Sum(r => r.Sales)),
                        products = c.OrderByDescending(r => r.Sales)
                            .Select(r => new { productId = r.ProductId, productName = r.Product, units = r.Units, sales = Money.Round(r.Sales) }),
                    })
                    .OrderByDescending(c => c.sales),
            };
        });

        // "Sales By Quarter": quarterly KPIs plus a category x quarter matrix.
        g.MapGet("/sales-by-quarter", async (int? year, AppDbContext db, CancellationToken ct) =>
        {
            var y = year ?? await LatestYear(db, ct);
            var orders = await db.Orders.Where(o => o.OrderDate.Year == y)
                .GroupBy(o => (o.OrderDate.Month - 1) / 3 + 1)
                .Select(g => new
                {
                    Quarter = g.Key,
                    Orders = g.Count(),
                    Customers = g.Select(o => o.CustomerId).Distinct().Count(),
                    Freight = g.Sum(o => (double)o.Freight),
                    Sales = g.SelectMany(o => o.Details).Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)),
                }).ToListAsync(ct);

            var byCategory = await db.OrderDetails.Where(d => d.Order!.OrderDate.Year == y)
                .GroupBy(d => new { Quarter = (d.Order!.OrderDate.Month - 1) / 3 + 1, Category = d.Product!.Category!.Name })
                .Select(g => new { g.Key.Quarter, g.Key.Category, Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)) })
                .ToListAsync(ct);

            var labels = new[] { "Q1", "Q2", "Q3", "Q4" };
            return new
            {
                year = y,
                quarters = Enumerable.Range(1, 4).Select(qn =>
                {
                    var r = orders.FirstOrDefault(o => o.Quarter == qn);
                    return new
                    {
                        quarter = $"Q{qn}",
                        orders = r?.Orders ?? 0,
                        customers = r?.Customers ?? 0,
                        sales = Money.Round(r?.Sales ?? 0),
                        freight = Money.Round(r?.Freight ?? 0),
                        averageOrderValue = r is { Orders: > 0 } ? Money.Round(r.Sales / r.Orders) : 0,
                    };
                }),
                byCategory = ChartData.From(labels, byCategory.Select(c => c.Category).Distinct().Order(), byCategory,
                    r => $"Q{r.Quarter}", r => r.Category, r => r.Sales),
            };
        });

        // "Sales By Year": yearly KPIs with growth plus a month x year comparison chart.
        g.MapGet("/sales-by-year", async (AppDbContext db, CancellationToken ct) =>
        {
            var years = await db.Orders.GroupBy(o => o.OrderDate.Year)
                .Select(g => new
                {
                    Year = g.Key,
                    Orders = g.Count(),
                    Customers = g.Select(o => o.CustomerId).Distinct().Count(),
                    Freight = g.Sum(o => (double)o.Freight),
                    Sales = g.SelectMany(o => o.Details).Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)),
                })
                .OrderBy(x => x.Year).ToListAsync(ct);

            var monthly = await db.OrderDetails
                .GroupBy(d => new { d.Order!.OrderDate.Year, d.Order.OrderDate.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)) })
                .ToListAsync(ct);

            return new
            {
                years = years.Select((x, i) => new
                {
                    year = x.Year,
                    orders = x.Orders,
                    customers = x.Customers,
                    sales = Money.Round(x.Sales),
                    freight = Money.Round(x.Freight),
                    averageOrderValue = Money.Round(x.Sales / x.Orders),
                    growthPercent = i == 0 || years[i - 1].Sales == 0 ? (double?)null : Math.Round((x.Sales - years[i - 1].Sales) / years[i - 1].Sales * 100, 1),
                }),
                monthlyByYear = ChartData.From(MonthNames, years.Select(y => y.Year.ToString()), monthly,
                    r => MonthNames[r.Month - 1], r => r.Year.ToString(), r => r.Sales),
            };
        });

        g.MapGet("/sales-by-country", async (int? year, AppDbContext db, CancellationToken ct) =>
        {
            var rows = await db.OrderDetails.Where(d => year == null || d.Order!.OrderDate.Year == year)
                .GroupBy(d => d.Order!.ShipCountry ?? "Unknown")
                .Select(g => new
                {
                    country = g.Key,
                    orders = g.Select(d => d.OrderId).Distinct().Count(),
                    customers = g.Select(d => d.Order!.CustomerId).Distinct().Count(),
                    sales = Math.Round(g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)), 2),
                })
                .OrderByDescending(x => x.sales).ToListAsync(ct);
            return new { year, countries = rows };
        });

        g.MapGet("/sales-by-employee", async (int? year, AppDbContext db, CancellationToken ct) =>
        {
            var rows = await db.OrderDetails.Where(d => year == null || d.Order!.OrderDate.Year == year)
                .GroupBy(d => new { d.Order!.EmployeeId, d.Order.Employee!.FirstName, d.Order.Employee.LastName, d.Order.Employee.Title })
                .Select(g => new
                {
                    employeeId = g.Key.EmployeeId,
                    name = g.Key.FirstName + " " + g.Key.LastName,
                    title = g.Key.Title,
                    orders = g.Select(d => d.OrderId).Distinct().Count(),
                    sales = Math.Round(g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)), 2),
                })
                .OrderByDescending(x => x.sales).ToListAsync(ct);
            return new { year, employees = rows };
        });

        g.MapGet("/monthly-sales", async (int? months, AppDbContext db, CancellationToken ct) =>
        {
            var count = Math.Clamp(months ?? 24, 1, 120);
            var since = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-(count - 1));
            var rows = await db.Orders.Where(o => o.OrderDate >= since)
                .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                .Select(g => new
                {
                    g.Key.Year, g.Key.Month,
                    Orders = g.Count(),
                    Sales = g.SelectMany(o => o.Details).Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)),
                }).ToListAsync(ct);
            return Enumerable.Range(0, count).Select(i => since.AddMonths(i)).Select(m =>
            {
                var r = rows.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month);
                return new { month = m.ToString("yyyy-MM"), orders = r?.Orders ?? 0, sales = Money.Round(r?.Sales ?? 0) };
            });
        });

        g.MapGet("/top-customers", async (int? year, int? top, AppDbContext db, CancellationToken ct) =>
            await db.OrderDetails.Where(d => year == null || d.Order!.OrderDate.Year == year)
                .GroupBy(d => new { d.Order!.CustomerId, d.Order.Customer!.CompanyName, d.Order.Customer.Country })
                .Select(g => new
                {
                    customerId = g.Key.CustomerId,
                    companyName = g.Key.CompanyName,
                    country = g.Key.Country,
                    orders = g.Select(d => d.OrderId).Distinct().Count(),
                    sales = Math.Round(g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)), 2),
                })
                .OrderByDescending(x => x.sales).Take(Math.Clamp(top ?? 10, 1, 100)).ToListAsync(ct));

        g.MapGet("/top-products", async (int? year, int? top, AppDbContext db, CancellationToken ct) =>
            await db.OrderDetails.Where(d => year == null || d.Order!.OrderDate.Year == year)
                .GroupBy(d => new { d.ProductId, d.Product!.Name, Category = d.Product.Category!.Name })
                .Select(g => new
                {
                    productId = g.Key.ProductId,
                    productName = g.Key.Name,
                    categoryName = g.Key.Category,
                    units = g.Sum(d => d.Quantity),
                    sales = Math.Round(g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)), 2),
                })
                .OrderByDescending(x => x.sales).Take(Math.Clamp(top ?? 10, 1, 77)).ToListAsync(ct));
    }

    internal static async Task<int> LatestYear(AppDbContext db, CancellationToken ct) =>
        await db.Orders.MaxAsync(o => (int?)o.OrderDate.Year, ct) ?? DateTime.Today.Year;
}
