using System.ComponentModel.DataAnnotations;
using DispatchApi.Infrastructure;
using DispatchApi.Models;

namespace DispatchApi.Dtos;

// ============================================================ Reference data
public record RegionDto(int Id, string Code, string Name, string TimeZone, double CenterLat, double CenterLng, int DepotCount);
public record DepotDto(int Id, int RegionId, string RegionName, string Code, string Name, string Address, string City, string State,
    string PostalCode, double Latitude, double Longitude, string Phone);
public record SkillDto(int Id, string Code, string Name, string Category, bool RequiresCertification);
public record JobTypeDto(int Id, string Code, string Name, JobCategory Category, int RequiredSkillId, string RequiredSkillName,
    int MinProficiency, int DefaultDurationMinutes, double BasePrice);
public record SlaPolicyDto(int Id, string Name, CustomerTier Tier, JobPriority Priority, int ResponseMinutes, int ResolutionMinutes,
    int AtRiskThresholdMinutes, double PenaltyAmount);
public record PartDto(int Id, string Sku, string Name, string Category, double UnitCost, double UnitPrice);
public record InventoryDto(int DepotId, string DepotName, int PartId, string Sku, string PartName, string Category, int QuantityOnHand,
    int ReorderLevel, bool BelowReorder);
public record VehicleDto(int Id, int DepotId, string DepotName, string PlateNumber, string Make, string Model, int Year,
    VehicleType VehicleType, VehicleStatus Status, int OdometerMiles, int? AssignedTechnicianId, string? AssignedTechnicianName);
public record DispatcherDto(int Id, string EmployeeNumber, string FirstName, string LastName, string FullName, string Email, string Phone,
    int RegionId, string RegionName, DispatcherShift ShiftName, DispatcherRole Role, bool IsActive);

public record ClockDto(DateTime Now, DateOnly Today, bool IsSimulated, DateOnly SeedAnchorDate, string GeneratorVersion);

public record LookupsDto(
    IReadOnlyList<RegionDto> Regions, IReadOnlyList<DepotDto> Depots, IReadOnlyList<SkillDto> Skills,
    IReadOnlyList<JobTypeDto> JobTypes, IReadOnlyList<SlaPolicyDto> SlaPolicies,
    IReadOnlyDictionary<string, string[]> Enums);

// ============================================================ Technicians
public record TechnicianSkillDto(int SkillId, string SkillCode, string SkillName, int Proficiency, DateOnly? CertifiedUntil, bool CertificationExpired);

public record TechnicianListItemDto(int Id, string EmployeeNumber, string FirstName, string LastName, string FullName, string Email, string Phone,
    int DepotId, string DepotName, int RegionId, string RegionName, TechnicianLevel Level, EmploymentStatus EmploymentStatus,
    FieldStatus FieldStatus, double Rating, int MaxJobsPerDay, string? VehiclePlate, string SkillCodes);

public record TechnicianDetailDto(int Id, string EmployeeNumber, string FirstName, string LastName, string FullName, string Email, string Phone,
    int DepotId, string DepotName, int RegionId, string RegionName, int? VehicleId, string? VehiclePlate, string? VehicleDescription,
    TechnicianLevel Level, EmploymentStatus EmploymentStatus, FieldStatus FieldStatus, DateOnly HireDate, double HourlyRate,
    int MaxJobsPerDay, double Rating, double HomeLatitude, double HomeLongitude, double? CurrentLatitude, double? CurrentLongitude,
    DateTime? LastLocationAt, IReadOnlyList<TechnicianSkillDto> Skills, TechnicianDayStatsDto Today);

public record TechnicianDayStatsDto(DateOnly Date, bool OnShift, TimeOnly? ShiftStart, TimeOnly? ShiftEnd, int JobsScheduled, int JobsCompleted,
    int JobsOpen, int BookedMinutes, int AvailableMinutes, double UtilizationPct);

