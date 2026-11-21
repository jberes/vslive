import { IgrButton } from 'igniteui-react';
import { IgrGridLite, IgrGridLiteColumn, type IgrCellContext } from 'igniteui-react/grid-lite';
import { useMemo, type MouseEvent } from 'react';
import { useSearchParams } from 'react-router-dom';
import { api } from '../../api/client';
import type { JobListItem, SlaState } from '../../api/types';
import { Chart } from '../../components/Chart';
import { chartColors } from '../../lib/chartColors';
import { Icon } from '../../components/Icon';
import { JobPanel } from '../../components/JobPanel';
import { EmptyState, ErrorState, Freshness, LoadingBlock } from '../../components/States';
import { PriorityTag, SlaCell, SlaTag, StatusText } from '../../components/Tags';
import { impactCompare, SLA_META } from '../../lib/status';
import { useSession } from '../../state/session';
import { usePolling } from '../../state/usePolling';

const worst = (jobs: JobListItem[]): SlaState =>
  jobs.reduce<SlaState>((w, j) => (SLA_META[j.sla.state].rank < SLA_META[w].rank ? j.sla.state : w), 'OnTrack');

type Row = JobListItem & { order: number };

/** Corridor Control — clustered faults along a line, by station, with a geographic schematic. */
export default function CorridorPage() {
  const s = useSession();
  const [params, setParams] = useSearchParams();
  const set = (patch: Record<string, string | null>) => setParams(prev => {
    const p = new URLSearchParams(prev);
    for (const [k, v] of Object.entries(patch)) {
      if (v === null) p.delete(k);
      else p.set(k, v);
    }
    return p;
  }, { replace: true });

  const jobs = usePolling(`corr|${s.regionId}|${s.today}`, sig =>
    api.allJobs({ to: s.today, openOnly: true, regionId: s.regionId ?? undefined }, sig));
  const all = useMemo(() => jobs.data ?? [], [jobs.data]);

  const lines = useMemo(() => {
    const m = new Map<number, { id: number; name: string; tier: string; region: string; jobs: JobListItem[] }>();
    for (const j of all) {
      const l = m.get(j.customerId) ?? { id: j.customerId, name: j.customerName, tier: j.customerTier, region: j.regionName, jobs: [] };
      l.jobs.push(j);
      m.set(j.customerId, l);
    }
    return [...m.values()].sort((a, b) => SLA_META[worst(a.jobs)].rank - SLA_META[worst(b.jobs)].rank || b.jobs.length - a.jobs.length);
  }, [all]);

  const lineId = params.get('line') ? Number(params.get('line')) : lines[0]?.id ?? null;
  const stationId = params.get('station') ? Number(params.get('station')) : null;
  const jobId = params.get('job') ? Number(params.get('job')) : null;
  const line = lines.find(l => l.id === lineId) ?? null;

  // Station order along the corridor: by longitude (east → west on the Northeast lines).
  const rows: Row[] = useMemo(() => (line?.jobs ?? [])
    .filter(j => !stationId || j.siteId === stationId)
    .sort((a, b) => b.siteLongitude - a.siteLongitude || impactCompare(a, b))
    .map((j, i) => ({ ...j, order: i })), [line, stationId]);

  const sites = useMemo(() => {
    const m = new Map<number, { id: number; name: string; lat: number; lng: number; jobs: JobListItem[] }>();
    for (const j of line?.jobs ?? []) {
      const x = m.get(j.siteId) ?? { id: j.siteId, name: j.siteName, lat: j.siteLatitude, lng: j.siteLongitude, jobs: [] };
      x.jobs.push(j);
      m.set(j.siteId, x);
    }
    return [...m.values()];
  }, [line]);

  const c = chartColors();
  const byState = (st: SlaState[]) => sites.filter(x => st.includes(worst(x.jobs)));
  const groups = [
    { name: 'Breached', color: c.breached, sites: byState(['Breached']) },
    { name: 'At risk', color: c.atRisk, sites: byState(['AtRisk']) },
    { name: 'On track', color: c.primary, sites: byState(['OnTrack', 'Met', 'NotApplicable']) },
  ];

  const templates = useMemo(() => ({
    station: (ctx: IgrCellContext<Row>) => {
      const j = ctx.row.data!;
      return (
        <div className="cell2">
          <span className="l1 strong ellipsis">{j.siteName}</span>
          <span className="l2 muted ellipsis">{j.siteCity}</span>
        </div>
      );
    },
    job: (ctx: IgrCellContext<Row>) => {
      const j = ctx.row.data!;
      const sel = j.id === jobId;
      return (
        <div className="cell2" ref={el => {
          if (!el) return;
          const row = ctx.row as unknown as HTMLElement;
          row.style.boxShadow = sel ? 'inset 4px 0 0 var(--ig-primary-500)' : '';
          for (const v of ['--row-odd-background', '--row-even-background']) {
            if (sel) row.style.setProperty(v, 'var(--ig-primary-50)'); else row.style.removeProperty(v);
          }
        }}>
          <button type="button" className="rowlink l1" onClick={e => { e.stopPropagation(); set({ job: String(j.id) }); }}>{j.jobNumber.replace(/^WO-\d+-/, 'WO-')}</button>
          <span className="l2 ellipsis">{j.summary}</span>
        </div>
      );
    },
    prio: (ctx: IgrCellContext<Row>) => <PriorityTag priority={ctx.row.data!.priority} />,
    sla: (ctx: IgrCellContext<Row>) => <SlaCell job={ctx.row.data!} />,
    status: (ctx: IgrCellContext<Row>) => (
      <div className="cell2">
        <span className="l1"><StatusText status={ctx.row.data!.status} /></span>
        <span className="l2 ellipsis">{ctx.row.data!.assignedTechnicianName ?? <span className="muted">No owner</span>}</span>
      </div>
    ),
  }), [jobId]); // eslint-disable-line react-hooks/exhaustive-deps

  const onGridClick = (e: MouseEvent) => {
    const row = e.nativeEvent.composedPath().find((n): n is HTMLElement & { data: Row } => (n as HTMLElement).tagName === 'IGC-GRID-LITE-ROW');
    if (row?.data) set({ job: String(row.data.id) });
  };
  const station = sites.find(x => x.id === stationId);
  const selectedJob = all.find(j => j.id === jobId);

  return (
    <div className="corridor">
      <header className="page-head">
        <div>
          <h1>Corridor control</h1>
          <p className="muted">{s.regionName} · open faults by line and station, ordered along the corridor</p>
        </div>
        <Freshness updatedAt={jobs.updatedAt} refreshing={jobs.refreshing} error={jobs.error} onRefresh={jobs.refresh} />
      </header>
      <div className="corridor-body">
        <nav className="card lines" aria-label="Lines">
          <div className="card-head"><h2>Lines</h2><span className="muted small">{lines.length}</span></div>
          {jobs.loading ? <LoadingBlock rows={4} /> : (
            <ul className="line-list">
              {lines.map(l => {
                const w = worst(l.jobs);
                const br = l.jobs.filter(j => j.sla.state === 'Breached').length;
                const ar = l.jobs.filter(j => j.sla.state === 'AtRisk').length;
                return (
                  <li key={l.id}>
                    <button type="button" className={`line-btn ${l.id === lineId ? 'on' : ''}`} aria-current={l.id === lineId} onClick={() => set({ line: String(l.id), station: null, job: null })}>
                      <span className="lb-top"><strong>{l.name}</strong><SlaTag state={w} compact /></span>
                      <span className="muted small">{l.tier} tier{s.regionId ? '' : ` · ${l.region}`}</span>
                      <span className="lb-counts small">
                        <span>{l.jobs.length} open</span>
                        <span className={br ? 'txt-crit' : 'muted'}>{br} breached</span>
                        <span className={ar ? 'txt-warn' : 'muted'}>{ar} at risk</span>
                      </span>
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </nav>

        <section className="card" aria-label="Jobs along the line">
          <div className="card-head">
            <h2>{line?.name ?? 'Line'} {station ? `· ${station.name}` : ''}</h2>
            {station ? <IgrButton variant="flat" onClick={() => set({ station: null })}><Icon name="close" /> All stations</IgrButton> : <span className="muted small">{rows.length} open jobs · {sites.length} stations</span>}
          </div>
          <div className="grid-fill" onClick={onGridClick}>
            {jobs.loading ? <LoadingBlock /> : jobs.error && !jobs.data ? <ErrorState error={jobs.error} onRetry={jobs.refresh} />
              : !rows.length ? <EmptyState title="No open faults on this line" />
              : (
                <IgrGridLite data={rows} adoptRootStyles>
                  <IgrGridLiteColumn field="order" header="Station (corridor order)" dataType="number" sortable cellTemplate={templates.station} />
                  <IgrGridLiteColumn field="jobNumber" header="Job" sortable cellTemplate={templates.job} />
                  <IgrGridLiteColumn field="priority" header="Priority" width="110px" cellTemplate={templates.prio} />
                  <IgrGridLiteColumn field="slaKey" header="SLA" width="220px" cellTemplate={templates.sla} />
                  <IgrGridLiteColumn field="status" header="Status / owner" sortable cellTemplate={templates.status} />
                </IgrGridLite>
              )}
          </div>
        </section>

        <div className="corridor-side">
          <section className="card geo-card" aria-label="Geographic schematic">
            <div className="card-head"><h2>Geographic context</h2><span className="muted small">Stations with open work · worst state · click to filter</span></div>
            <div className="chart-box">
              {sites.length ? (
                <Chart
                  type="scatter"
                  label={`Map-style scatter of ${sites.length} stations on ${line?.name}, colored by worst SLA state`}
                  series={groups.map(g => ({ name: g.name, data: g.sites.map(x => ({ x: x.lng, y: x.lat })) }))}
                  options={{
                    colors: groups.map(g => g.color),
                    markers: { size: 8, strokeWidth: 1, strokeColors: '#fff', shape: ['square', 'triangle', 'circle'] as unknown as 'circle' },
                    xaxis: { type: 'numeric', tickAmount: 5, labels: { formatter: v => `${Number(v).toFixed(1)}°` }, title: { text: 'Longitude' } },
                    yaxis: { tickAmount: 4, labels: { formatter: v => `${Number(v).toFixed(1)}°` }, title: { text: 'Latitude' } },
                    chart: {
                      events: {
                        dataPointSelection: (_e, _c, o) => {
                          if (!o) return;
                          const site = groups[o.seriesIndex]?.sites[o.dataPointIndex];
                          if (site) set({ station: String(site.id), job: null });
                        },
                      },
                    },
                    tooltip: {
                      custom: ({ seriesIndex, dataPointIndex }: { seriesIndex: number; dataPointIndex: number }) => {
                        const x = groups[seriesIndex].sites[dataPointIndex];
                        return `<div class="apx-tip"><strong>${x.name}</strong><br/>${x.jobs.length} open · worst ${SLA_META[worst(x.jobs)].label}</div>`;
                      },
                    },
                  }}
                />
              ) : <EmptyState icon="place" title="No stations to plot" />}
            </div>
          </section>
          <JobPanel jobId={jobId} listItem={selectedJob} onClose={() => set({ job: null })} onChanged={jobs.refresh} />
        </div>
      </div>
    </div>
  );
}
