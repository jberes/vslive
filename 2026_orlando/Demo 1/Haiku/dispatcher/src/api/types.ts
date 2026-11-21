// Subset of the DispatchApi contract used by the console (see Backend openapi).
export type JobStatus = 'Unassigned' | 'Assigned' | 'Dispatched' | 'EnRoute' | 'OnSite' | 'OnHold' | 'Completed' | 'Cancelled';
export type JobPriority = 'Critical' | 'High' | 'Medium' | 'Low';
export type SlaState = 'OnTrack' | 'AtRisk' | 'Breached' | 'Met' | 'NotApplicable';
export type DispatcherRole = 'Dispatcher' | 'SeniorDispatcher' | 'Supervisor';
export type RescheduleReason = 'CustomerRequest' | 'TechnicianUnavailable' | 'PartsDelay' | 'Overrun' | 'Weather' | 'SlaRebalance' | 'Other';

export interface SlaStatus {
  state: SlaState;
  stage: 'Response' | 'Resolution' | 'Closed';
  dueAt: string | null;
  minutesRemaining: number | null;
  projectedAt: string | null;
  reason: string | null;
}

export interface Job {
  id: number;
  jobNumber: string;
  status: JobStatus;
  priority: JobPriority;
  summary: string;
  customerName: string;
  customerTier: string;
  siteId: number;
  siteName: string;
  siteCity: string;
  regionId: number;
  regionName: string;
  jobTypeName: string;
  jobCategory: string;
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
  rescheduleCount: number;
  createdAt: string;
  updatedAt: string;
  sla: SlaStatus;
}

export interface Paged<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface JobDetail {
  job: Job;
  siteAddress: string;
  siteAccessNotes: string | null;
  siteContactPhone: string | null;
  customerContact: string;
  customerPhone: string;
  assetTag: string | null;
  onHoldReason: string | null;
  notes: { id: number; authorName: string; body: string; isInternal: boolean; createdAt: string }[];
}

export interface Recommendation {
  technicianId: number;
  technicianName: string;
  level: string;
  fieldStatus: string;
  depotName: string;
  proficiency: number;
  travelMinutes: number;
  jobsToday: number;
  maxJobsPerDay: number;
  utilizationPct: number;
  shiftStart: string | null;
  shiftEnd: string | null;
  earliestStart: string | null;
  meetsResponseSla: boolean;
  score: number;
  issues: { code: string; severity: string; message: string }[];
}

export interface Validation {
  canAssign: boolean;
  proposedStart: string;
  proposedEnd: string;
  issues: { code: string; severity: string; message: string }[];
}

export interface BoardTechnician {
  technicianId: number;
  technicianName: string;
  level: string;
  fieldStatus: string;
  depotName: string;
  skillCodes: string;
  shiftStart: string | null;
  shiftEnd: string | null;
  shiftStatus: string | null;
  onTimeOff: boolean;
  bookedMinutes: number;
  utilizationPct: number;
  jobs: Job[];
}

export interface DispatchBoard {
  date: string;
  now: string;
  technicians: BoardTechnician[];
  unassigned: Job[];
}

export interface Dispatcher {
  id: number;
  fullName: string;
  regionId: number;
  regionName: string;
  role: DispatcherRole;
  shiftName: string;
  isActive: boolean;
}

export interface SlaSummary {
  date: string;
  now: string;
  counts: { total: number; onTrack: number; atRisk: number; breached: number; met: number };
  compliancePct: number;
  penaltyExposure: number;
  byPriority: { key: string; label: string; counts: SlaSummary['counts'] }[];
  dueByHour: { hour: number; atRisk: number; breached: number }[];
}

export interface TrendPoint { date: string; responseCompliancePct: number; resolutionCompliancePct: number }

export interface Clock { now: string; today: string; isSimulated: boolean }
export interface Region { id: number; code: string; name: string }
export interface Site { id: number; name: string; city: string; customerId: number }
export interface JobType { id: number; name: string; category: string; requiredSkillId: number }
