-- =====================================================================
-- Field Service Dispatch Console - SQLite schema
-- Conventions:
--   * PascalCase tables/columns (maps 1:1 to EF Core entities)
--   * DateTimes stored as TEXT 'YYYY-MM-DD HH:MM:SS' in operations-local time
--   * Dates stored as TEXT 'YYYY-MM-DD', times as TEXT 'HH:MM:SS'
--   * Enums stored as TEXT (see CHECK constraints for allowed values)
-- =====================================================================
PRAGMA foreign_keys = ON;

CREATE TABLE SeedMeta (
    Id              INTEGER PRIMARY KEY CHECK (Id = 1),
    AnchorDate      TEXT    NOT NULL,          -- the "today" the data was generated for
    GeneratedAt     TEXT    NOT NULL,
    GeneratorVersion TEXT   NOT NULL
);

-- ---------------------------------------------------------------------
-- Organization
-- ---------------------------------------------------------------------
CREATE TABLE Regions (
    Id          INTEGER PRIMARY KEY,
    Code        TEXT    NOT NULL UNIQUE,
    Name        TEXT    NOT NULL,
    TimeZone    TEXT    NOT NULL,
    CenterLat   REAL    NOT NULL,
    CenterLng   REAL    NOT NULL
);

CREATE TABLE Depots (
    Id          INTEGER PRIMARY KEY,
    RegionId    INTEGER NOT NULL REFERENCES Regions(Id),
    Code        TEXT    NOT NULL UNIQUE,
    Name        TEXT    NOT NULL,
    Address     TEXT    NOT NULL,
    City        TEXT    NOT NULL,
    State       TEXT    NOT NULL,
    PostalCode  TEXT    NOT NULL,
    Latitude    REAL    NOT NULL,
    Longitude   REAL    NOT NULL,
    Phone       TEXT    NOT NULL
);
CREATE INDEX IX_Depots_RegionId ON Depots(RegionId);

CREATE TABLE Dispatchers (
    Id          INTEGER PRIMARY KEY,
    EmployeeNumber TEXT NOT NULL UNIQUE,
    FirstName   TEXT    NOT NULL,
    LastName    TEXT    NOT NULL,
    Email       TEXT    NOT NULL UNIQUE,
    Phone       TEXT    NOT NULL,
    RegionId    INTEGER NOT NULL REFERENCES Regions(Id),
    ShiftName   TEXT    NOT NULL CHECK (ShiftName IN ('Early','Day','Late','Night')),
    Role        TEXT    NOT NULL CHECK (Role IN ('Dispatcher','SeniorDispatcher','Supervisor')),
    IsActive    INTEGER NOT NULL DEFAULT 1
);
CREATE INDEX IX_Dispatchers_RegionId ON Dispatchers(RegionId);

