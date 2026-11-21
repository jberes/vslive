import { createContext, useContext } from 'react';

export type SlaState = 'OnTrack' | 'AtRisk' | 'Breached' | 'Met' | 'NotApplicable';

export interface Job {
  id: number;
  jobNumber: string;
  status: string;
  priority: 'Critical' | 'High' | 'Medium' | 'Low';
  summary: string;
  customerName: string;
  customerTier: string;
  siteName: string;
  siteCity: string;
  siteLatitude: number;
  siteLongitude: number;
  regionId: number;
  regionName: string;
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
  updatedAt: string;
  sla: {
    state: SlaState;
    stage: string;
    dueAt: string;
    minutesRemaining: number | null;
    reason: string | null;
  };
}

export interface JobDetail {
  job: Job;
  description: string;
  assetTag: string;
  assetDescription: string;
  siteAddress: string;
  siteAccessNotes: string | null;
  customerContact: string;
  customerPhone: string;
  events: Array<{ id: number; eventType: string; occurredAt: string; details: string }>;
  notes: Array<{ id: number; authorName: string; body: string; createdAt: string }>;
  reschedules: Array<{ id: number; reasonCode: string; comment: string | null; createdAt: string }>;
}

export interface Recommendation {
  technicianId: number;
  technicianName: string;
  level: string;
  fieldStatus: string;
  depotName: string;
  proficiency: number;
  distanceKm: number;
  travelMinutes: number;
  jobsToday: number;
  utilizationPct: number;
  earliestStart: string;
  meetsResponseSla: boolean;
  score: number;
  issues: Array<{ code: string; severity: 'Blocking' | 'Warning'; message: string }>;
}

export interface BoardTechnician {
  technicianId: number;
  technicianName: string;
  fieldStatus: string;
  depotName: string;
  skillCodes: string;
  shiftStart: string | null;
  shiftEnd: string | null;
  utilizationPct: number;
  jobs: Job[];
}

export interface Board {
  now: string;
  date: string;
  technicians: BoardTechnician[];
  unassigned: Job[];
}

export interface SlaSummary {
  now: string;
  counts: { total: number; onTrack: number; atRisk: number; breached: number; met: number };
  compliancePct: number;
  penaltyExposure: number;
  byPriority: Array<{ label: string; counts: { total: number; breached: number; atRisk: number } }>;
  byRegion: Array<{ label: string; counts: { total: number; breached: number; atRisk: number } }>;
  dueByHour: Array<{ hour: number; due: number; atRisk: number; breached: number }>;
}

export interface Region {
  id: number;
  name: string;
  code: string;
}

export interface DispatchState {
  jobs: Job[];
  riskJobs: Job[];
  totalJobs: number;
  board: Board | null;
  summary: SlaSummary | null;
  regions: Region[];
  selectedId: number | null;
  detail: JobDetail | null;
  recommendations: Recommendation[];
  loading: boolean;
  refreshing: boolean;
  error: string | null;
  message: string | null;
  lastUpdated: string | null;
  regionId: number | null;
  priority: string;
  status: string;
  search: string;
  setRegionId: (id: number | null) => void;
  setPriority: (priority: string) => void;
  setStatus: (status: string) => void;
  setSearch: (search: string) => void;
  selectJob: (id: number | null) => void;
  refresh: () => Promise<void>;
  loadRecommendations: (id: number) => Promise<void>;
  assignJob: (jobId: number, technicianId: number, scheduledStart: string) => Promise<void>;
  rescheduleJob: (jobId: number, technicianId: number, newStart: string) => Promise<void>;
  addHandoverNote: (jobId: number, body: string) => Promise<void>;
}

export const DispatchContext = createContext<DispatchState | null>(null);

export function useDispatch() {
  const value = useContext(DispatchContext);
  if (!value) throw new Error('DispatchContext is unavailable');
  return value;
}

export const API_BASE = '/api';

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  });
  if (!response.ok) {
    const body = await response.json().catch(() => null) as
      | { title?: string; detail?: string; data?: { issues?: Array<{ message: string }> } }
      | null;
    const issue = body?.data?.issues?.map((item) => item.message).join(' ');
    throw new Error(issue || body?.detail || body?.title || `Request failed (${response.status})`);
  }
  return await response.json() as T;
}

export function formatTime(value: string | null | undefined) {
  if (!value) return '—';
  return new Intl.DateTimeFormat('en-US', { hour: '2-digit', minute: '2-digit', hour12: false }).format(new Date(value));
}

export function formatDateTime(value: string | null | undefined) {
  if (!value) return 'Not scheduled';
  return new Intl.DateTimeFormat('en-US', {
    month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
  }).format(new Date(value));
}

export function slaLabel(job: Job) {
  const remaining = job.sla.minutesRemaining;
  if (remaining == null) return job.sla.state;
  if (job.sla.state === 'Breached') return remaining < 0 ? `${Math.abs(Math.round(remaining))}m overdue` : 'Breach recorded';
  if (remaining < 0) return `${Math.abs(Math.round(remaining))}m overdue`;
  return `${Math.round(remaining)}m left`;
}
