import {
  IgrButton, IgrButtonGroup, IgrCombo, IgrInput, IgrToggleButton,
} from 'igniteui-react';
import { IgrGridLite, IgrGridLiteColumn, type IgrCellContext } from 'igniteui-react/grid-lite';
import { useDeferredValue, useEffect, useMemo, type MouseEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import { api } from '../../api/client';
import type { JobListItem, JobPriority } from '../../api/types';
import { Icon } from '../../components/Icon';
import { JobPanel } from '../../components/JobPanel';
import { EmptyState, ErrorState, Freshness, LoadingBlock } from '../../components/States';
import { PriorityTag, SlaCell, StatusText } from '../../components/Tags';
import { useSession } from '../../state/session';
import { rememberLedgerSearch } from '../../state/ledgerState';
import { usePolling } from '../../state/usePolling';
import { impactCompare, nextAction, PRIORITY_META, riskCause, SLA_META } from '../../lib/status';
import { dayTime, duration, hm, remaining } from '../../lib/time';

type View = 'all' | 'unassigned' | 'risk' | 'critical' | 'hold';
const VIEWS: { id: View; label: string }[] = [
  { id: 'all', label: 'All open' },
  { id: 'risk', label: 'Breached + at risk' },
  { id: 'unassigned', label: 'Unassigned' },
  { id: 'critical', label: 'Critical' },
  { id: 'hold', label: 'On hold' },
];

/** Grid row: the API job plus numeric sort keys (sorting by text would put "Critical" after "High"). */
type Row = JobListItem & { prioRank: number; slaKey: number; startKey: string };

const matchesView = (j: JobListItem, v: View) =>
  v === 'all' ||
  (v === 'unassigned' && j.status === 'Unassigned') ||
  (v === 'risk' && (j.sla.state === 'Breached' || j.sla.state === 'AtRisk')) ||
  (v === 'critical' && j.priority === 'Critical') ||
  (v === 'hold' && j.status === 'OnHold');

export default function LedgerPage() {
  const s = useSession();
  const [params, setParams] = useSearchParams();
  const view = (params.get('view') as View) ?? 'all';
  const scope = params.get('scope') === 'today' ? 'today' : 'open';
  const prioParam = params.get('prio') ?? '';
  const skillParam = params.get('skill') ?? '';
  const prio = useMemo(() => prioParam.split(',').filter(Boolean) as JobPriority[], [prioParam]);
  const skill = useMemo(() => skillParam.split(',').filter(Boolean).map(Number), [skillParam]);
  const q = params.get('q') ?? '';
  const selectedId = params.get('job') ? Number(params.get('job')) : null;
  const deferredQ = useDeferredValue(q);

  // Mirror the URL into session storage so returning to the ledger restores filters + selection.
  useEffect(() => { rememberLedgerSearch(params.size ? `?${params.toString()}` : ''); }, [params]);

  const update = (patch: Record<string, string | null>) => {
    setParams(prev => {
      const p = new URLSearchParams(prev);
      for (const [k, v] of Object.entries(patch)) {
        if (v === null || v === '') p.delete(k);
        else p.set(k, v);
      }
      return p;
    }, { replace: true });
  };

  const key = `${scope}|${s.regionId ?? 'all'}|${s.today}`;
  const jobs = usePolling(key, signal =>
    api.allJobs(scope === 'open'
      ? { to: s.today, openOnly: true, regionId: s.regionId ?? undefined }
      : { date: s.today, regionId: s.regionId ?? undefined }, signal),
  );

  const all = useMemo(() => jobs.data ?? [], [jobs.data]);
  const counts = useMemo(() => {
    const open = all.filter(j => !['Completed', 'Cancelled'].includes(j.status));
    return {
      breached: open.filter(j => j.sla.state === 'Breached').length,
      atRisk: open.filter(j => j.sla.state === 'AtRisk').length,
      unassigned: open.filter(j => j.status === 'Unassigned').length,
      critical: open.filter(j => j.priority === 'Critical').length,
      hold: open.filter(j => j.status === 'OnHold').length,
      open: open.length,
    };
  }, [all]);

  const rows: Row[] = useMemo(() => {
    const needle = deferredQ.trim().toLowerCase();
    return all
      .filter(j => matchesView(j, view))
      .filter(j => !prio.length || prio.includes(j.priority))
      .filter(j => !skill.length || skill.includes(j.requiredSkillId))
      .filter(j => !needle || [j.jobNumber, j.summary, j.siteName, j.siteCity, j.customerName, j.assignedTechnicianName ?? '']
        .some(f => f.toLowerCase().includes(needle)))
      .sort(impactCompare)
      .map(j => ({
        ...j,
        prioRank: PRIORITY_META[j.priority].rank,
        slaKey: SLA_META[j.sla.state].rank * 100000 + (j.sla.minutesRemaining ?? 99999),
        startKey: j.scheduledStart ?? '9999',
      }));
  }, [all, view, prio, skill, deferredQ]);

  // Highest-impact unresolved problem in the current location context (ignores grid filters on purpose).
  const top = useMemo(
    () => all.filter(j => !['Completed', 'Cancelled'].includes(j.status) && j.sla.state !== 'OnTrack').sort(impactCompare)[0],
    [all],
  );

  const select = (id: number | null) => update({ job: id ? String(id) : null });

  // Row click → select. Grid Lite renders rows in shadow DOM, so read the row element from the event path.
  const onGridClick = (e: MouseEvent) => {
    const row = e.nativeEvent.composedPath().find(
      (n): n is HTMLElement & { data: JobListItem } => (n as HTMLElement).tagName === 'IGC-GRID-LITE-ROW',
    );
    if (row?.data) select(row.data.id);
  };

  // Templates depend on the selection so the selected row restyles when it changes.
  const templates = useMemo(() => makeTemplates(selectedId, select, s.today), [selectedId, s.today]); // eslint-disable-line react-hooks/exhaustive-deps

  const selectedJob = all.find(j => j.id === selectedId) ?? null;

  return (
    <div className="ledger">
      <header className="page-head">
        <div>
          <h1>Operations ledger</h1>
          <p className="muted">
            {s.regionName} · {scope === 'open' ? 'all open work, including overdue carry-over' : 'all work scheduled today'}
          </p>
        </div>
        <Freshness updatedAt={jobs.updatedAt} refreshing={jobs.refreshing} error={jobs.error} onRefresh={jobs.refresh} />
      </header>

      {/* Slim risk summary: each tile is also a filter. Counts are text + icon, not color alone. */}
      <section className="risk-strip" aria-label="Risk summary">
        <RiskTile icon="octagon" tone="crit" label="Breached" value={jobs.data ? counts.breached : '—'} active={view === 'risk'} onClick={() => update({ view: 'risk' })} />
        <RiskTile icon="warning" tone="warn" label="At risk" value={jobs.data ? counts.atRisk : '—'} active={view === 'risk'} onClick={() => update({ view: 'risk' })} />
        <RiskTile icon="ring" tone="neutral" label="Unassigned" value={jobs.data ? counts.unassigned : '—'} active={view === 'unassigned'} onClick={() => update({ view: 'unassigned' })} />
        <RiskTile icon="diamond" tone="crit" label="Critical open" value={jobs.data ? counts.critical : '—'} active={view === 'critical'} onClick={() => update({ view: 'critical' })} />
        <RiskTile icon="pause" tone="warn" label="On hold" value={jobs.data ? counts.hold : '—'} active={view === 'hold'} onClick={() => update({ view: 'hold' })} />
        <RiskTile icon="list" tone="info" label="Open jobs" value={jobs.data ? counts.open : '—'} active={view === 'all'} onClick={() => update({ view: null })} />
        {top ? (
          <div className={`top-problem tone-${top.sla.state === 'Breached' ? 'crit' : 'warn'}`} role="region" aria-label="Highest-impact unresolved problem">
            <div className="tp-label"><Icon name="arrow_up" /> Highest-impact problem</div>
            <div className="tp-main">
              <PriorityTag priority={top.priority} />
              <strong>{top.summary}</strong>
              <span className="muted">{top.siteName} · {top.customerName}</span>
            </div>
            <div className="tp-meta">
              <span><span className="muted">Owner </span><strong>{top.assignedTechnicianName ?? 'Unowned — no crew'}</strong></span>
              <span><span className="muted">SLA </span><strong className={top.sla.state === 'Breached' ? 'txt-crit' : 'txt-warn'}>{SLA_META[top.sla.state].label}, {remaining(top.sla.minutesRemaining)}</strong></span>
              <span><span className="muted">Next </span><strong>{nextAction(top)}</strong></span>
            </div>
            <IgrButton onClick={() => select(top.id)}>Open</IgrButton>
          </div>
        ) : null}
      </section>

      <section className="filters" aria-label="Filters">
        <IgrButtonGroup
          className="seg"
          selection="single-required"
          aria-label="Scope"
        >
          <IgrToggleButton value="open" selected={scope === 'open'} onClick={() => update({ scope: null })}>Open + carry-over</IgrToggleButton>
          <IgrToggleButton value="today" selected={scope === 'today'} onClick={() => update({ scope: 'today' })}>All today</IgrToggleButton>
        </IgrButtonGroup>
        <IgrButtonGroup className="seg" selection="single-required" aria-label="View">
          {VIEWS.map(v => (
            <IgrToggleButton key={v.id} value={v.id} selected={view === v.id} onClick={() => update({ view: v.id === 'all' ? null : v.id })}>
              {v.label}
            </IgrToggleButton>
          ))}
        </IgrButtonGroup>
        <IgrCombo
          className="f-prio"
          label="Priority"
          placeholder="Any"
          data={(['Critical', 'High', 'Medium', 'Low'] as JobPriority[]).map(p => ({ id: p, name: p }))}
          valueKey="id"
          displayKey="name"
          value={prio}
          disableFiltering
          onChange={e => update({ prio: (e.detail.newValue as string[]).join(',') || null })}
        />
        <IgrCombo
          className="f-skill"
          label="Trade / qualification"
          placeholder="Any"
          data={s.lookups!.skills}
          valueKey="id"
          displayKey="name"
          groupKey="category"
          value={skill}
          onChange={e => update({ skill: (e.detail.newValue as number[]).join(',') || null })}
        />
        <IgrInput
          className="f-search"
          label="Search"
          placeholder="Job, station, line or crew"
          value={q}
          onInput={e => update({ q: e.detail || null })}
        >
          <span slot="prefix"><Icon name="search" /></span>
        </IgrInput>
        {(view !== 'all' || prio.length || skill.length || q) ? (
          <IgrButton variant="flat" onClick={() => update({ view: null, prio: null, skill: null, q: null })}>Clear filters</IgrButton>
        ) : null}
        <span className="muted result-count" aria-live="polite">{rows.length} of {all.length} jobs</span>
      </section>

      <div className="ledger-body">
        <section className="grid-wrap" aria-label="Job queue" onClick={onGridClick}>
          {jobs.loading ? (
            <LoadingBlock label="Loading job queue…" />
          ) : jobs.error && !jobs.data ? (
            <ErrorState error={jobs.error} onRetry={jobs.refresh} />
          ) : rows.length === 0 ? (
            <EmptyState icon="search" title="No jobs match these filters">
              {all.length ? 'Clear or change the filters to see more of the queue.' : 'No open work in this location context.'}
            </EmptyState>
          ) : (
            <IgrGridLite className="ledger-grid" data={rows} adoptRootStyles>
              <IgrGridLiteColumn field="jobNumber" header="Job" sortable cellTemplate={templates.job} />
              <IgrGridLiteColumn field="prioRank" header="Priority" dataType="number" sortable width="120px" cellTemplate={templates.prio} />
              <IgrGridLiteColumn field="siteName" header="Asset / location" sortable cellTemplate={templates.location} />
              <IgrGridLiteColumn field="slaKey" header="SLA" dataType="number" sortable width="230px" cellTemplate={templates.sla} />
              <IgrGridLiteColumn field="status" header="Status / owner" sortable cellTemplate={templates.status} />
              <IgrGridLiteColumn field="startKey" header="Schedule / access window" sortable width="190px" cellTemplate={templates.schedule} />
              <IgrGridLiteColumn field="requiredSkillName" header="Qualification" sortable cellTemplate={templates.skill} />
            </IgrGridLite>
          )}
        </section>

        <JobPanel
          jobId={selectedId}
          listItem={selectedJob}
          onClose={() => select(null)}
          onChanged={jobs.refresh}
        />
      </div>
    </div>
  );
}

function RiskTile({ icon, tone, label, value, active, onClick }: {
  icon: 'octagon' | 'warning' | 'ring' | 'diamond' | 'pause' | 'list'; tone: string; label: string; value: number | string; active: boolean; onClick: () => void;
}) {
  return (
    <button type="button" className={`risk-tile tone-${tone} ${active ? 'is-active' : ''}`} onClick={onClick} aria-pressed={active}>
      <Icon name={icon} />
      <span className="rt-value">{value}</span>
      <span className="rt-label">{label}</span>
    </button>
  );
}

function makeTemplates(selectedId: number | null, select: (id: number) => void, today: string) {
  type Ctx = IgrCellContext<Row>;
  // Applies the selected-row treatment to Grid Lite's (recycled) row element.
  const markRow = (ctx: Ctx) => (el: HTMLElement | null) => {
    if (!el) return;
    const row = ctx.row as unknown as HTMLElement;
    const sel = ctx.row.data?.id === selectedId;
    row.toggleAttribute('data-selected', sel);
    row.setAttribute('aria-selected', String(sel));
    for (const v of ['--row-odd-background', '--row-even-background', '--row-hover-background']) {
      if (sel) row.style.setProperty(v, 'var(--ig-primary-50)');
      else row.style.removeProperty(v);
    }
    row.style.boxShadow = sel ? 'inset 4px 0 0 var(--ig-primary-500)' : '';
  };
  return {
    job: (ctx: Ctx) => {
      const j = ctx.row.data!;
      return (
        <div className="cell2" ref={markRow(ctx)}>
          <button type="button" className="rowlink l1" onClick={e => { e.stopPropagation(); select(j.id); }} aria-label={`Open ${j.jobNumber}, ${j.summary}`}>
            {j.jobNumber.replace(/^WO-\d+-/, 'WO-')}
          </button>
          <span className="l2 ellipsis" title={j.summary}>{j.summary}</span>
        </div>
      );
    },
    prio: (ctx: Ctx) => (
      <div className="cell2">
        <span className="l1"><PriorityTag priority={ctx.row.data!.priority} /></span>
        <span className="l2 muted">{ctx.row.data!.jobCategory}</span>
      </div>
    ),
    location: (ctx: Ctx) => {
      const j = ctx.row.data!;
      return (
        <div className="cell2">
          <span className="l1 ellipsis strong" title={j.siteName}>{j.siteName}</span>
          <span className="l2 ellipsis muted">{j.customerName} · {j.siteCity}</span>
        </div>
      );
    },
    sla: (ctx: Ctx) => <SlaCell job={ctx.row.data!} />,
    status: (ctx: Ctx) => {
      const j = ctx.row.data!;
      return (
        <div className="cell2">
          <span className="l1"><StatusText status={j.status} /></span>
          <span className={`l2 ellipsis ${j.assignedTechnicianName ? '' : 'muted'}`}>
            {j.assignedTechnicianName ?? 'No owner'}
            {j.sla.state !== 'OnTrack' && j.sla.state !== 'Met' ? <span className="muted"> · {riskCause(j).split(' · ')[0]}</span> : null}
          </span>
        </div>
      );
    },
    schedule: (ctx: Ctx) => {
      const j = ctx.row.data!;
      return (
        <div className="cell2">
          <span className="l1">{j.scheduledStart ? `${dayTime(j.scheduledStart, today)}–${hm(j.scheduledEnd)}` : 'Not scheduled'}</span>
          <span className="l2 muted">Window {dayTime(j.windowStart, today)}–{hm(j.windowEnd)}</span>
        </div>
      );
    },
    skill: (ctx: Ctx) => {
      const j = ctx.row.data!;
      return (
        <div className="cell2">
          <span className="l1 ellipsis">{j.requiredSkillName}</span>
          <span className="l2 muted">Est. {duration(j.estimatedDurationMinutes)}</span>
        </div>
      );
    },
  };
}

