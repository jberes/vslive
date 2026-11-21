import { IgrButton } from 'igniteui-react';
import { IgrGridLite, IgrGridLiteColumn, type IgrCellContext } from 'igniteui-react/grid-lite';
import { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../../api/client';
import type { JobListItem } from '../../api/types';
import { Chart } from '../../components/Chart';
import { chartColors } from '../../lib/chartColors';
import { Icon, type IconName } from '../../components/Icon';
import { EmptyState, ErrorState, Freshness, LoadingBlock } from '../../components/States';
import { PriorityTag, SlaCell, StatusText } from '../../components/Tags';
import { nextAction, PRIORITY_META, riskCause } from '../../lib/status';
import { dayTime } from '../../lib/time';
import { ledgerHrefFor } from '../../state/ledgerState';
import { useSession } from '../../state/session';
import { usePolling } from '../../state/usePolling';

type Row = JobListItem & { prioRank: number; left: number; cause: string; next: string };

/** Journey 3 — see today's SLA risk: where, why, and the action to take. */
export default function SlaPage() {
  const s = useSession();
  const navigate = useNavigate();
  const region = s.regionId ?? undefined;
  const key = `${s.today}|${region ?? 'all'}`;
  const summary = usePolling(`sum|${key}`, sig => api.slaSummary(s.today, region, sig));
  const trend = usePolling(`trend|${region ?? 'all'}`, sig => api.slaTrend(14, region, sig), 300_000);
  const risk = usePolling(`risk|${key}`, sig => api.slaAtRisk({ date: s.today, regionId: region, limit: 300 }, sig));
  const c = chartColors();

  const rows: Row[] = useMemo(() => (risk.data ?? []).map(j => ({
    ...j, prioRank: PRIORITY_META[j.priority].rank, left: j.sla.minutesRemaining ?? 0, cause: riskCause(j), next: nextAction(j),
  })), [risk.data]);

  const templates = useMemo(() => ({
    job: (ctx: IgrCellContext<Row>) => {
      const j = ctx.row.data!;
      return (
        <div className="cell2">
          <button type="button" className="rowlink l1" onClick={() => navigate(ledgerHrefFor(j.id))} aria-label={`Open ${j.jobNumber} in the ledger`}>
            {j.jobNumber.replace(/^WO-\d+-/, 'WO-')}
          </button>
          <span className="l2 ellipsis" title={j.summary}>{j.summary}</span>
        </div>
      );
    },
    prio: (ctx: IgrCellContext<Row>) => <PriorityTag priority={ctx.row.data!.priority} />,
    loc: (ctx: IgrCellContext<Row>) => (
      <div className="cell2">
        <span className="l1 ellipsis strong">{ctx.row.data!.siteName}</span>
        <span className="l2 ellipsis muted">{ctx.row.data!.customerName} · {ctx.row.data!.regionName}</span>
      </div>
    ),
    sla: (ctx: IgrCellContext<Row>) => <SlaCell job={ctx.row.data!} />,
    cause: (ctx: IgrCellContext<Row>) => (
      <div className="cell2">
        <span className="l1 ellipsis" title={ctx.row.data!.cause}>{ctx.row.data!.cause}</span>
        <span className="l2 muted">Due {dayTime(ctx.row.data!.sla.dueAt, s.today)} · est. work {ctx.row.data!.estimatedDurationMinutes} min</span>
      </div>
    ),
    owner: (ctx: IgrCellContext<Row>) => (
      <div className="cell2">
        <span className="l1"><StatusText status={ctx.row.data!.status} /></span>
        <span className="l2 ellipsis">{ctx.row.data!.assignedTechnicianName ?? <span className="txt-warn">No owner</span>}</span>
      </div>
    ),
    next: (ctx: IgrCellContext<Row>) => (
      <div className="cell2">
        <span className="l1 strong ellipsis">{ctx.row.data!.next}</span>
        <span className="l2">
          <IgrButton variant="outlined" onClick={() => navigate(ledgerHrefFor(ctx.row.data!.id))}>Act in ledger</IgrButton>
        </span>
      </div>
    ),
  }), [navigate, s.today]);

  const sum = summary.data;
  const hours = sum?.dueByHour ?? [];

  return (
    <div className="sla-page">
      <header className="page-head">
        <div>
          <h1>SLA risk today</h1>
          <p className="muted">{s.regionName} · response and restoration targets · service targets only (safety restrictions are shown on each job)</p>
        </div>
        <Freshness updatedAt={summary.updatedAt} refreshing={summary.refreshing || risk.refreshing} error={summary.error ?? risk.error}
          onRefresh={() => { summary.refresh(); risk.refresh(); }} />
      </header>

      {summary.loading ? <LoadingBlock label="Loading SLA summary…" rows={3} />
        : !sum ? <ErrorState error={summary.error ?? new Error('No data')} onRetry={summary.refresh} />
        : (
          <>
            <section className="kpis sla-kpis" aria-label="SLA indicators">
              <Kpi icon="chart" label="Compliance today" value={`${sum.compliancePct.toFixed(1)}%`} sub={`${sum.counts.met} met of ${sum.counts.met + sum.counts.breached} closed or due`} />
              <Kpi icon="octagon" tone="crit" label="Breached" value={sum.counts.breached} sub="Target already missed" />
              <Kpi icon="warning" tone="warn" label="At risk" value={sum.counts.atRisk} sub="Projected or close to due" />
              <Kpi icon="dot" label="On track" value={sum.counts.onTrack} sub="Inside target" />
              <Kpi icon="check_circle" tone="ok" label="Met" value={sum.counts.met} sub="Completed within target" />
              <Kpi icon="info" label="Penalty exposure" value={`$${Math.round(sum.penaltyExposure / 1000)}k`} sub="Contractual, breached + at risk" />
            </section>

            <section className="sla-charts">
              <div className="card">
                <div className="card-head"><h2>Targets falling due by hour</h2><span className="muted small">Count of jobs whose current target is due in each hour (EST)</span></div>
                <div className="chart-box">
                  <Chart
                    type="bar"
                    label="Stacked column chart of SLA targets due by hour, split into breached, at risk and other"
                    series={[
                      { name: 'Breached', data: hours.map(h => h.breached) },
                      { name: 'At risk', data: hours.map(h => h.atRisk) },
                      { name: 'Other due', data: hours.map(h => Math.max(0, h.due - h.breached - h.atRisk)) },
                    ]}
                    options={{
                      chart: { stacked: true },
                      colors: [c.breached, c.atRisk, c.neutral],
                      xaxis: { categories: hours.map(h => `${String(h.hour).padStart(2, '0')}:00`), labels: { rotate: 0, hideOverlappingLabels: true } },
                      yaxis: { labels: { formatter: v => String(Math.round(v)) } },
                      plotOptions: { bar: { columnWidth: '70%', borderRadius: 2 } },
                    }}
                  />
                </div>
              </div>
              <div className="card">
                <div className="card-head"><h2>By priority</h2><span className="muted small">Open + closed today</span></div>
                <div className="chart-box">
                  <Chart
                    type="bar"
                    label="Stacked bar chart of SLA state by priority"
                    series={[
                      { name: 'Breached', data: sum.byPriority.map(g => g.counts.breached) },
                      { name: 'At risk', data: sum.byPriority.map(g => g.counts.atRisk) },
                      { name: 'On track', data: sum.byPriority.map(g => g.counts.onTrack) },
                      { name: 'Met', data: sum.byPriority.map(g => g.counts.met) },
                    ]}
                    options={{
                      chart: { stacked: true },
                      colors: [c.breached, c.atRisk, c.onTrack, c.met],
                      plotOptions: { bar: { horizontal: true, barHeight: '60%' } },
                      xaxis: { categories: sum.byPriority.map(g => g.label) },
                    }}
                  />
                </div>
              </div>
              <div className="card">
                <div className="card-head"><h2>14-day compliance</h2><span className="muted small">Baseline for improvement targets</span></div>
                <div className="chart-box">
                  {trend.data ? (
                    <Chart
                      type="line"
                      label="Line chart of daily response and restoration compliance over 14 days"
                      series={[
                        { name: 'Response %', data: trend.data.map(t => t.responseCompliancePct) },
                        { name: 'Restoration %', data: trend.data.map(t => t.resolutionCompliancePct) },
                      ]}
                      options={{
                        colors: [c.primary, c.met],
                        stroke: { width: 2, curve: 'straight', dashArray: [0, 5] },
                        markers: { size: 3 },
                        xaxis: { categories: trend.data.map(t => t.date.slice(5).replace('-', '/')), labels: { rotate: 0, hideOverlappingLabels: true } },
                        yaxis: { min: Math.max(0, Math.floor(Math.min(...trend.data.flatMap(t => [t.responseCompliancePct, t.resolutionCompliancePct])) / 10) * 10), max: 100, labels: { formatter: v => `${Math.round(v)}%` } },
                      }}
                    />
                  ) : <LoadingBlock label="Loading trend…" rows={2} />}
                </div>
              </div>
            </section>
          </>
        )}

      <section className="card sla-grid">
        <div className="card-head">
          <h2>Jobs at risk or breached · most urgent first</h2>
          <span className="muted small">{rows.length} jobs · cause combines time left, work remaining, crew, access and schedule</span>
        </div>
        <div className="grid-fill">
          {risk.loading ? <LoadingBlock label="Loading at-risk jobs…" />
            : risk.error && !risk.data ? <ErrorState error={risk.error} onRetry={risk.refresh} />
            : !rows.length ? <EmptyState title="No jobs at risk">Every open target in this location context is on track.</EmptyState>
            : (
              <IgrGridLite data={rows} adoptRootStyles>
                <IgrGridLiteColumn field="jobNumber" header="Job" sortable cellTemplate={templates.job} />
                <IgrGridLiteColumn field="prioRank" header="Priority" dataType="number" sortable width="110px" cellTemplate={templates.prio} />
                <IgrGridLiteColumn field="siteName" header="Location" sortable cellTemplate={templates.loc} />
                <IgrGridLiteColumn field="left" header="SLA" dataType="number" sortable width="230px" cellTemplate={templates.sla} />
                <IgrGridLiteColumn field="cause" header="Cause" sortable cellTemplate={templates.cause} />
                <IgrGridLiteColumn field="status" header="Status / owner" sortable cellTemplate={templates.owner} />
                <IgrGridLiteColumn field="next" header="Next action" cellTemplate={templates.next} />
              </IgrGridLite>
            )}
        </div>
      </section>
    </div>
  );
}

function Kpi({ icon, label, value, sub, tone }: { icon: IconName; label: string; value: string | number; sub: string; tone?: 'crit' | 'warn' | 'ok' }) {
  return (
    <div className={`kpi ${tone ? `tone-${tone}` : ''}`}>
      <span className="k-label"><Icon name={icon} /> {label}</span>
      <span className="k-value">{value}</span>
      <span className="k-sub">{sub}</span>
    </div>
  );
}
