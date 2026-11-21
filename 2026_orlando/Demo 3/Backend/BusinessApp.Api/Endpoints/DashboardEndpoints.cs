using BusinessApp.Api.Common;
using BusinessApp.Api.Data;
using BusinessApp.Api.Models.Marketing;
using BusinessApp.Api.Models.Projects;
using Microsoft.EntityFrameworkCore;

namespace BusinessApp.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this RouteGroupBuilder api)
    {
        var g = api.MapGroup("/dashboard").WithTags("Dashboard");

        // Workplace / home screen KPIs across all modules.
        g.MapGet("/summary", async (AppDbContext db, CancellationToken ct) =>
        {
            var today = DateTime.Today;
            var yearStart = new DateTime(today.Year, 1, 1);
            var lastYearStart = yearStart.AddYears(-1);
            var lastYearSameDay = today.AddYears(-1);

            var ytd = await SalesBetween(db, yearStart, today.AddDays(1), ct);
            var lastYtd = await SalesBetween(db, lastYearStart, lastYearSameDay.AddDays(1), ct);
            var ordersYtd = await db.Orders.CountAsync(o => o.OrderDate >= yearStart, ct);

            return new
            {
                asOf = DateOnly.FromDateTime(today),
                sales = new
                {
                    salesYearToDate = Money.Round(ytd),
                    salesLastYearToDate = Money.Round(lastYtd),
                    growthPercent = lastYtd == 0 ? 0 : Math.Round((ytd - lastYtd) / lastYtd * 100, 1),
                    ordersYearToDate = ordersYtd,
                    averageOrderValue = ordersYtd == 0 ? 0 : Money.Round(ytd / ordersYtd),
                    activeCustomers90Days = await db.Orders.Where(o => o.OrderDate >= today.AddDays(-90)).Select(o => o.CustomerId).Distinct().CountAsync(ct),
                    totalCustomers = await db.Customers.CountAsync(ct),
                    pendingShipments = await db.Orders.CountAsync(o => o.ShippedDate == null, ct),
                    overdueShipments = await db.Orders.CountAsync(o => o.ShippedDate == null && o.RequiredDate < today, ct),
                    productsToReorder = await db.Products.CountAsync(p => !p.Discontinued && p.UnitsInStock + p.UnitsOnOrder <= p.ReorderLevel, ct),
                    outOfStock = await db.Products.CountAsync(p => !p.Discontinued && p.UnitsInStock <= 0, ct),
                },
                projects = new
                {
                    active = await db.Projects.CountAsync(p => p.Status == ProjectStatus.Active, ct),
                    planned = await db.Projects.CountAsync(p => p.Status == ProjectStatus.Planned, ct),
                    overdueTasks = await db.ProjectTasks.CountAsync(t => !t.IsSummary && t.PercentComplete < 100 && t.Finish < today && t.Project!.Status == ProjectStatus.Active, ct),
                    milestonesNext30Days = await db.ProjectTasks.CountAsync(t => t.IsMilestone && t.Start >= today && t.Start <= today.AddDays(30), ct),
                },
                marketing = new
                {
                    activeCampaigns = await db.Campaigns.CountAsync(c => c.Status == CampaignStatus.Active, ct),
                    openLeads = await db.Leads.CountAsync(l => l.Status != LeadStatus.Won && l.Status != LeadStatus.Lost, ct),
                    newLeads30Days = await db.Leads.CountAsync(l => l.CreatedAt >= today.AddDays(-30), ct),
                    leadsWon30Days = await db.Leads.CountAsync(l => l.ConvertedAt >= today.AddDays(-30), ct),
                },
            };
        });

        g.MapGet("/sales-overview", async (AppDbContext db, CancellationToken ct) =>
        {
            var y = await ReportEndpoints.LatestYear(db, ct);
            var byCategory = await db.OrderDetails.Where(d => d.Order!.OrderDate.Year == y)
                .GroupBy(d => d.Product!.Category!.Name)
                .Select(g => new { g.Key, Sales = g.Sum(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount)) })
                .OrderByDescending(x => x.Sales).ToListAsync(ct);
            var byShipper = await db.Orders.Where(o => o.OrderDate.Year == y)
                .GroupBy(o => o.ShipVia!.CompanyName)
                .Select(g => new NameValue(g.Key, g.Count())).ToListAsync(ct);
            var recent = await OrderEndpoints.Project(db.Orders).OrderByDescending(o => o.OrderDate).Take(10).ToListAsync(ct);
            var lowStock = await ProductEndpoints.Project(db.Products.Where(p => !p.Discontinued && p.UnitsInStock + p.UnitsOnOrder <= p.ReorderLevel))
                .OrderBy(p => p.UnitsInStock).Take(10).ToListAsync(ct);

            return new
            {
                year = y,
                salesByCategory = byCategory.Select(x => new NameValue(x.Key, Money.Round(x.Sales))),
                ordersByShipper = byShipper,
                recentOrders = recent,
                lowStockProducts = lowStock,
            };
        });
    }

    private static async Task<double> SalesBetween(AppDbContext db, DateTime from, DateTime to, CancellationToken ct) =>
        await db.OrderDetails.Where(d => d.Order!.OrderDate >= from && d.Order.OrderDate < to)
            .SumAsync(d => (double)d.UnitPrice * d.Quantity * (1 - d.Discount), ct);
}
