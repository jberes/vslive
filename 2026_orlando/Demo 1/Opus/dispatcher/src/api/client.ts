import { connection } from '../state/connection';
import type {
  Asset, AssignmentValidation, Customer, DispatchBoard, Dispatcher, JobDetail, JobListItem, JobNote, Kpis, Lookups,
  OpsClock, Paged, Recommendation, RescheduleReason, Site, SlaSummary, SlaTrendPoint, TimeOffRequest, JobStatus,
  JobPriority,
} from './types';

// Requests go through the Vite dev proxy (/api → http://localhost:5207). Override with VITE_API_BASE.
const BASE = (import.meta.env.VITE_API_BASE as string | undefined) ?? '/api';

/** RFC 7807 problem details, plus the validation issues the API attaches to 409s. */
export class ApiError extends Error {
  status: number;
  title: string;
  detail?: string;
  data?: unknown;
  constructor(status: number, title: string, detail?: string, data?: unknown) {
    super(detail || title);
    this.status = status;
    this.title = title;
    this.detail = detail;
    this.data = data;
  }
  get isConflict() { return this.status === 409; }
  get isNetwork() { return this.status === 0; }
}

type Query = Record<string, string | number | boolean | undefined | null | (string | number)[]>;

function qs(q?: Query) {
  if (!q) return '';
  const p = new URLSearchParams();
  for (const [k, v] of Object.entries(q)) {
    if (v === undefined || v === null || v === '') continue;
    if (Array.isArray(v)) v.forEach(x => p.append(k, String(x)));
    else p.append(k, String(v));
  }
  const s = p.toString();
  return s ? `?${s}` : '';
}