public class TechnicianUpsertDto
{
    [Required, MaxLength(20)] public string EmployeeNumber { get; set; } = "";
    [Required, MaxLength(60)] public string FirstName { get; set; } = "";
    [Required, MaxLength(60)] public string LastName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Phone { get; set; } = "";
    [Range(1, int.MaxValue)] public int DepotId { get; set; }
    public int? VehicleId { get; set; }
    public TechnicianLevel Level { get; set; } = TechnicianLevel.TechI;
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Active;
    public FieldStatus FieldStatus { get; set; } = FieldStatus.Offline;
    public DateOnly? HireDate { get; set; }
    [Range(0, 500)] public double HourlyRate { get; set; } = 35;
    [Range(1, 15)] public int MaxJobsPerDay { get; set; } = 5;
    [Range(0, 5)] public double Rating { get; set; } = 4.5;
    public double HomeLatitude { get; set; }
    public double HomeLongitude { get; set; }
    public List<TechnicianSkillInput>? Skills { get; set; }
}

public class TechnicianSkillInput
{
    public int SkillId { get; set; }
    [Range(1, 5)] public int Proficiency { get; set; } = 3;
    public DateOnly? CertifiedUntil { get; set; }
}

public class FieldStatusUpdateDto
{
    public FieldStatus FieldStatus { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

// ============================================================ Shifts / time off
public record ShiftDto(int Id, int TechnicianId, string TechnicianName, DateOnly ShiftDate, TimeOnly StartTime, TimeOnly EndTime,
    ShiftType ShiftType, ShiftStatus Status, string? Notes);

public class ShiftUpsertDto
{
    [Range(1, int.MaxValue)] public int TechnicianId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public TimeOnly StartTime { get; set; } = new(8, 0);
    public TimeOnly EndTime { get; set; } = new(16, 30);
    public ShiftType ShiftType { get; set; } = ShiftType.Regular;
    public ShiftStatus Status { get; set; } = ShiftStatus.Scheduled;
    public string? Notes { get; set; }
}

public record TimeOffDto(int Id, int TechnicianId, string TechnicianName, DateOnly StartDate, DateOnly EndDate, TimeOffType Type,
    TimeOffStatus Status, string? Reason, DateTime RequestedAt);

public class TimeOffUpsertDto
{
    [Range(1, int.MaxValue)] public int TechnicianId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOffType Type { get; set; } = TimeOffType.Vacation;
    public TimeOffStatus Status { get; set; } = TimeOffStatus.Pending;
    public string? Reason { get; set; }
}

public record TechnicianScheduleDto(int TechnicianId, string TechnicianName, DateOnly From, DateOnly To,
    IReadOnlyList<ShiftDto> Shifts, IReadOnlyList<TimeOffDto> TimeOff, IReadOnlyList<JobListItemDto> Jobs);

// ============================================================ Customers / sites / assets
public record CustomerListItemDto(int Id, string AccountNumber, string Name, CustomerType CustomerType, CustomerTier Tier,
    string ContactName, string Phone, string Email, bool IsActive, int SiteCount, int OpenJobCount);

public record CustomerDetailDto(int Id, string AccountNumber, string Name, CustomerType CustomerType, CustomerTier Tier,
    string ContactName, string Phone, string Email, DateTime CreatedAt, bool IsActive, IReadOnlyList<SiteDto> Sites,
    int TotalJobs, int OpenJobs, double? AvgRating, double? SlaCompliancePct);

public class CustomerUpsertDto
{
    [Required, MaxLength(20)] public string AccountNumber { get; set; } = "";
    [Required, MaxLength(150)] public string Name { get; set; } = "";
    public CustomerType CustomerType { get; set; } = CustomerType.Commercial;
    public CustomerTier Tier { get; set; } = CustomerTier.Standard;
    [Required] public string ContactName { get; set; } = "";
    [Required] public string Phone { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public record SiteDto(int Id, int CustomerId, string CustomerName, CustomerTier CustomerTier, int RegionId, string RegionName, string Name,
    string Address, string City, string State, string PostalCode, double Latitude, double Longitude, string? AccessNotes,
    string? SiteContactPhone, int AssetCount);

public class SiteUpsertDto
{
    [Range(1, int.MaxValue)] public int CustomerId { get; set; }
    [Range(1, int.MaxValue)] public int RegionId { get; set; }
    [Required] public string Name { get; set; } = "";
    [Required] public string Address { get; set; } = "";
    [Required] public string City { get; set; } = "";
    [Required] public string State { get; set; } = "";
    [Required] public string PostalCode { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? AccessNotes { get; set; }
    public string? SiteContactPhone { get; set; }
}

public record AssetDto(int Id, int SiteId, string SiteName, int CustomerId, string AssetTag, string AssetType, string Manufacturer, string Model,
    string SerialNumber, DateOnly InstallDate, DateOnly? WarrantyExpires, bool UnderWarranty, AssetStatus Status);

public class AssetUpsertDto
{
    [Range(1, int.MaxValue)] public int SiteId { get; set; }
    [Required] public string AssetTag { get; set; } = "";
    [Required] public string AssetType { get; set; } = "";
    [Required] public string Manufacturer { get; set; } = "";
    [Required] public string Model { get; set; } = "";
    [Required] public string SerialNumber { get; set; } = "";
    public DateOnly InstallDate { get; set; }
    public DateOnly? WarrantyExpires { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.Operational;
}

// ============================================================ SLA
public enum SlaState { OnTrack, AtRisk, Breached, Met, NotApplicable }
public enum SlaStage { Response, Resolution, Closed }

public record SlaStatusDto(SlaState State, SlaStage Stage, DateTime? DueAt, int? MinutesRemaining, DateTime? ProjectedAt,
    string? Reason, bool ResponseMet, bool? ResolutionMet);

// ============================================================ Jobs
public record JobListItemDto(int Id, string JobNumber, JobStatus Status, JobPriority Priority, string Summary,
    int CustomerId, string CustomerName, CustomerTier CustomerTier, int SiteId, string SiteName, string SiteCity,
    double SiteLatitude, double SiteLongitude, int RegionId, string RegionName, int JobTypeId, string JobTypeName,
    JobCategory JobCategory, int RequiredSkillId, string RequiredSkillName,
    DateTime WindowStart, DateTime WindowEnd, DateTime? ScheduledStart, DateTime? ScheduledEnd, int EstimatedDurationMinutes,
    DateTime ResponseDueAt, DateTime ResolutionDueAt, int? AssignedTechnicianId, string? AssignedTechnicianName,
    DateTime? ActualStart, DateTime? ActualEnd, int RescheduleCount, DateTime CreatedAt, DateTime UpdatedAt, SlaStatusDto Sla);

public record JobEventDto(int Id, JobEventType EventType, JobStatus? FromStatus, JobStatus? ToStatus, int? TechnicianId,
    string? TechnicianName, int? DispatcherId, string? DispatcherName, DateTime OccurredAt, string? Details);
public record JobNoteDto(int Id, NoteAuthorType AuthorType, string AuthorName, string Body, bool IsInternal, DateTime CreatedAt);
public record JobPartDto(int Id, int PartId, string Sku, string PartName, int Quantity, double UnitPrice, double LineTotal, DateTime AddedAt);
public record RescheduleDto(int Id, int JobId, string JobNumber, int? FromTechnicianId, string? FromTechnicianName, int? ToTechnicianId,
    string? ToTechnicianName, DateTime? FromStart, DateTime? ToStart, RescheduleReason ReasonCode, string? Comment, int? DispatcherId,
    string? DispatcherName, DateTime CreatedAt);

public record JobDetailDto(JobListItemDto Job, string? Description, JobSource Source, int SlaPolicyId, string SlaPolicyName,
    int? AssetId, string? AssetTag, string? AssetDescription, string SiteAddress, string? SiteAccessNotes, string? SiteContactPhone,
    string CustomerPhone, string CustomerContact, int? AssignedByDispatcherId, string? AssignedByDispatcherName, DateTime? AssignedAt,
    string? OnHoldReason, string? CompletionNotes, bool? FirstTimeFix, int? CustomerRating, double PartsTotal,
    IReadOnlyList<JobEventDto> Events, IReadOnlyList<JobNoteDto> Notes, IReadOnlyList<JobPartDto> Parts, IReadOnlyList<RescheduleDto> Reschedules);

public class JobQuery : PageQuery
{
    /// <summary>One or more statuses (repeat the parameter: ?status=Assigned&amp;status=Dispatched).</summary>
    public List<JobStatus>? Status { get; set; }
    public List<JobPriority>? Priority { get; set; }
    public int? RegionId { get; set; }
    public int? TechnicianId { get; set; }
    public int? CustomerId { get; set; }
    public int? SiteId { get; set; }
    public int? JobTypeId { get; set; }
    public int? SkillId { get; set; }
    /// <summary>Jobs whose window starts on/after this date.</summary>
    public DateOnly? From { get; set; }
    /// <summary>Jobs whose window starts on/before this date.</summary>
    public DateOnly? To { get; set; }
    /// <summary>Shortcut for From=To=date.</summary>
    public DateOnly? Date { get; set; }
    public bool? Unassigned { get; set; }
    public bool? OpenOnly { get; set; }
    /// <summary>Filter by computed SLA state (OnTrack, AtRisk, Breached, Met).</summary>
    public List<SlaState>? SlaState { get; set; }
}

public class JobCreateDto
{
    [Range(1, int.MaxValue)] public int SiteId { get; set; }
    public int? AssetId { get; set; }
    [Range(1, int.MaxValue)] public int JobTypeId { get; set; }
    public JobPriority Priority { get; set; } = JobPriority.Medium;
    public JobSource Source { get; set; } = JobSource.Phone;
    [Required, MaxLength(200)] public string Summary { get; set; } = "";
    public string? Description { get; set; }
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public int? DispatcherId { get; set; }
}

public class JobUpdateDto
{
    public JobPriority? Priority { get; set; }
    [MaxLength(200)] public string? Summary { get; set; }
    public string? Description { get; set; }
    public DateTime? WindowStart { get; set; }
    public DateTime? WindowEnd { get; set; }
    public int? EstimatedDurationMinutes { get; set; }
    public int? AssetId { get; set; }
}

public class AssignJobDto
{
    [Range(1, int.MaxValue)] public int TechnicianId { get; set; }
    /// <summary>Defaults to the technician's first free slot at/after the window start.</summary>
    public DateTime? ScheduledStart { get; set; }
    public int? DispatcherId { get; set; }
    /// <summary>Assign even when validation reports blocking issues.</summary>
    public bool Force { get; set; }
    public string? Note { get; set; }
}

public class UnassignJobDto
{
    public int? DispatcherId { get; set; }
    public string? Reason { get; set; }
}

public class RescheduleJobDto
{
    public DateTime NewScheduledStart { get; set; }
    /// <summary>Optionally move the job to another technician at the same time.</summary>
    public int? TechnicianId { get; set; }
    public RescheduleReason ReasonCode { get; set; } = RescheduleReason.CustomerRequest;
    public string? Comment { get; set; }
    public int? DispatcherId { get; set; }
    /// <summary>Also move the customer window to match the new time.</summary>
    public bool UpdateWindow { get; set; } = true;
    public bool Force { get; set; }
}

public class StatusChangeDto
{
    public JobStatus Status { get; set; }
    public string? Note { get; set; }
    public int? DispatcherId { get; set; }
    public string? OnHoldReason { get; set; }
    public string? CompletionNotes { get; set; }
    public bool? FirstTimeFix { get; set; }
}

public class NoteCreateDto
{
    public NoteAuthorType AuthorType { get; set; } = NoteAuthorType.Dispatcher;
    [Required] public string AuthorName { get; set; } = "";
    [Required, MaxLength(4000)] public string Body { get; set; } = "";
    public bool IsInternal { get; set; } = true;
}

public class JobPartCreateDto
{
    [Range(1, int.MaxValue)] public int PartId { get; set; }
    [Range(1, 1000)] public int Quantity { get; set; } = 1;
}

// ============================================================ Assignment / scheduling
public enum IssueSeverity { Warning, Blocking }
public record AssignmentIssueDto(string Code, IssueSeverity Severity, string Message);
public record AssignmentValidationDto(bool CanAssign, DateTime ProposedStart, DateTime ProposedEnd, IReadOnlyList<AssignmentIssueDto> Issues);

public record TechnicianRecommendationDto(int TechnicianId, string TechnicianName, TechnicianLevel Level, FieldStatus FieldStatus,
    string DepotName, int Proficiency, double DistanceKm, int TravelMinutes, int JobsToday, int MaxJobsPerDay, double UtilizationPct,
    DateTime? ShiftStart, DateTime? ShiftEnd, DateTime? EarliestStart, bool MeetsResponseSla, double Score,
    IReadOnlyList<AssignmentIssueDto> Issues);

public record AssignmentResultDto(JobDetailDto Job, AssignmentValidationDto Validation);

public class ReassignDayDto
{
    public DateOnly Date { get; set; }
    /// <summary>"AutoDistribute" (best recommended tech per job), "ToTechnician" (all to TargetTechnicianId) or "Unassign".</summary>
    public ReassignMode Mode { get; set; } = ReassignMode.AutoDistribute;
    public int? TargetTechnicianId { get; set; }
    public RescheduleReason ReasonCode { get; set; } = RescheduleReason.TechnicianUnavailable;
    public string? Comment { get; set; }
    public int? DispatcherId { get; set; }
    /// <summary>Also mark the technician's shift for that date Absent.</summary>
    public bool MarkShiftAbsent { get; set; } = true;
}
public enum ReassignMode { AutoDistribute, ToTechnician, Unassign }

public record ReassignJobResultDto(int JobId, string JobNumber, string Outcome, int? NewTechnicianId, string? NewTechnicianName,
    DateTime? NewScheduledStart, string? Message);
public record ReassignDayResultDto(int TechnicianId, DateOnly Date, int JobsAffected, int Reassigned, int Unassigned,
    IReadOnlyList<ReassignJobResultDto> Results);

public class ShiftMoveDto
{
    public TimeOnly NewStartTime { get; set; }
    public TimeOnly NewEndTime { get; set; }
    /// <summary>Shift the technician's scheduled jobs that day by the same delta as the shift start.</summary>
    public bool MoveJobs { get; set; } = true;
    public int? DispatcherId { get; set; }
    public RescheduleReason ReasonCode { get; set; } = RescheduleReason.TechnicianUnavailable;
}

// ============================================================ Dispatch board
public record BoardTechnicianDto(int TechnicianId, string TechnicianName, TechnicianLevel Level, FieldStatus FieldStatus, int DepotId,
    string DepotName, string SkillCodes, DateTime? ShiftStart, DateTime? ShiftEnd, ShiftStatus? ShiftStatus, bool OnTimeOff,
    int BookedMinutes, double UtilizationPct, IReadOnlyList<JobListItemDto> Jobs);
public record DispatchBoardDto(DateOnly Date, DateTime Now, int? RegionId, IReadOnlyList<BoardTechnicianDto> Technicians,
    IReadOnlyList<JobListItemDto> Unassigned);

// ============================================================ SLA & dashboard
public record SlaCountsDto(int Total, int OnTrack, int AtRisk, int Breached, int Met);
public record SlaGroupDto(string Key, string Label, SlaCountsDto Counts, double PenaltyExposure);
public record SlaSummaryDto(DateOnly Date, DateTime Now, int? RegionId, SlaCountsDto Counts, double CompliancePct, double PenaltyExposure,
    IReadOnlyList<SlaGroupDto> ByPriority, IReadOnlyList<SlaGroupDto> ByRegion, IReadOnlyList<SlaGroupDto> ByTier,
    IReadOnlyList<SlaRiskHourDto> DueByHour);
public record SlaRiskHourDto(int Hour, int Due, int AtRisk, int Breached);
public record SlaTrendPointDto(DateOnly Date, int Completed, int ResponseMet, int ResolutionMet, double ResponseCompliancePct,
    double ResolutionCompliancePct, int Breaches);

public record KpiDto(DateOnly Date, DateTime Now, int? RegionId, int JobsToday, int Completed, int InProgress, int Unassigned, int OnHold,
    int Cancelled, int TechniciansOnShift, int TechniciansAvailable, int TechniciansOnSite, int TechniciansEnRoute, double UtilizationPct,
    double FirstTimeFixPct30d, double AvgCustomerRating30d, double ResponseCompliancePct30d, int AtRisk, int Breached,
    double AvgJobMinutes30d, int ReschedulesToday);
public record HourBucketDto(int Hour, int Scheduled, int Completed, int Unassigned);
public record StatusCountDto(string Key, int Count);
public record TechUtilizationDto(int TechnicianId, string TechnicianName, string RegionName, int Jobs, int BookedMinutes,
    int ShiftMinutes, double UtilizationPct);
