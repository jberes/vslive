namespace DispatchApi.Models;

// ---------------------------------------------------------------- Enums (stored as TEXT)
public enum DispatcherShift { Early, Day, Late, Night }
public enum DispatcherRole { Dispatcher, SeniorDispatcher, Supervisor }
public enum VehicleType { Van, Pickup, BoxTruck, Car }
public enum VehicleStatus { InService, Maintenance, OutOfService }
public enum TechnicianLevel { Apprentice, TechI, TechII, Senior, Lead }
public enum EmploymentStatus { Active, OnLeave, Inactive }
public enum FieldStatus { Available, EnRoute, OnSite, OnBreak, Offline }
public enum ShiftType { Regular, Overtime, OnCall }
public enum ShiftStatus { Scheduled, InProgress, Completed, Absent, Cancelled }
public enum TimeOffType { Vacation, Sick, Training, Personal }
public enum TimeOffStatus { Pending, Approved, Denied }
public enum CustomerType { Residential, Commercial, Enterprise, Government }
public enum CustomerTier { Standard, Gold, Platinum }
public enum AssetStatus { Operational, Degraded, Down, Retired }
public enum JobPriority { Critical, High, Medium, Low }
public enum JobCategory { Install, Repair, Maintenance, Inspection, Emergency }
public enum JobStatus { Unassigned, Assigned, Dispatched, EnRoute, OnSite, OnHold, Completed, Cancelled }
public enum JobSource { Phone, Portal, Email, IoTAlert, Contract }
public enum JobEventType { Created, Assigned, Unassigned, Rescheduled, StatusChanged, NoteAdded, SlaBreached, PartsUsed }
public enum NoteAuthorType { Dispatcher, Technician, System, Customer }
public enum RescheduleReason { CustomerRequest, TechnicianUnavailable, PartsDelay, Overrun, Weather, SlaRebalance, Other }

// ---------------------------------------------------------------- Entities
public class SeedMeta
{
    public int Id { get; set; }
    public DateOnly AnchorDate { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string GeneratorVersion { get; set; } = "";
}

public class Region
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string TimeZone { get; set; } = "";
    public double CenterLat { get; set; }
    public double CenterLng { get; set; }
    public List<Depot> Depots { get; set; } = [];
}

public class Depot
{
    public int Id { get; set; }
    public int RegionId { get; set; }
    public Region Region { get; set; } = null!;
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Phone { get; set; } = "";
}

public class Dispatcher
{
    public int Id { get; set; }
    public string EmployeeNumber { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public int RegionId { get; set; }
    public Region Region { get; set; } = null!;
    public DispatcherShift ShiftName { get; set; }
    public DispatcherRole Role { get; set; }
    public bool IsActive { get; set; }
}

public class Skill
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public bool RequiresCertification { get; set; }
}

public class Vehicle
{
    public int Id { get; set; }
    public int DepotId { get; set; }
    public Depot Depot { get; set; } = null!;
    public string PlateNumber { get; set; } = "";
    public string Make { get; set; } = "";
    public string Model { get; set; } = "";
    public int Year { get; set; }
    public VehicleType VehicleType { get; set; }
    public VehicleStatus Status { get; set; }
    public int OdometerMiles { get; set; }
}

