import { ja } from '../i18n/ja';
import type { SlaState, JobPriority, JobStatus } from '../api/types';
import { fmtMinutes } from '../lib/format';

// State is shown with glyph + text, never colour alone (brief §5).
const SLA_GLYPH: Record<SlaState, string> = {
  OnTrack: '●',
  AtRisk: '▲',
  Breached: '✖',
  Met: '✔',
  NotApplicable: '–',
};

export function SlaChip({ state, minutes }: { state: SlaState; minutes?: number | null }) {
  const label = ja.slaState[state];
  return (
    <span className={`chip chip-sla chip-${state}`} aria-label={`SLA ${label}`}>
      <span aria-hidden="true">{SLA_GLYPH[state]}</span>
      <span>{label}</span>
      {minutes !== undefined && minutes !== null && state !== 'Met' && state !== 'NotApplicable' && <span className="chip-sub">{fmtMinutes(minutes)}</span>}
    </span>
  );
}

export function PriorityText({ priority }: { priority: JobPriority }) {
  const cls = priority === 'Critical' ? 'pri pri-critical' : priority === 'High' ? 'pri pri-high' : 'pri';
  return <span className={cls}>{priority === 'Critical' ? '■ ' : priority === 'High' ? '◆ ' : ''}{ja.priority[priority]}</span>;
}

export function StatusText({ status }: { status: JobStatus }) {
  return <span>{ja.status[status]}</span>;
}

export function Freshness({ stale, disconnected, onRetry }: {
  stale: boolean;
  disconnected: boolean;
  onRetry?: () => void;
}) {
  if (disconnected) {
    return (
      <div className="banner banner-error" role="alert">
        <span>{ja.state.disconnected}</span>
        {onRetry && <button type="button" className="link-btn" onClick={onRetry}>{ja.header.reconnect}</button>}
      </div>
    );
  }
  if (stale) {
    return <div className="banner banner-warn" role="status">▲ {ja.state.stale}</div>;
  }
  return null;
}

export function UpdatedAt({ at }: { at: number | null }) {
  if (!at) return <span className="muted">—</span>;
  const t = new Date(at).toLocaleTimeString('ja-JP', { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false });
  return <time dateTime={new Date(at).toISOString()} className="muted">{ja.header.refreshed} {t}</time>;
}
