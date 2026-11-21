import { IgrButton, IgrDatePicker, IgrSelect, IgrSelectItem } from 'igniteui-react';
import { IgrGridLite, IgrGridLiteColumn, type IgrCellContext } from 'igniteui-react/grid-lite';
import { useMemo, useState, type MouseEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import { api } from '../../api/client';
import type { BoardTechnician, JobListItem } from '../../api/types';
import { Chart } from '../../components/Chart';
import { chartColors } from '../../lib/chartColors';
import { ConflictDialog } from '../../components/ConflictDialog';
import { Icon, type IconName } from '../../components/Icon';
import { JobPanel } from '../../components/JobPanel';
import { RescheduleDialog } from '../../components/RescheduleDialog';
import { EmptyState, ErrorState, Freshness, LoadingBlock } from '../../components/States';
import { SlaTag } from '../../components/Tags';
import type { ConflictError } from '../../lib/conflict';
import { STATUS_META } from '../../lib/status';
import { fromDateKey, hm, parse, toDateKey } from '../../lib/time';
import { useSession } from '../../state/session';
import { usePolling } from '../../state/usePolling';

interface Exception {
  id: string;
  kind: 'overlap' | 'overtime' | 'window' | 'staffing' | 'unassigned' | 'capacity';
  severity: 'crit' | 'warn';
  title: string;
  detail: string;
  job?: JobListItem;
}
const KIND: Record<Exception['kind'], { label: string; icon: IconName }> = {
  overlap: { label: 'Overlapping bookings', icon: 'octagon' },
  staffing: { label: 'Staffing gap', icon: 'person' },
  unassigned: { label: 'Unassigned, at risk', icon: 'ring' },
  window: { label: 'Outside access window', icon: 'lock' },
  overtime: { label: 'Runs past shift end', icon: 'schedule' },
  capacity: { label: 'Over capacity', icon: 'warning' },
};

const FIELD: Record<string, string> = { Available: 'Available', EnRoute: 'En route', OnSite: 'On site', OnBreak: 'On break', Offline: 'Offline' };
const open = (j: JobListItem) => STATUS_META[j.status].open;

/** Exceptions: access-window conflicts, staffing gaps, overlaps and deadlines the planner must resolve. */
function findExceptions(techs: BoardTechnician[], unassigned: JobListItem[]): Exception[] {
  const out: Exception[] = [];
  for (const t of techs) {
    const jobs = t.jobs.filter(j => open(j) && j.scheduledStart).sort((a, b) => a.scheduledStart!.localeCompare(b.scheduledStart!));
    if (t.onTimeOff && jobs.length) {
      out.push({ id: `staff-${t.technicianId}`, kind: 'staffing', severity: 'crit', title: `${t.technicianName} is on leave`, detail: `${jobs.length} booked job${jobs.length > 1 ? 's' : ''} need a new crew`, job: jobs[0] });
    }
    if (t.utilizationPct > 100) {
      out.push({ id: `cap-${t.technicianId}`, kind: 'capacity', severity: 'warn', title: `${t.technicianName} booked ${Math.round(t.utilizationPct)}%`, detail: `${t.depotName} · shift ${hm(t.shiftStart)}–${hm(t.shiftEnd)}`, job: jobs[jobs.length - 1] });
    }
    for (let i = 0; i < jobs.length; i++) {
      const j = jobs[i];
      const next = jobs[i + 1];
      if (next && next.scheduledStart! < j.scheduledEnd!) {
        out.push({ id: `ov-${j.id}-${next.id}`, kind: 'overlap', severity: 'crit', title: `${t.technicianName}: ${hm(j.scheduledStart)} and ${hm(next.scheduledStart)} overlap`, detail: `${j.summary} / ${next.summary}`, job: next });
      }
      if (t.shiftEnd && j.scheduledEnd! > t.shiftEnd) {
        out.push({ id: `ot-${j.id}`, kind: 'overtime', severity: 'warn', title: `${j.summary} ends ${hm(j.scheduledEnd)}`, detail: `${t.technicianName}'s shift ends ${hm(t.shiftEnd)}`, job: j });
      }
      if (!j.actualStart && (j.scheduledStart! < j.windowStart || j.scheduledStart! > j.windowEnd)) {
        out.push({ id: `win-${j.id}`, kind: 'window', severity: j.priority === 'Critical' || j.priority === 'High' ? 'crit' : 'warn', title: `${j.siteName} at ${hm(j.scheduledStart)}`, detail: `Access window ${hm(j.windowStart)}–${hm(j.windowEnd)} · ${t.technicianName}`, job: j });
      }
    }
  }
  for (const j of unassigned) {
    if (j.sla.state === 'Breached' || j.sla.state === 'AtRisk') {
      out.push({ id: `un-${j.id}`, kind: 'unassigned', severity: j.sla.state === 'Breached' ? 'crit' : 'warn', title: j.summary, detail: `${j.siteName} · due ${hm(j.sla.dueAt)}`, job: j });
    }
  }
  const order: Exception['kind'][] = ['overlap', 'staffing', 'unassigned', 'window', 'overtime', 'capacity'];
  return out.sort((a, b) => (a.severity === b.severity ? 0 : a.severity === 'crit' ? -1 : 1) || order.indexOf(a.kind) - order.indexOf(b.kind));
}

type CrewRow = BoardTechnician & { openJobs: number };

/** Journey 2 (planning view) — crews and their day on a timeline, with an exception queue alongside. */
export default function PlannerPage() {
  const s = useSession();
  const [params, setParams] = useSearchParams();
  const date = params.get('date') ?? s.today;
  const depots = s.lookups!.depots.filter(d => !s.regionId || d.regionId === s.regionId);
  const depotParam = params.get('depot');
  const depotId = depotParam === 'all' ? null : depotParam ? Number(depotParam) : depots[0]?.id ?? null;
  const skillId = params.get('skill') ? Number(params.get('skill')) : null;
  const jobId = params.get('job') ? Number(params.get('job')) : null;
  const [kindFilter, setKindFilter] = useState<Exception['kind'] | null>(null);
  const [reschedJob, setReschedJob] = useState<JobListItem | null>(null);
  const [conflict, setConflict] = useState<ConflictError | null>(null);

  const set = (patch: Record<string, string | null>) => setParams(prev => {
    const p = new URLSearchParams(prev);
    for (const [k, v] of Object.entries(patch)) {
      if (v === null) p.delete(k);
      else p.set(k, v);
    }
    return p;
  }, { replace: true });

  const board = usePolling(`board|${date}|${s.regionId}|${depotId}|${skillId}`, sig =>
    api.board({ date, regionId: s.regionId ?? undefined, depotId: depotId ?? undefined, skillId: skillId ?? undefined }, sig));

  const techs = useMemo(() => (board.data?.technicians ?? []).slice().sort((a, b) =>
    a.depotName.localeCompare(b.depotName) || (a.shiftStart ?? '').localeCompare(b.shiftStart ?? '') || a.technicianName.localeCompare(b.technicianName)), [board.data]);
  const exceptions = useMemo(() => findExceptions(techs, board.data?.unassigned ?? []), [techs, board.data]);
  const shown = kindFilter ? exceptions.filter(e => e.kind === kindFilter) : exceptions;
  const allJobs = useMemo(() => techs.flatMap(t => t.jobs).concat(board.data?.unassigned ?? []), [techs, board.data]);
  const selected = allJobs.find(j => j.id === jobId) ?? null;

  const crewRows: CrewRow[] = useMemo(() => techs.map(t => ({ ...t, openJobs: t.jobs.filter(open).length })), [techs]);
  const crewTemplates = useMemo(() => ({
    crew: (ctx: IgrCellContext<CrewRow>) => {
      const t = ctx.row.data!;
      return (
        <div className="cell2">
          <span className="l1 strong ellipsis">{t.technicianName}</span>
          <span className="l2 muted ellipsis">{t.level} · {t.skillCodes}</span>
        </div>
      );
    },
    shift: (ctx: IgrCellContext<CrewRow>) => {
      const t = ctx.row.data!;
      const off = t.onTimeOff || t.shiftStatus === 'Absent' || t.shiftStatus === 'Cancelled';
      return (
        <div className="cell2">
          <span className="l1">{t.shiftStart ? `${hm(t.shiftStart)}–${hm(t.shiftEnd)}` : 'No shift'}</span>
          <span className={`l2 ${off ? 'txt-crit' : 'muted'}`}>{t.onTimeOff ? 'On leave' : t.shiftStatus === 'Absent' ? 'Absent' : t.shiftStatus === 'Cancelled' ? 'Shift cancelled' : FIELD[t.fieldStatus] ?? t.fieldStatus}</span>
        </div>
      );
    },
    load: (ctx: IgrCellContext<CrewRow>) => {
      const t = ctx.row.data!;
      return (
        <div className="cell2">
          <span className={`l1 ${t.utilizationPct > 100 ? 'txt-crit' : t.utilizationPct > 85 ? 'txt-warn' : ''}`}>{Math.round(t.utilizationPct)}%</span>
          <span className="l2 muted">{t.openJobs} open</span>
        </div>
      );
    },
  }), []);

  // ---- Timeline (ApexCharts rangeBar): shift bars behind job bars, colored by SLA state ----
  const c = chartColors();
  const dayStart = fromDateKey(date);
  const xmin = new Date(dayStart).setHours(5, 0, 0, 0);
  const xmax = new Date(dayStart).setHours(23, 0, 0, 0);
  const jobPoints: { job: JobListItem; tech: string }[] = [];
  const shiftData = techs.filter(t => t.shiftStart).map(t => ({
    x: t.technicianName,
    y: [parse(t.shiftStart)!.getTime(), parse(t.shiftEnd)!.getTime()],
    fillColor: t.onTimeOff || t.shiftStatus === 'Absent' || t.shiftStatus === 'Cancelled' ? 'hsl(0 60% 92%)' : 'hsl(220 18% 92%)',
  }));
  const jobData = techs.flatMap(t => t.jobs.filter(j => j.scheduledStart && j.status !== 'Cancelled').map(j => {
    jobPoints.push({ job: j, tech: t.technicianName });
    const color = j.status === 'Completed' ? c.neutral : j.sla.state === 'Breached' ? c.breached : j.sla.state === 'AtRisk' ? c.atRisk : c.primary;
    return {
      x: t.technicianName,
      y: [parse(j.scheduledStart)!.getTime(), parse(j.scheduledEnd)!.getTime()],
      fillColor: color,
      strokeColor: j.id === jobId ? '#111' : '#fff',
    };
  }));
  const nowMs = s.opsNow().getTime();

  const onCrewClick = (e: MouseEvent) => {
    const row = e.nativeEvent.composedPath().find((n): n is HTMLElement & { data: CrewRow } => (n as HTMLElement).tagName === 'IGC-GRID-LITE-ROW');
    const first = row?.data?.jobs.filter(open).sort((a, b) => (a.scheduledStart ?? '').localeCompare(b.scheduledStart ?? ''))[0];
    if (first) set({ job: String(first.id) });
  };

  return (
    <div className="planner">
      <header className="page-head">
        <div>
          <h1>Shift planner</h1>
          <p className="muted">{s.regionName} · crews, shifts and booked work · click a bar or an exception to see the job and reschedule it</p>
        </div>
        <div className="planner-filters">
          <IgrDatePicker label="Date" value={fromDateKey(date)} onChange={e => e.detail && set({ date: toDateKey(e.detail), job: null })} />
          <IgrSelect label="Depot" value={depotId ? String(depotId) : 'all'} onChange={e => set({ depot: e.detail.value === 'all' ? 'all' : e.detail.value, job: null })}>
            <IgrSelectItem value="all">All depots</IgrSelectItem>
            {depots.map(d => <IgrSelectItem key={d.id} value={String(d.id)}>{d.name}</IgrSelectItem>)}
          </IgrSelect>
          <IgrSelect label="Qualification" value={skillId ? String(skillId) : 'any'} onChange={e => set({ skill: e.detail.value === 'any' ? null : e.detail.value })}>
            <IgrSelectItem value="any">Any qualification</IgrSelectItem>
            {s.lookups!.skills.map(k => <IgrSelectItem key={k.id} value={String(k.id)}>{k.name}</IgrSelectItem>)}
          </IgrSelect>
          <Freshness updatedAt={board.updatedAt} refreshing={board.refreshing} error={board.error} onRefresh={board.refresh} />
        </div>
      </header>

      <div className="planner-body">
        <section className="card crew-card" aria-label="Crews">
          <div className="card-head"><h2>Crews</h2><span className="muted small">{techs.length} on roster</span></div>
          <div className="grid-fill" onClick={onCrewClick}>
            {board.loading ? <LoadingBlock rows={8} />
              : board.error && !board.data ? <ErrorState error={board.error} onRetry={board.refresh} />
              : !crewRows.length ? <EmptyState icon="person" title="No crews on shift">Change the depot, qualification or date.</EmptyState>
              : (
                <IgrGridLite data={crewRows} adoptRootStyles>
                  <IgrGridLiteColumn field="technicianName" header="Crew" sortable cellTemplate={crewTemplates.crew} />
                  <IgrGridLiteColumn field="shiftStart" header="Shift" sortable width="118px" cellTemplate={crewTemplates.shift} />
                  <IgrGridLiteColumn field="utilizationPct" header="Load" dataType="number" sortable width="76px" cellTemplate={crewTemplates.load} />
                </IgrGridLite>
              )}
          </div>
        </section>

        <section className="card timeline-card" aria-label="Crew timeline">
          <div className="card-head">
            <h2>Timeline · {date === s.today ? 'today' : date}</h2>
            <div className="legend-inline small">
              <span><i style={{ background: c.breached }} /> Breached</span>
              <span><i style={{ background: c.atRisk }} /> At risk</span>
              <span><i style={{ background: c.primary }} /> On track</span>
              <span><i style={{ background: c.neutral }} /> Completed</span>
              <span><i style={{ background: 'hsl(220 18% 92%)', outline: '1px solid #ccc' }} /> Shift</span>
              <span><i style={{ background: 'hsl(0 60% 92%)', outline: '1px solid #e99' }} /> Absent / leave</span>
            </div>
          </div>
          <div className="timeline-scroll">
            {board.loading ? <LoadingBlock rows={10} />
              : !techs.length ? <EmptyState icon="timeline" title="Nothing scheduled" />
              : (
                <Chart
                  type="rangeBar"
                  height={Math.max(360, techs.length * 34 + 70)}
                  label={`Timeline of ${techs.length} crews with shift and job bars. Use the exception queue or crew list for keyboard access.`}
                  series={[{ name: 'Shift', data: shiftData }, { name: 'Job', data: jobData }]}
                  options={{
                    chart: {
                      events: {
                        dataPointSelection: (_e, _c, o) => {
                          if (o && o.seriesIndex === 1) set({ job: String(jobPoints[o.dataPointIndex].job.id) });
                        },
                      },
                    },
                    plotOptions: { bar: { horizontal: true, rangeBarOverlap: true, rangeBarGroupRows: true, barHeight: '62%', borderRadius: 2 } },
                    xaxis: { type: 'datetime', min: xmin, max: xmax, position: 'top', labels: { datetimeUTC: false, format: 'HH:mm' }, tickAmount: 18 },
                    yaxis: { labels: { maxWidth: 170 } },
                    legend: { show: false },
                    stroke: { width: 1 },
                    states: { active: { filter: { type: 'none' } } },
                    annotations: date === s.today ? {
                      xaxis: [{ x: nowMs, borderColor: c.breached, strokeDashArray: 0, label: { text: `Now ${hm(new Date(nowMs))}`, borderColor: c.breached, style: { color: '#fff', background: c.breached }, orientation: 'horizontal' } }],
                    } : undefined,
                    tooltip: {
                      custom: ({ seriesIndex, dataPointIndex }: { seriesIndex: number; dataPointIndex: number }) => {
                        if (seriesIndex === 0) {
                          const t = techs.filter(x => x.shiftStart)[dataPointIndex];
                          return `<div class="apx-tip"><strong>${t.technicianName}</strong><br/>Shift ${hm(t.shiftStart)}–${hm(t.shiftEnd)} · ${t.onTimeOff ? 'on leave' : t.shiftStatus}</div>`;
                        }
                        const { job: j } = jobPoints[dataPointIndex];
                        return `<div class="apx-tip"><strong>${j.summary}</strong><br/>${j.siteName}<br/>${hm(j.scheduledStart)}–${hm(j.scheduledEnd)} · ${STATUS_META[j.status].label} · SLA ${j.sla.state}</div>`;
                      },
                    },
                  }}
                />
              )}
          </div>
        </section>

        <aside className="planner-side">
          {selected ? (
            <>
              <div className="side-back">
                <IgrButton variant="flat" onClick={() => set({ job: null })}><Icon name="arrow_back" /> Exception queue</IgrButton>
                {selected.assignedTechnicianId && open(selected) ? (
                  <IgrButton onClick={() => setReschedJob(selected)}><Icon name="event" /> Reschedule</IgrButton>
                ) : null}
              </div>
              <JobPanel jobId={selected.id} listItem={selected} onChanged={board.refresh} />
            </>
          ) : (
            <section className="card exc-card" aria-label="Exception queue">
              <div className="card-head"><h2>Exception queue</h2><span className="muted small">{exceptions.length} to resolve</span></div>
              <div className="exc-kinds">
                <button type="button" className={`chipbtn ${!kindFilter ? 'on' : ''}`} aria-pressed={!kindFilter} onClick={() => setKindFilter(null)}>All {exceptions.length}</button>
                {(Object.keys(KIND) as Exception['kind'][]).map(k => {
                  const n = exceptions.filter(e => e.kind === k).length;
                  return n ? (
                    <button type="button" key={k} className={`chipbtn ${kindFilter === k ? 'on' : ''}`} aria-pressed={kindFilter === k} onClick={() => setKindFilter(k)}>
                      <Icon name={KIND[k].icon} /> {KIND[k].label} {n}
                    </button>
                  ) : null;
                })}
              </div>
              <ul className="exc-list">
                {!shown.length ? <li><EmptyState title="No exceptions">Shifts, access windows and bookings line up.</EmptyState></li> : null}
                {shown.map(e => (
                  <li key={e.id}>
                    <button type="button" className={`exc tone-${e.severity}`} onClick={() => e.job && set({ job: String(e.job.id) })}>
                      <Icon name={KIND[e.kind].icon} />
                      <span className="exc-body">
                        <span className="exc-kind">{KIND[e.kind].label}{e.severity === 'crit' ? ' · urgent' : ''}</span>
                        <strong>{e.title}</strong>
                        <span className="muted">{e.detail}</span>
                      </span>
                      {e.job ? <SlaTag state={e.job.sla.state} compact /> : null}
                    </button>
                  </li>
                ))}
              </ul>
            </section>
          )}
        </aside>
      </div>

      {reschedJob ? (
        <RescheduleDialog
          open={!!reschedJob}
          job={reschedJob}
          onClose={() => setReschedJob(null)}
          onSaved={board.refresh}
          onConflict={setConflict}
        />
      ) : null}
      <ConflictDialog conflict={conflict} onClose={() => setConflict(null)} onReload={() => { setConflict(null); board.refresh(); }} />
    </div>
  );
}