public class Technician
{
    public int Id { get; set; }
    public string EmployeeNumber { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public int DepotId { get; set; }
    public Depot Depot { get; set; } = null!;
    public int? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public TechnicianLevel Level { get; set; }
    public EmploymentStatus EmploymentStatus { get; set; }
    public FieldStatus FieldStatus { get; set; }
    public DateOnly HireDate { get; set; }
    public double HourlyRate { get; set; }
    public int MaxJobsPerDay { get; set; }
    public double Rating { get; set; }
    public double HomeLatitude { get; set; }
    public double HomeLongitude { get; set; }
    public double? CurrentLatitude { get; set; }
    public double? CurrentLongitude { get; set; }
    public DateTime? LastLocationAt { get; set; }
    public List<TechnicianSkill> Skills { get; set; } = [];
    public List<Shift> Shifts { get; set; } = [];
    public List<TimeOffRequest> TimeOffRequests { get; set; } = [];
}

public class TechnicianSkill
{
    public int TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;
    public int SkillId { get; set; }
    public Skill Skill { get; set; } = null!;
    public int Proficiency { get; set; }
    public DateOnly? CertifiedUntil { get; set; }
}

public class Shift
{
    public int Id { get; set; }
    public int TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;
    public DateOnly ShiftDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public ShiftType ShiftType { get; set; }
    public ShiftStatus Status { get; set; }
    public string? Notes { get; set; }
}

public class TimeOffRequest
{
    public int Id { get; set; }
    public int TechnicianId { get; set; }
    public Technician Technician { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOffType Type { get; set; }
    public TimeOffStatus Status { get; set; }
    public string? Reason { get; set; }
    public DateTime RequestedAt { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public CustomerType CustomerType { get; set; }
    public CustomerTier Tier { get; set; }
    public string ContactName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public List<Site> Sites { get; set; } = [];
}

public class Site
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int RegionId { get; set; }
    public Region Region { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? AccessNotes { get; set; }
    public string? SiteContactPhone { get; set; }
    public List<Asset> Assets { get; set; } = [];
}

public class Asset
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;
    public string AssetTag { get; set; } = "";
    public string AssetType { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public DateOnly InstallDate { get; set; }
    public DateOnly? WarrantyExpires { get; set; }
    public AssetStatus Status { get; set; }
}

public class SlaPolicy
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public CustomerTier Tier { get; set; }
    public JobPriority Priority { get; set; }
    public int ResponseMinutes { get; set; }
    public int ResolutionMinutes { get; set; }
    public int AtRiskThresholdMinutes { get; set; }
    public double PenaltyAmount { get; set; }
}

public class JobType
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public JobCategory Category { get; set; }
    public int RequiredSkillId { get; set; }
    public Skill RequiredSkill { get; set; } = null!;
    public int MinProficiency { get; set; }
    public int DefaultDurationMinutes { get; set; }
    public double BasePrice { get; set; }
}

public class Part
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public double UnitCost { get; set; }
    public double UnitPrice { get; set; }
}

public class DepotInventory
{
    public int DepotId { get; set; }
    public Depot Depot { get; set; } = null!;
    public int PartId { get; set; }
    public Part Part { get; set; } = null!;
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; }
}

public class Job
{
    public int Id { get; set; }
    public string JobNumber { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;
    public int? AssetId { get; set; }
    public Asset? Asset { get; set; }
    public int JobTypeId { get; set; }
    public JobType JobType { get; set; } = null!;
    public int RegionId { get; set; }
    public Region Region { get; set; } = null!;
    public int SlaPolicyId { get; set; }
    public SlaPolicy SlaPolicy { get; set; } = null!;
    public JobPriority Priority { get; set; }
    public JobStatus Status { get; set; }
    public JobSource Source { get; set; }
    public string Summary { get; set; } = "";
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public DateTime ResponseDueAt { get; set; }
    public DateTime ResolutionDueAt { get; set; }
    public int? AssignedTechnicianId { get; set; }
    public Technician? AssignedTechnician { get; set; }
    public int? AssignedByDispatcherId { get; set; }
    public Dispatcher? AssignedByDispatcher { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public string? OnHoldReason { get; set; }
    public string? CompletionNotes { get; set; }
    public bool? FirstTimeFix { get; set; }
    public int? CustomerRating { get; set; }
    public int RescheduleCount { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<JobEvent> Events { get; set; } = [];
    public List<JobNote> Notes { get; set; } = [];
    public List<JobPart> Parts { get; set; } = [];
    public List<Reschedule> Reschedules { get; set; } = [];

    public bool IsOpen => Status is not (JobStatus.Completed or JobStatus.Cancelled);
}

public class JobEvent
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public Job Job { get; set; } = null!;
    public JobEventType EventType { get; set; }
    public JobStatus? FromStatus { get; set; }
    public JobStatus? ToStatus { get; set; }
    public int? TechnicianId { get; set; }
    public Technician? Technician { get; set; }
    public int? DispatcherId { get; set; }
    public Dispatcher? Dispatcher { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Details { get; set; }
}

public class JobNote
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public Job Job { get; set; } = null!;
    public NoteAuthorType AuthorType { get; set; }
    public string AuthorName { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsInternal { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class JobPart
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public Job Job { get; set; } = null!;
    public int PartId { get; set; }
    public Part Part { get; set; } = null!;
    public int Quantity { get; set; }
    public double UnitPrice { get; set; }
    public DateTime AddedAt { get; set; }
}

public class Reschedule
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public Job Job { get; set; } = null!;
    public int? FromTechnicianId { get; set; }
    public Technician? FromTechnician { get; set; }
    public int? ToTechnicianId { get; set; }
    public Technician? ToTechnician { get; set; }
    public DateTime? FromStart { get; set; }
    public DateTime? ToStart { get; set; }
    public RescheduleReason ReasonCode { get; set; }
    public string? Comment { get; set; }
    public int? DispatcherId { get; set; }
    public Dispatcher? Dispatcher { get; set; }
    public DateTime CreatedAt { get; set; }
}