async function request<T>(method: string, path: string, query?: Query, body?: unknown, signal?: AbortSignal): Promise<T> {
  let res: Response;
  try {
    res = await fetch(`${BASE}${path}${qs(query)}`, {
      method,
      signal,
      headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch (e) {
    if ((e as Error).name === 'AbortError') throw e;
    connection.fail('Cannot reach the dispatch service');
    throw new ApiError(0, 'Network error', 'Cannot reach the dispatch service.');
  }
  if (res.status >= 500 || res.status === 502 || res.status === 504) {
    connection.fail(`Dispatch service error ${res.status}`);
  } else {
    connection.ok();
  }
  if (!res.ok) {
    let problem: { title?: string; detail?: string; data?: unknown; errors?: unknown } = {};
    try { problem = await res.json(); } catch { /* no body */ }
    throw new ApiError(res.status, problem.title ?? res.statusText, problem.detail, problem.data ?? problem.errors);
  }
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

const get = <T>(path: string, query?: Query, signal?: AbortSignal) => request<T>('GET', path, query, undefined, signal);
const post = <T>(path: string, body?: unknown) => request<T>('POST', path, undefined, body ?? {});

export interface JobQuery extends Query {
  date?: string;
  regionId?: number;
  status?: JobStatus[];
  priority?: JobPriority[];
  openOnly?: boolean;
  unassigned?: boolean;
  customerId?: number;
  siteId?: number;
  technicianId?: number;
  search?: string;
  sort?: string;
  page?: number;
  pageSize?: number;
}

export const api = {
  clock: (signal?: AbortSignal) => get<OpsClock>('/meta/clock', undefined, signal),
  lookups: () => get<Lookups>('/meta/lookups'),
  dispatchers: () => get<Dispatcher[]>('/dispatchers'),
  kpis: (date: string, regionId?: number, signal?: AbortSignal) => get<Kpis>('/dashboard/kpis', { date, regionId }, signal),

  jobs: (q: JobQuery, signal?: AbortSignal) => get<Paged<JobListItem>>('/jobs', q, signal),
  /** Fetches every page (API max pageSize is 500) in parallel after the first. */
  async allJobs(q: JobQuery, signal?: AbortSignal): Promise<JobListItem[]> {
    const first = await api.jobs({ ...q, page: 1, pageSize: 500 }, signal);
    if (first.totalPages <= 1) return first.items;
    const rest = await Promise.all(
      Array.from({ length: first.totalPages - 1 }, (_, i) => api.jobs({ ...q, page: i + 2, pageSize: 500 }, signal)),
    );
    return first.items.concat(...rest.map(r => r.items));
  },
  job: (id: number, signal?: AbortSignal) => get<JobDetail>(`/jobs/${id}`, undefined, signal),
  recommendations: (id: number, opts: { limit?: number; includeIneligible?: boolean; crossRegion?: boolean } = {}) =>
    get<Recommendation[]>(`/jobs/${id}/recommendations`, opts),
  validateAssignment: (id: number, technicianId: number, scheduledStart?: string) =>
    get<AssignmentValidation>(`/jobs/${id}/validate-assignment`, { technicianId, scheduledStart }),
  assign: (id: number, body: { technicianId: number; scheduledStart?: string; dispatcherId?: number; force?: boolean; note?: string }) =>
    post<{ job: JobDetail; validation: AssignmentValidation }>(`/jobs/${id}/assign`, body),
  unassign: (id: number, body: { dispatcherId?: number; reason?: string }) => post<JobDetail>(`/jobs/${id}/unassign`, body),
  reschedule: (id: number, body: {
    newScheduledStart: string; technicianId?: number; reasonCode: RescheduleReason; comment?: string;
    dispatcherId?: number; updateWindow: boolean; force?: boolean;
  }) => post<JobDetail>(`/jobs/${id}/reschedule`, body),
  setStatus: (id: number, body: { status: JobStatus; note?: string; dispatcherId?: number; onHoldReason?: string }) =>
    post<JobDetail>(`/jobs/${id}/status`, body),
  updateJob: (id: number, body: { priority?: JobPriority; summary?: string; description?: string }) =>
    request<JobDetail>('PATCH', `/jobs/${id}`, undefined, body),
  addNote: (id: number, body: { authorName: string; body: string; isInternal?: boolean }) =>
    post<JobNote>(`/jobs/${id}/notes`, { authorType: 'Dispatcher', isInternal: true, ...body }),
  createJob: (body: {
    siteId: number; assetId?: number; jobTypeId: number; priority: JobPriority; source: string; summary: string;
    description?: string; windowStart: string; windowEnd: string; estimatedDurationMinutes?: number; dispatcherId?: number;
  }) => post<JobDetail>('/jobs', body),

  board: (q: { date: string; regionId?: number; depotId?: number; skillId?: number }, signal?: AbortSignal) =>
    get<DispatchBoard>('/dispatch/board', q, signal),

  slaSummary: (date: string, regionId?: number, signal?: AbortSignal) => get<SlaSummary>('/sla/summary', { date, regionId }, signal),
  slaAtRisk: (q: { date?: string; regionId?: number; limit?: number }, signal?: AbortSignal) =>
    get<JobListItem[]>('/sla/at-risk', q, signal),
  slaTrend: (days: number, regionId?: number, signal?: AbortSignal) => get<SlaTrendPoint[]>('/sla/trend', { days, regionId }, signal),

  customers: (regionId?: number) => get<Paged<Customer>>('/customers', { regionId, pageSize: 500, sort: 'name' }),
  customerSites: (id: number) => get<Site[]>(`/customers/${id}/sites`),
  siteAssets: (id: number) => get<(Asset & { assetType: string })[]>(`/sites/${id}/assets`),
  techSchedule: (id: number, from: string, to: string) =>
    get<{ technicianId: number; technicianName: string; shifts: { id: number; shiftDate: string; startTime: string; endTime: string; status: string }[]; timeOff: unknown[]; jobs: JobListItem[] }>(
      `/technicians/${id}/schedule`, { from, to }),
  timeOff: (q: { status?: string; regionId?: number }, signal?: AbortSignal) => get<TimeOffRequest[]>('/time-off', q, signal),
};
