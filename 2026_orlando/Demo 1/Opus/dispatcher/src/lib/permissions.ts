import type { DispatcherRole, JobPriority } from '../api/types';

export const ROLE_LABEL: Record<DispatcherRole, string> = {
  Dispatcher: 'Dispatcher',
  SeniorDispatcher: 'Senior dispatcher',
  Supervisor: 'Shift supervisor',
};

const RANK: Record<DispatcherRole, number> = { Dispatcher: 1, SeniorDispatcher: 2, Supervisor: 3 };

export interface Permissions {
  role: DispatcherRole;
  /** Assign / reschedule / status / notes inside normal validation. */
  canDispatch: boolean;
  /** Override blocking validation issues (recorded with a reason). */
  canOverride: boolean;
  /** Reassign a crew's whole day. */
  canReassignDay: boolean;
  /** Whether this role may move a job from `from` priority to `to`. */
  canSetPriority: (from: JobPriority, to: JobPriority) => boolean;
}

export function permissionsFor(role: DispatcherRole): Permissions {
  const r = RANK[role];
  return {
    role,
    canDispatch: true,
    canOverride: r >= 3,
    canReassignDay: r >= 2,
    // Dispatchers adjust High/Medium/Low within their authority; setting or clearing Critical
    // needs a senior dispatcher or supervisor.
    canSetPriority: (from, to) => from === to || ((from !== 'Critical' && to !== 'Critical') || r >= 2),
  };
}

export const AUTHORITY_NOTE: Record<DispatcherRole, string> = {
  Dispatcher: 'Can assign, reschedule and change High/Medium/Low priority. Overrides need a supervisor.',
  SeniorDispatcher: 'Can also set or clear Critical priority and reassign a crew’s day. Overrides need a supervisor.',
  Supervisor: 'Full authority, including overriding blocking validation with a recorded reason.',
};