-- ---------------------------------------------------------------------
-- Workforce
-- ---------------------------------------------------------------------
CREATE TABLE Skills (
    Id          INTEGER PRIMARY KEY,
    Code        TEXT    NOT NULL UNIQUE,
    Name        TEXT    NOT NULL,
    Category    TEXT    NOT NULL,
    RequiresCertification INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE Vehicles (
    Id          INTEGER PRIMARY KEY,
    DepotId     INTEGER NOT NULL REFERENCES Depots(Id),
    PlateNumber TEXT    NOT NULL UNIQUE,
    Make        TEXT    NOT NULL,
    Model       TEXT    NOT NULL,
    Year        INTEGER NOT NULL,
    VehicleType TEXT    NOT NULL CHECK (VehicleType IN ('Van','Pickup','BoxTruck','Car')),
    Status      TEXT    NOT NULL CHECK (Status IN ('InService','Maintenance','OutOfService')),
    OdometerMiles INTEGER NOT NULL
);
CREATE INDEX IX_Vehicles_DepotId ON Vehicles(DepotId);

CREATE TABLE Technicians (
    Id              INTEGER PRIMARY KEY,
    EmployeeNumber  TEXT    NOT NULL UNIQUE,
    FirstName       TEXT    NOT NULL,
    LastName        TEXT    NOT NULL,
    Email           TEXT    NOT NULL UNIQUE,
    Phone           TEXT    NOT NULL,
    DepotId         INTEGER NOT NULL REFERENCES Depots(Id),
    VehicleId       INTEGER NULL REFERENCES Vehicles(Id),
    Level           TEXT    NOT NULL CHECK (Level IN ('Apprentice','TechI','TechII','Senior','Lead')),
    EmploymentStatus TEXT   NOT NULL CHECK (EmploymentStatus IN ('Active','OnLeave','Inactive')),
    FieldStatus     TEXT    NOT NULL CHECK (FieldStatus IN ('Available','EnRoute','OnSite','OnBreak','Offline')),
    HireDate        TEXT    NOT NULL,
    HourlyRate      REAL    NOT NULL,
    MaxJobsPerDay   INTEGER NOT NULL,
    Rating          REAL    NOT NULL,
    HomeLatitude    REAL    NOT NULL,
    HomeLongitude   REAL    NOT NULL,
    CurrentLatitude REAL    NULL,
    CurrentLongitude REAL   NULL,
    LastLocationAt  TEXT    NULL
);
CREATE INDEX IX_Technicians_DepotId ON Technicians(DepotId);
CREATE INDEX IX_Technicians_Status ON Technicians(EmploymentStatus, FieldStatus);

CREATE TABLE TechnicianSkills (
    TechnicianId    INTEGER NOT NULL REFERENCES Technicians(Id) ON DELETE CASCADE,
    SkillId         INTEGER NOT NULL REFERENCES Skills(Id),
    Proficiency     INTEGER NOT NULL CHECK (Proficiency BETWEEN 1 AND 5),
    CertifiedUntil  TEXT    NULL,
    PRIMARY KEY (TechnicianId, SkillId)
);
CREATE INDEX IX_TechnicianSkills_SkillId ON TechnicianSkills(SkillId);

CREATE TABLE Shifts (
    Id              INTEGER PRIMARY KEY,
    TechnicianId    INTEGER NOT NULL REFERENCES Technicians(Id) ON DELETE CASCADE,
    ShiftDate       TEXT    NOT NULL,
    StartTime       TEXT    NOT NULL,
    EndTime         TEXT    NOT NULL,
    ShiftType       TEXT    NOT NULL CHECK (ShiftType IN ('Regular','Overtime','OnCall')),
    Status          TEXT    NOT NULL CHECK (Status IN ('Scheduled','InProgress','Completed','Absent','Cancelled')),
    Notes           TEXT    NULL,
    UNIQUE (TechnicianId, ShiftDate, StartTime)
);
CREATE INDEX IX_Shifts_Date ON Shifts(ShiftDate);

CREATE TABLE TimeOffRequests (
    Id              INTEGER PRIMARY KEY,
    TechnicianId    INTEGER NOT NULL REFERENCES Technicians(Id) ON DELETE CASCADE,
    StartDate       TEXT    NOT NULL,
    EndDate         TEXT    NOT NULL,
    Type            TEXT    NOT NULL CHECK (Type IN ('Vacation','Sick','Training','Personal')),
    Status          TEXT    NOT NULL CHECK (Status IN ('Pending','Approved','Denied')),
    Reason          TEXT    NULL,
    RequestedAt     TEXT    NOT NULL
);
CREATE INDEX IX_TimeOff_Tech ON TimeOffRequests(TechnicianId, StartDate);

-- ---------------------------------------------------------------------
-- Customers
-- ---------------------------------------------------------------------
CREATE TABLE Customers (
    Id              INTEGER PRIMARY KEY,
    AccountNumber   TEXT    NOT NULL UNIQUE,
    Name            TEXT    NOT NULL,
    CustomerType    TEXT    NOT NULL CHECK (CustomerType IN ('Residential','Commercial','Enterprise','Government')),
    Tier            TEXT    NOT NULL CHECK (Tier IN ('Standard','Gold','Platinum')),
    ContactName     TEXT    NOT NULL,
    Phone           TEXT    NOT NULL,
    Email           TEXT    NOT NULL,
    CreatedAt       TEXT    NOT NULL,
    IsActive        INTEGER NOT NULL DEFAULT 1
);
CREATE INDEX IX_Customers_Name ON Customers(Name);

CREATE TABLE Sites (
    Id              INTEGER PRIMARY KEY,
    CustomerId      INTEGER NOT NULL REFERENCES Customers(Id) ON DELETE CASCADE,
    RegionId        INTEGER NOT NULL REFERENCES Regions(Id),
    Name            TEXT    NOT NULL,
    Address         TEXT    NOT NULL,
    City            TEXT    NOT NULL,
    State           TEXT    NOT NULL,
    PostalCode      TEXT    NOT NULL,
    Latitude        REAL    NOT NULL,
    Longitude       REAL    NOT NULL,
    AccessNotes     TEXT    NULL,
    SiteContactPhone TEXT   NULL
);
CREATE INDEX IX_Sites_CustomerId ON Sites(CustomerId);
CREATE INDEX IX_Sites_RegionId ON Sites(RegionId);

CREATE TABLE Assets (
    Id              INTEGER PRIMARY KEY,
    SiteId          INTEGER NOT NULL REFERENCES Sites(Id) ON DELETE CASCADE,
    AssetTag        TEXT    NOT NULL UNIQUE,
    AssetType       TEXT    NOT NULL,
    Manufacturer    TEXT    NOT NULL,
    Model           TEXT    NOT NULL,
    SerialNumber    TEXT    NOT NULL,
    InstallDate     TEXT    NOT NULL,
    WarrantyExpires TEXT    NULL,
    Status          TEXT    NOT NULL CHECK (Status IN ('Operational','Degraded','Down','Retired'))
);
CREATE INDEX IX_Assets_SiteId ON Assets(SiteId);

-- ---------------------------------------------------------------------
-- Service catalogue & SLA
-- ---------------------------------------------------------------------
CREATE TABLE SlaPolicies (
    Id                  INTEGER PRIMARY KEY,
    Name                TEXT    NOT NULL,
    Tier                TEXT    NOT NULL CHECK (Tier IN ('Standard','Gold','Platinum')),
    Priority            TEXT    NOT NULL CHECK (Priority IN ('Critical','High','Medium','Low')),
    ResponseMinutes     INTEGER NOT NULL,
    ResolutionMinutes   INTEGER NOT NULL,
    AtRiskThresholdMinutes INTEGER NOT NULL,
    PenaltyAmount       REAL    NOT NULL,
    UNIQUE (Tier, Priority)
);

CREATE TABLE JobTypes (
    Id                  INTEGER PRIMARY KEY,
    Code                TEXT    NOT NULL UNIQUE,
    Name                TEXT    NOT NULL,
    Category            TEXT    NOT NULL CHECK (Category IN ('Install','Repair','Maintenance','Inspection','Emergency')),
    RequiredSkillId     INTEGER NOT NULL REFERENCES Skills(Id),
    MinProficiency      INTEGER NOT NULL DEFAULT 1,
    DefaultDurationMinutes INTEGER NOT NULL,
    BasePrice           REAL    NOT NULL
);

CREATE TABLE Parts (
    Id          INTEGER PRIMARY KEY,
    Sku         TEXT    NOT NULL UNIQUE,
    Name        TEXT    NOT NULL,
    Category    TEXT    NOT NULL,
    UnitCost    REAL    NOT NULL,
    UnitPrice   REAL    NOT NULL
);

CREATE TABLE DepotInventory (
    DepotId         INTEGER NOT NULL REFERENCES Depots(Id),
    PartId          INTEGER NOT NULL REFERENCES Parts(Id),
    QuantityOnHand  INTEGER NOT NULL,
    ReorderLevel    INTEGER NOT NULL,
    PRIMARY KEY (DepotId, PartId)
);

-- ---------------------------------------------------------------------
-- Jobs (work orders)
-- ---------------------------------------------------------------------
CREATE TABLE Jobs (
    Id                  INTEGER PRIMARY KEY,
    JobNumber           TEXT    NOT NULL UNIQUE,
    CustomerId          INTEGER NOT NULL REFERENCES Customers(Id),
    SiteId              INTEGER NOT NULL REFERENCES Sites(Id),
    AssetId             INTEGER NULL REFERENCES Assets(Id),
    JobTypeId           INTEGER NOT NULL REFERENCES JobTypes(Id),
    RegionId            INTEGER NOT NULL REFERENCES Regions(Id),
    SlaPolicyId         INTEGER NOT NULL REFERENCES SlaPolicies(Id),
    Priority            TEXT    NOT NULL CHECK (Priority IN ('Critical','High','Medium','Low')),
    Status              TEXT    NOT NULL CHECK (Status IN ('Unassigned','Assigned','Dispatched','EnRoute','OnSite','OnHold','Completed','Cancelled')),
    Source              TEXT    NOT NULL CHECK (Source IN ('Phone','Portal','Email','IoTAlert','Contract')),
    Summary             TEXT    NOT NULL,
    Description         TEXT    NULL,
    CreatedAt           TEXT    NOT NULL,
    WindowStart         TEXT    NOT NULL,      -- customer-promised arrival window
    WindowEnd           TEXT    NOT NULL,
    ScheduledStart      TEXT    NULL,
    ScheduledEnd        TEXT    NULL,
    EstimatedDurationMinutes INTEGER NOT NULL,
    ResponseDueAt       TEXT    NOT NULL,      -- SLA: technician must be on site by
    ResolutionDueAt     TEXT    NOT NULL,      -- SLA: job must be completed by
    AssignedTechnicianId INTEGER NULL REFERENCES Technicians(Id),
    AssignedByDispatcherId INTEGER NULL REFERENCES Dispatchers(Id),
    AssignedAt          TEXT    NULL,
    ActualStart         TEXT    NULL,           -- arrived on site
    ActualEnd           TEXT    NULL,
    OnHoldReason        TEXT    NULL,
    CompletionNotes     TEXT    NULL,
    FirstTimeFix        INTEGER NULL,
    CustomerRating      INTEGER NULL CHECK (CustomerRating BETWEEN 1 AND 5),
    RescheduleCount     INTEGER NOT NULL DEFAULT 0,
    UpdatedAt           TEXT    NOT NULL
);
CREATE INDEX IX_Jobs_Status ON Jobs(Status);
CREATE INDEX IX_Jobs_ScheduledStart ON Jobs(ScheduledStart);
CREATE INDEX IX_Jobs_WindowStart ON Jobs(WindowStart);
CREATE INDEX IX_Jobs_ResolutionDueAt ON Jobs(ResolutionDueAt);
CREATE INDEX IX_Jobs_Tech ON Jobs(AssignedTechnicianId, ScheduledStart);
CREATE INDEX IX_Jobs_Region ON Jobs(RegionId, WindowStart);
CREATE INDEX IX_Jobs_Customer ON Jobs(CustomerId);

CREATE TABLE JobEvents (
    Id              INTEGER PRIMARY KEY,
    JobId           INTEGER NOT NULL REFERENCES Jobs(Id) ON DELETE CASCADE,
    EventType       TEXT    NOT NULL CHECK (EventType IN ('Created','Assigned','Unassigned','Rescheduled','StatusChanged','NoteAdded','SlaBreached','PartsUsed')),
    FromStatus      TEXT    NULL,
    ToStatus        TEXT    NULL,
    TechnicianId    INTEGER NULL REFERENCES Technicians(Id),
    DispatcherId    INTEGER NULL REFERENCES Dispatchers(Id),
    OccurredAt      TEXT    NOT NULL,
    Details         TEXT    NULL
);
CREATE INDEX IX_JobEvents_JobId ON JobEvents(JobId, OccurredAt);

CREATE TABLE JobNotes (
    Id              INTEGER PRIMARY KEY,
    JobId           INTEGER NOT NULL REFERENCES Jobs(Id) ON DELETE CASCADE,
    AuthorType      TEXT    NOT NULL CHECK (AuthorType IN ('Dispatcher','Technician','System','Customer')),
    AuthorName      TEXT    NOT NULL,
    Body            TEXT    NOT NULL,
    IsInternal      INTEGER NOT NULL DEFAULT 1,
    CreatedAt       TEXT    NOT NULL
);
CREATE INDEX IX_JobNotes_JobId ON JobNotes(JobId);

CREATE TABLE JobParts (
    Id              INTEGER PRIMARY KEY,
    JobId           INTEGER NOT NULL REFERENCES Jobs(Id) ON DELETE CASCADE,
    PartId          INTEGER NOT NULL REFERENCES Parts(Id),
    Quantity        INTEGER NOT NULL CHECK (Quantity > 0),
    UnitPrice       REAL    NOT NULL,
    AddedAt         TEXT    NOT NULL
);
CREATE INDEX IX_JobParts_JobId ON JobParts(JobId);

CREATE TABLE Reschedules (
    Id                  INTEGER PRIMARY KEY,
    JobId               INTEGER NOT NULL REFERENCES Jobs(Id) ON DELETE CASCADE,
    FromTechnicianId    INTEGER NULL REFERENCES Technicians(Id),
    ToTechnicianId      INTEGER NULL REFERENCES Technicians(Id),
    FromStart           TEXT    NULL,
    ToStart             TEXT    NULL,
    ReasonCode          TEXT    NOT NULL CHECK (ReasonCode IN ('CustomerRequest','TechnicianUnavailable','PartsDelay','Overrun','Weather','SlaRebalance','Other')),
    Comment             TEXT    NULL,
    DispatcherId        INTEGER NULL REFERENCES Dispatchers(Id),
    CreatedAt           TEXT    NOT NULL
);
CREATE INDEX IX_Reschedules_JobId ON Reschedules(JobId);
CREATE INDEX IX_Reschedules_CreatedAt ON Reschedules(CreatedAt);
