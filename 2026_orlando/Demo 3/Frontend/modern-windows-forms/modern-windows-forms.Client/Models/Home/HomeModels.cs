using modern_windows_forms.Client.Models;

namespace modern_windows_forms.Client.Models.Home;

/// <summary>GET /api/reports/monthly-sales?months=13 — one row per calendar month ("2026-11").</summary>
public sealed record MonthlySales(string Month, int Orders, decimal Sales);

/// <summary>GET /api/dashboard/sales-overview.</summary>
public sealed record SalesOverview(
    int Year,
    IReadOnlyList<NameValue> SalesByCategory,
    IReadOnlyList<NameValue> OrdersByShipper,
    IReadOnlyList<OrderRow> RecentOrders,
    IReadOnlyList<ProductRow> LowStockProducts);

/// <summary>Order row as returned by /api/orders and the overview's recentOrders.</summary>
public sealed record OrderRow(
    int Id, int CustomerId, string CustomerName, string? EmployeeName,
    DateTime OrderDate, DateTime? RequiredDate, DateTime? ShippedDate,
    string? ShipViaName, decimal Subtotal, decimal Total, string Status);

public sealed record ProductRow(
    int Id, string Name, string? CategoryName, int UnitsInStock, int UnitsOnOrder,
    int ReorderLevel, bool Discontinued, bool NeedsReorder);

public sealed record ProjectRow(
    int Id, string Name, string Status, DateTime StartDate, DateTime FinishDate,
    string? ManagerName, decimal Budget, int TaskCount, int CompletedTaskCount, decimal PercentComplete);

public sealed record CampaignRow(
    int Id, string Name, string Channel, string Status, DateTime StartDate, DateTime EndDate,
    decimal Budget, decimal ActualCost, int LeadCount, string? OwnerName);

/// <summary>GET /api/marketing/dashboard — only the parts the home screen needs.</summary>
public sealed record MarketingDashboard(MarketingKpis Kpis, IReadOnlyList<NameValue> LeadsByStatus);
public sealed record MarketingKpis(int OpenLeads, decimal PipelineValue, int ActiveCampaigns);

/// <summary>GET /api/projects/{id}/gantt — tasks only, used to find work that is behind schedule.</summary>
public sealed record ProjectGantt(IReadOnlyList<GanttTaskRow> Tasks);
public sealed record GanttTaskRow(
    int Id, string Name, DateTime Start, DateTime Finish, decimal PercentComplete,
    bool IsMilestone, bool IsSummary, string? Resources);

// ---------- View models ----------

public enum DeltaTone { Up, Down, Neutral }

public sealed record SparkPoint(int Index, decimal Value);

public sealed record KpiCard(
    string Label, string Value, string Sub, string Delta, DeltaTone Tone,
    string Href, string Color, IReadOnlyList<SparkPoint> Spark);

public sealed record MonthPoint(string Short, string Full, decimal Sales, int Orders);

public enum AttentionTone { Error, Warning, Info }

public sealed record AttentionItem(string Title, string Meta, string Href, string Icon, AttentionTone Tone);

/// <summary>Flat, preformatted row for the Recent orders IgbGrid (cell templates look rows up by primary key).</summary>
public sealed class RecentOrderVm
{
    public int Id { get; set; }
    public string Customer { get; set; } = "";
    public string Initials { get; set; } = "";
    public string AvatarClass { get; set; } = "";
    public string Date { get; set; } = "";
    public string ShipVia { get; set; } = "";
    public decimal Total { get; set; }
    public string TotalText { get; set; } = "";
    public string Status { get; set; } = "";
}
