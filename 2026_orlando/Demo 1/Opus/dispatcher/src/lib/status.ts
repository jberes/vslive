import type { JobDetail, JobListItem, JobPriority, JobStatus, SlaState } from '../api/types';
import type { IconName } from '../components/Icon';

/**
 * One status language for every view. Each state carries a distinct icon SHAPE and text label, so it
 * reads without color. Tones: `crit` (red: breached / safety), `warn` (amber: at risk / attention),
 * `ok` (green: met), `neutral` (slate), `info` (blue).
 */
export type Tone = 'crit' | 'warn' | 'ok' | 'neutral' | 'info';

export const SLA_META: Record<SlaState, { label: string; icon: IconName; tone: Tone; rank: number }> = {
  Breached: { label: 'Breached', icon: 'octagon', tone: 'crit', rank: 0 },
  AtRisk: { label: 'At risk', icon: 'warning', tone: 'warn', rank: 1 },
  OnTrack: { label: 'On track', icon: 'dot', tone: 'neutral', rank: 2 },
  Met: { label: 'Met', icon: 'check_circle', tone: 'ok', rank: 3 },
  NotApplicable: { label: 'No target', icon: 'dash', tone: 'neutral', rank: 4 },
};

export const PRIORITY_META: Record<JobPriority, { label: string; icon: IconName; tone: Tone; rank: number }> = {
  Critical: { label: 'Critical', icon: 'diamond', tone: 'crit', rank: 0 },
  High: { label: 'High', icon: 'triangle', tone: 'warn', rank: 1 },
  Medium: { label: 'Medium', icon: 'square', tone: 'neutral', rank: 2 },
  Low: { label: 'Low', icon: 'ring', tone: 'neutral', rank: 3 },
};

export const STATUS_META: Record<JobStatus, { label: string; open: boolean; icon: IconName }> = {
  Unassigned: { label: 'Unassigned', open: true, icon: 'ring' },
  Assigned: { label: 'Assigned', open: true, icon: 'person' },
  Dispatched: { label: 'Dispatched', open: true, icon: 'send' },
  EnRoute: { label: 'En route', open: true, icon: 'route' },
  OnSite: { label: 'On site', open: true, icon: 'build' },
  OnHold: { label: 'On hold', open: true, icon: 'pause' },
  Completed: { label: 'Completed', open: false, icon: 'check_circle' },
  Cancelled: { label: 'Cancelled', open: false, icon: 'close' },
};

/** Workflow transitions the API accepts (invalid ones return 409). */
export const NEXT_STATUS: Partial<Record<JobStatus, JobStatus[]>> = {
  Assigned: ['Dispatched', 'OnHold'],
  Dispatched: ['EnRoute', 'OnHold'],
  EnRoute: ['OnSite', 'OnHold'],
  OnSite: ['Completed', 'OnHold'],
  OnHold: ['Assigned'],
};

export const isOpen = (j: JobListItem) => STATUS_META[j.status].open;

/**
 * Operational impact ordering: priority first (safety-critical faults), then SLA state, then how
 * little time is left. Used for the default ledger order and the "highest-impact problem" callout.
 */
export function impactCompare(a: JobListItem, b: JobListItem): number {
  return (
    PRIORITY_META[a.priority].rank - PRIORITY_META[b.priority].rank ||
    SLA_META[a.sla.state].rank - SLA_META[b.sla.state].rank ||
    (a.sla.minutesRemaining ?? 1e9) - (b.sla.minutesRemaining ?? 1e9)
  );
}

/**
 * Explains WHY a job is at risk, combining the API's SLA reason with remaining time, estimated work,
 * travel/assignment state and access constraints, as the brief requires.
 */
export function riskCause(j: JobListItem): string {
  const parts: string[] = [];
  if (j.sla.reason) parts.push(j.sla.reason);
  if (j.status === 'Unassigned') parts.push('no crew assigned');
  else if (j.status === 'OnHold') parts.push('job on hold');
  const left = j.sla.minutesRemaining;
  if (left !== null && left >= 0 && j.sla.stage === 'Resolution' && !j.actualStart && left < j.estimatedDurationMinutes) {
    parts.push(`work needs ${j.estimatedDurationMinutes} min, ${left} min left`);
  }
  if (j.scheduledStart && j.sla.stage === 'Response' && j.scheduledStart > (j.sla.dueAt ?? '')) {
    parts.push('scheduled after response target');
  }
  return parts.length ? capitalize(parts.join(' · ')) : '—';
}

/** Suggested next action for a job, used in the risk callout, SLA grid and handover. */
export function nextAction(j: JobListItem): string {
  switch (j.status) {
    case 'Unassigned': return 'Assign a qualified crew';
    case 'Assigned': return j.sla.state === 'OnTrack' ? 'Dispatch crew' : 'Dispatch now or reassign earlier crew';
    case 'Dispatched': return 'Confirm crew is en route';
    case 'EnRoute': return 'Confirm arrival and track access';
    case 'OnSite': return j.sla.state === 'OnTrack' ? 'Monitor restoration' : 'Check restoration ETA with crew';
    case 'OnHold': return 'Resolve hold (parts, access or approval)';
    default: return '—';
  }
}

const capitalize = (s: string) => s.charAt(0).toUpperCase() + s.slice(1);

/** Text that marks an operating restriction (as opposed to a service target). */
export const hasSafetyRestriction = (d: JobDetail) =>
  /safety restriction|speed restriction|out of service|track (?:closed|blocked)/i.test(d.description ?? '') ||
  d.job.jobCategory === 'Emergency';

