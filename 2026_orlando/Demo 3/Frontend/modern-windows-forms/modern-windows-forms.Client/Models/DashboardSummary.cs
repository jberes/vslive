namespace modern_windows_forms.Client.Models;

/// <summary>GET /api/dashboard/summary — home KPIs across Sales, Projects and Marketing.</summary>
public sealed record DashboardSummary(DateTime AsOf, SalesSummary Sales, ProjectsSummary Projects, MarketingSummary Marketing);

public sealed record SalesSummary(
    decimal SalesYearToDate, decimal SalesLastYearToDate, decimal? GrowthPercent, int OrdersYearToDate,
    decimal AverageOrderValue, int ActiveCustomers90Days, int TotalCustomers, int PendingShipments,
    int OverdueShipments, int ProductsToReorder, int OutOfStock);

public sealed record ProjectsSummary(int Active, int Planned, int OverdueTasks, int MilestonesNext30Days);

public sealed record MarketingSummary(int ActiveCampaigns, int OpenLeads, int NewLeads30Days, int LeadsWon30Days);
