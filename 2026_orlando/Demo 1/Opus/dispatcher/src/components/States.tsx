import { IgrButton, IgrLinearProgress } from 'igniteui-react';
import type { ReactNode } from 'react';
import { STALE_MS, useNow } from '../state/usePolling';
import { ago, hms } from '../lib/time';
import { useSession } from '../state/session';
import { Icon, type IconName } from './Icon';

/** Header line for any data region: last update, relative age, Stale flag, manual refresh. */
export function Freshness({
  updatedAt, refreshing, error, onRefresh, label = 'Updated',
}: {
  updatedAt: number | null; refreshing?: boolean; error?: Error; onRefresh?: () => void; label?: string;
}) {
  const now = useNow(5000);
  const { opsOffset } = useSession();
  const stale = !!updatedAt && now - updatedAt > STALE_MS;
  return (
    <div className="freshness" aria-live="polite">
      {error && updatedAt ? (
        <span className="tag tone-crit tag-compact" title={error.message}>
          <Icon name="cloud_off" /> Refresh failed
        </span>
      ) : null}
      {stale ? (
        <span className="tag tone-warn tag-compact" title="Data has not refreshed for over 90 seconds">
          <Icon name="history" /> Stale
        </span>
      ) : null}
      <span className="muted">
        {updatedAt ? `${label} ${hms(updatedAt + opsOffset)} · ${ago(updatedAt, now)}` : refreshing ? 'Loading…' : '—'}
      </span>
      {onRefresh ? (
        <IgrButton variant="flat" onClick={onRefresh} disabled={refreshing} aria-label="Refresh now">
          <Icon name="refresh" /> Refresh
        </IgrButton>
      ) : null}
    </div>
  );
}

export function LoadingBlock({ label = 'Loading…', rows = 6 }: { label?: string; rows?: number }) {
  return (
    <div className="state-block" role="status" aria-live="polite">
      <IgrLinearProgress indeterminate aria-label={label} />
      <p className="muted">{label}</p>
      <div className="skeleton" aria-hidden="true">
        {Array.from({ length: rows }, (_, i) => <div key={i} className="skeleton-row" />)}
      </div>
    </div>
  );
}

export function EmptyState({ icon = 'check_circle', title, children }: { icon?: IconName; title: string; children?: ReactNode }) {
  return (
    <div className="state-block state-empty" role="status">
      <Icon name={icon} className="state-icon" />
      <h3>{title}</h3>
      {children ? <div className="muted">{children}</div> : null}
    </div>
  );
}

/** Used when the first load fails (no data to show) — distinct from a stale background refresh. */
export function ErrorState({ error, onRetry }: { error: Error; onRetry?: () => void }) {
  const network = (error as { status?: number }).status === 0;
  return (
    <div className="state-block state-error" role="alert">
      <Icon name={network ? 'cloud_off' : 'octagon'} className="state-icon" />
      <h3>{network ? 'Disconnected from the dispatch service' : 'Could not load this data'}</h3>
      <p className="muted">{error.message}</p>
      {onRetry ? <IgrButton onClick={onRetry}><Icon name="refresh" /> Try again</IgrButton> : null}
    </div>
  );
}
