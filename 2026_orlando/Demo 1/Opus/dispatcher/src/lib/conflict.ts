import { api, ApiError } from '../api/client';
import type { AssignmentIssue, JobDetail, JobListItem } from '../api/types';
import { outcomes } from '../state/outcomeStore';
import { STATUS_META } from './status';
import { dayTime } from './time';

/** A change made by someone else between loading a job and saving over it. */
export interface FieldChange {
  field: string;
  mine: string;
  theirs: string;
}

export class ConflictError extends Error {
  latest: JobDetail;
  changes: FieldChange[];
  changedBy: string | null;
  constructor(latest: JobDetail, changes: FieldChange[], changedBy: string | null) {
    super('This job was changed by someone else since you opened it.');
    this.latest = latest;
    this.changes = changes;
    this.changedBy = changedBy;
  }
}

function diff(seen: JobListItem, now: JobListItem): FieldChange[] {
  const out: FieldChange[] = [];
  const add = (field: string, a: string, b: string) => a !== b && out.push({ field, mine: a, theirs: b });
  add('Status', STATUS_META[seen.status].label, STATUS_META[now.status].label);
  add('Crew', seen.assignedTechnicianName ?? 'Unassigned', now.assignedTechnicianName ?? 'Unassigned');
  add('Scheduled start', dayTime(seen.scheduledStart), dayTime(now.scheduledStart));
  add('Priority', seen.priority, now.priority);
  if (!out.length) add('Last updated', dayTime(seen.updatedAt), dayTime(now.updatedAt));
  return out;
}

/**
 * Optimistic-concurrency check for 40 simultaneous dispatchers. The API has no row versions, so
 * re-read the job right before saving and compare `updatedAt`; throw ConflictError if it moved.
 */
export async function ensureUnchanged(seen: JobListItem): Promise<JobDetail> {
  const latest = await api.job(seen.id);
  if (latest.job.updatedAt !== seen.updatedAt) {
    const lastEvent = [...latest.events].sort((a, b) => b.occurredAt.localeCompare(a.occurredAt))[0];
    const by = lastEvent?.dispatcherName ?? lastEvent?.technicianName ?? null;
    throw new ConflictError(latest, diff(seen, latest.job), by);
  }
  return latest;
}

export type SaveResult<T> =
  | { kind: 'saved'; value: T }
  | { kind: 'conflict'; error: ConflictError }
  | { kind: 'blocked'; issues: AssignmentIssue[]; message: string }
  | { kind: 'failed'; message: string };

/**
 * Runs a mutation with the conflict check and converts every ending into an explicit outcome
 * (posted to the outcome tray) so no save result is silent.
 */
export async function save<T>(opts: {
  seen?: JobListItem;
  skipCheck?: boolean;
  action: () => Promise<T>;
  success: (v: T) => { title: string; detail?: string };
  label: string;
}): Promise<SaveResult<T>> {
  try {
    if (opts.seen && !opts.skipCheck) await ensureUnchanged(opts.seen);
    const value = await opts.action();
    outcomes.push({ tone: 'ok', ...opts.success(value) });
    return { kind: 'saved', value };
  } catch (e) {
    if (e instanceof ConflictError) {
      outcomes.push({
        tone: 'warn',
        title: `${opts.label} not saved — conflicting update`,
        detail: `${e.changedBy ? `${e.changedBy} changed` : 'Someone changed'} this job first. Review the latest version.`,
      });
      return { kind: 'conflict', error: e };
    }
    if (e instanceof ApiError && e.isConflict) {
      const issues = Array.isArray(e.data) ? (e.data as AssignmentIssue[]) : [];
      const message = issues.length ? issues.map(i => i.message).join(' ') : e.message;
      outcomes.push({ tone: 'crit', title: `${opts.label} blocked by validation`, detail: message });
      return { kind: 'blocked', issues, message };
    }
    const message = (e as Error).message;
    outcomes.push({ tone: 'crit', title: `${opts.label} failed — nothing was changed`, detail: message });
    return { kind: 'failed', message };
  }
}
