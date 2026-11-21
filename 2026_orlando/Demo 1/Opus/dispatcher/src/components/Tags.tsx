import type { JobListItem, JobPriority, JobStatus, SlaState } from '../api/types';
import { PRIORITY_META, SLA_META, STATUS_META } from '../lib/status';
import { remaining } from '../lib/time';
import { Icon } from './Icon';

export function SlaTag({ state, compact }: { state: SlaState; compact?: boolean }) {
  const m = SLA_META[state];
  return (
    <span className={`tag tone-${m.tone} ${compact ? 'tag-compact' : ''}`}>
      <Icon name={m.icon} />
      {m.label}
    </span>
  );
}

export function PriorityTag({ priority }: { priority: JobPriority }) {
  const m = PRIORITY_META[priority];
  return (
    <span className={`prio prio-${priority.toLowerCase()}`}>
      <Icon name={m.icon} />
      {m.label}
    </span>
  );
}

export function StatusText({ status }: { status: JobStatus }) {
  const m = STATUS_META[status];
  return (
    <span className={`status status-${status.toLowerCase()}`}>
      <Icon name={m.icon} />
      {m.label}
    </span>
  );
}

/** "Breached · 45 min overdue (Response)" — the SLA cell used across grids. */
export function SlaCell({ job }: { job: JobListItem }) {
  const s = job.sla;
  const timeText = s.state === 'Met' ? 'Target met' : s.minutesRemaining === null ? 'No target' : remaining(s.minutesRemaining);
  return (
    <div className="cell2">
      <span className="l1"><SlaTag state={s.state} compact /></span>
      <span className={`l2 ${s.minutesRemaining !== null && s.minutesRemaining < 0 ? 'txt-crit' : ''}`}>
        {timeText}{s.stage !== 'Closed' ? ` · ${s.stage.toLowerCase()}` : ''}
      </span>
    </div>
  );
}
