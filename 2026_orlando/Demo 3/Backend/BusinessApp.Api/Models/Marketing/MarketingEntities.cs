namespace BusinessApp.Api.Models.Marketing;

public enum CampaignChannel { Email, Social, Search, Display, Events, Webinar, DirectMail, Partner }
public enum CampaignStatus { Draft, Scheduled, Active, Paused, Completed }
public enum LeadStatus { New, Contacted, Qualified, Proposal, Won, Lost }
public enum LeadSource { Campaign, Website, Referral, TradeShow, ColdCall, Partner, Social }
public enum ActivityType { Call, Email, Meeting, Demo, Note }

public class Campaign
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public CampaignChannel Channel { get; set; }
    public CampaignStatus Status { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Budget { get; set; }
    public decimal ActualCost { get; set; }
    public decimal ExpectedRevenue { get; set; }
    public int Impressions { get; set; }
    public int Clicks { get; set; }
    public int? TargetCategoryId { get; set; }
    public Sales.Category? TargetCategory { get; set; }
    public int? OwnerId { get; set; }
    public Sales.Employee? Owner { get; set; }
    public List<Lead> Leads { get; set; } = [];
}

public class Lead
{
    public int Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Title { get; set; }
    public required string Company { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public LeadSource Source { get; set; }
    public LeadStatus Status { get; set; }
    /// <summary>0..100</summary>
    public int Score { get; set; }
    public decimal EstimatedValue { get; set; }
    public int? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public int? OwnerId { get; set; }
    public Sales.Employee? Owner { get; set; }
    public int? ConvertedCustomerId { get; set; }
    public Sales.Customer? ConvertedCustomer { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ConvertedAt { get; set; }
    public List<LeadActivity> Activities { get; set; } = [];
}

public class LeadActivity
{
    public int Id { get; set; }
    public int LeadId { get; set; }
    public Lead? Lead { get; set; }
    public ActivityType Type { get; set; }
    public required string Subject { get; set; }
    public string? Notes { get; set; }
    public DateTime OccurredAt { get; set; }
    public int? EmployeeId { get; set; }
    public Sales.Employee? Employee { get; set; }
}
