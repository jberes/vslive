// Types mirror the Dispatch API DTOs (Backend/DispatchApi/Dtos/Dtos.cs). Timestamps are
// operations-local ISO strings without an offset (EST), e.g. "2026-11-17T13:45:00".

export type JobStatus =
  | 'Unassigned' | 'Assigned' | 'Dispatched' | 'EnRoute' | 'OnSite' | 'OnHold' | 'Completed' | 'Cancelled';
export type JobPriority = 'Critical' | 'High' | 'Medium' | 'Low';
export type SlaState = 'OnTrack' | 'AtRisk' | 'Breached' | 'Met' | 'NotApplicable';
export type SlaStage = 'Response' | 'Resolution' | 'Closed';
export type RescheduleReason =
  | 'CustomerRequest' | 'TechnicianUnavailable' | 'PartsDelay' | 'Overrun' | 'Weather' | 'SlaRebalance' | 'Other';
export type DispatcherRole = 'Dispatcher' | 'SeniorDispatcher' | 'Supervisor';
export type FieldStatus = 'Available' | 'EnRoute' | 'OnSite' | 'OnBreak' | 'Offline';

export interface SlaStatus {
  state: SlaState;
  stage: SlaStage;
  dueAt: string | null;
  minutesRemaining: number | null;
  projectedAt: string | null;
  reason: string | null;
  responseMet: boolean | null;
  resolutionMet: boolean | null;
}

export interface JobListItem {
  id: number;
  jobNumber: string;
  status: JobStatus;
  priority: JobPriority;
  summary: string;
  customerId: number;
  customerName: string;
  customerTier: string;
  siteId: number;
  siteName: string;
  siteCity: string;
  siteLatitude: number;
  siteLongitude: number;
  regionId: number;
  regionName: string;
  jobTypeId: number;
  jobTypeName: string;
  jobCategory: string;
  requiredSkillId: number;
  requiredSkillName: string;
  windowStart: string;
  windowEnd: string;
  scheduledStart: string | null;
  scheduledEnd: string | null;
  estimatedDurationMinutes: number;
  responseDueAt: string;
  resolutionDueAt: string;
  assignedTechnicianId: number | null;
  assignedTechnicianName: string | null;
  actualStart: string | null;
  actualEnd: string | null;
  rescheduleCount: number;
  createdAt: string;
  updatedAt: string;
  sla: SlaStatus;
}

export interface JobEvent {
  id: number;
  eventType: string;
  fromStatus: JobStatus | null;
  toStatus: JobStatus | null;
  technicianId: number | null;
  technicianName: string | null;
  dispatcherId: number | null;
  dispatcherName: string | null;
  occurredAt: string;
  details: string | null;
}

export interface JobNote {
  id: number;
  authorType: 'Dispatcher' | 'Technician' | 'System' | 'Customer';
  authorName: string;
  body: string;
  isInternal: boolean;
  createdAt: string;
}

export interface Reschedule {
  id: number;
  jobId: number;
  jobNumber: string;
  fromTechnicianId: number | null;
  fromTechnicianName: string | null;
  toTechnicianId: number | null;
  toTechnicianName: string | null;
  fromStart: string | null;
  toStart: string | null;
  reasonCode: RescheduleReason;
  comment: string | null;
  dispatcherId: number | null;
  dispatcherName: string | null;
  createdAt: string;
}

export interface JobDetail {
  job: JobListItem;
  description: string | null;
  source: string;
  slaPolicyId: number;
  slaPolicyName: string;
  assetId: number | null;
  assetTag: string | null;
  assetDescription: string | null;
  siteAddress: string;
  siteAccessNotes: string | null;
  siteContactPhone: string | null;
  customerPhone: string;
  customerContact: string;
  assignedByDispatcherId: number | null;
  assignedByDispatcherName: string | null;
  assignedAt: string | null;
  onHoldReason: string | null;
  completionNotes: string | null;
  partsTotal: number;
  events: JobEvent[];
  notes: JobNote[];
  reschedules: Reschedule[];
}

