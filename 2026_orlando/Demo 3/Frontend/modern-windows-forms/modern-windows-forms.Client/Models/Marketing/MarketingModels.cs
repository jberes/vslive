namespace modern_windows_forms.Client.Models.Marketing;

/// <summary>Lead pipeline stages (API enum <c>LeadStatus</c>, serialized as strings).</summary>
public enum LeadStatus { New, Contacted, Qualified, Proposal, Won, Lost }

/// <summary>GET /api/leads item.</summary>
public sealed record LeadDto(
    int Id, string FirstName, string LastName, string FullName, string? Title, string Company,
    string? Email, string? Phone, string? City, string? Country, string Source, LeadStatus Status,
    int Score, decimal EstimatedValue, int? CampaignId, string? CampaignName, int? OwnerId, string? OwnerName,
    int? ConvertedCustomerId, DateTime CreatedAt, DateTime? ConvertedAt, DateTime? LastActivityAt,
    int ActivityCount, string? AvatarUrl);

/// <summary>Body of PATCH /api/leads/{id}/status.</summary>
public sealed record LeadStatusInput(LeadStatus Status);

/// <summary>Customer as returned by POST /api/leads/{id}/convert (and GET /api/customers/{id}).</summary>
public sealed record ConvertedCustomer(
    int Id, string Code, string CompanyName, string? ContactName, string? ContactTitle, string? Email,
    string? Address, string? City, string? Region, string? PostalCode, string? Country, string? Phone, string? Fax);

/// <summary>Response of POST /api/leads/{id}/convert.</summary>
public sealed record LeadConversion(LeadDto Lead, ConvertedCustomer Customer);

/// <summary>Body of PUT /api/customers/{id} (used to apply edits made in the convert dialog).</summary>
public sealed record CustomerInput(
    string? Code, string CompanyName, string? ContactName, string? ContactTitle, string? Email, string? Address,
    string? City, string? Region, string? PostalCode, string? Country, string? Phone, string? Fax);

/// <summary>GET /api/customers/countries item (country select in the convert dialog).</summary>
public sealed record CountryCount(string Country, int Customers);

/// <summary>GET /api/campaigns item.</summary>
public sealed record CampaignDto(
    int Id, string Name, string? Description, string Channel, string Status, DateTime StartDate, DateTime EndDate,
    decimal Budget, decimal ActualCost, decimal ExpectedRevenue, long Impressions, long Clicks, decimal ClickThroughRate,
    int? TargetCategoryId, string? TargetCategoryName, int? OwnerId, string? OwnerName, int LeadCount, int WonCount,
    decimal ConversionRate, decimal WonValue, decimal CostPerLead, decimal RoiPercent);

/// <summary>GET /api/campaigns/{id}/performance.</summary>
public sealed record CampaignPerformance(
    CampaignDto Campaign, IReadOnlyList<NameValue> Funnel, IReadOnlyList<NameValue> LeadsByStatus,
    IReadOnlyList<NameValue> LeadsByWeek, decimal BudgetUsedPercent);

/// <summary>GET /api/marketing/dashboard (KPIs only; the other sections are not used by these screens).</summary>
public sealed record MarketingDashboard(MarketingKpis Kpis);

public sealed record MarketingKpis(
    int TotalLeads, int OpenLeads, int WonLeads, decimal WinRatePercent, decimal PipelineValue,
    decimal WonValue, int ActiveCampaigns, decimal TotalSpend);

/// <summary>Flat row bound to the Campaigns IgbGrid (PascalCase fields are the grid's column keys).</summary>
public sealed class CampaignRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Channel { get; init; } = "";
    public string Status { get; init; } = "";
    public string Dates { get; init; } = "";
    public decimal Budget { get; init; }
    public decimal ActualCost { get; init; }
    public double SpendPercent { get; init; }
    public int Leads { get; init; }
    public string Ctr { get; init; } = "";
    public string Owner { get; init; } = "";
}