export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export type IssueSeverity = 'Warning' | 'Blocking';
export interface AssignmentIssue {
  code: string;
  severity: IssueSeverity;
  message: string;
}
export interface AssignmentValidation {
  canAssign: boolean;
  proposedStart: string;
  proposedEnd: string;
  issues: AssignmentIssue[];
}

export interface Recommendation {
  technicianId: number;
  technicianName: string;
  level: string;
  fieldStatus: FieldStatus;
  depotName: string;
  proficiency: number;
  distanceKm: number;
  travelMinutes: number;
  jobsToday: number;
  maxJobsPerDay: number;
  utilizationPct: number;
  shiftStart: string | null;
  shiftEnd: string | null;
  earliestStart: string | null;
  meetsResponseSla: boolean;
  score: number;
  issues: AssignmentIssue[];
}

export interface BoardTechnician {
  technicianId: number;
  technicianName: string;
  level: string;
  fieldStatus: FieldStatus;
  depotId: number;
  depotName: string;
  skillCodes: string;
  shiftStart: string | null;
  shiftEnd: string | null;
  shiftStatus: string | null;
  onTimeOff: boolean;
  bookedMinutes: number;
  utilizationPct: number;
  jobs: JobListItem[];
}

export interface DispatchBoard {
  date: string;
  now: string;
  regionId: number | null;
  technicians: BoardTechnician[];
  unassigned: JobListItem[];
}

export interface SlaCounts {
  total: number;
  onTrack: number;
  atRisk: number;
  breached: number;
  met: number;
}
export interface SlaGroup {
  key: string;
  label: string;
  counts: SlaCounts;
  penaltyExposure: number;
}
export interface SlaSummary {
  date: string;
  now: string;
  regionId: number | null;
  counts: SlaCounts;
  compliancePct: number;
  penaltyExposure: number;
  byPriority: SlaGroup[];
  byRegion: SlaGroup[];
  byTier: SlaGroup[];
  dueByHour: { hour: number; due: number; atRisk: number; breached: number }[];
}
export interface SlaTrendPoint {
  date: string;
  completed: number;
  responseMet: number;
  resolutionMet: number;
  responseCompliancePct: number;
  resolutionCompliancePct: number;
  breaches: number;
}

export interface Kpis {
  date: string;
  now: string;
  jobsToday: number;
  completed: number;
  inProgress: number;
  unassigned: number;
  onHold: number;
  techniciansOnShift: number;
  techniciansAvailable: number;
  techniciansOnSite: number;
  techniciansEnRoute: number;
  utilizationPct: number;
  atRisk: number;
  breached: number;
  reschedulesToday: number;
}

export interface Region {
  id: number;
  code: string;
  name: string;
  centerLat: number;
  centerLng: number;
}
export interface Depot {
  id: number;
  name: string;
  regionId: number;
}
export interface Skill {
  id: number;
  code: string;
  name: string;
  category: string;
  requiresCertification: boolean;
}
export interface JobType {
  id: number;
  code: string;
  name: string;
  category: string;
  requiredSkillId: number;
  requiredSkillName: string;
  minProficiency: number;
  defaultDurationMinutes: number;
}
export interface Lookups {
  regions: Region[];
  depots: Depot[];
  skills: Skill[];
  jobTypes: JobType[];
  enums: Record<string, string[]>;
}

export interface Dispatcher {
  id: number;
  employeeNumber: string;
  fullName: string;
  regionId: number;
  regionName: string;
  shiftName: string;
  role: DispatcherRole;
  isActive: boolean;
}

export interface OpsClock {
  now: string;
  today: string;
  isSimulated: boolean;
}

export interface Customer {
  id: number;
  name: string;
  tier: string;
  regionId: number;
}
export interface Site {
  id: number;
  name: string;
  city: string;
  address?: string;
  accessNotes?: string | null;
  customerId: number;
}
export interface Asset {
  id: number;
  assetTag: string;
  description?: string;
  model?: string;
  status: string;
}

export interface TimeOffRequest {
  id: number;
  technicianId: number;
  technicianName: string;
  type: string;
  status: string;
  startDate?: string;
  endDate?: string;
  start?: string;
  end?: string;
  reason?: string | null;
}
