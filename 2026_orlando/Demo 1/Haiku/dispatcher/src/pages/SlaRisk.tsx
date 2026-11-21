import { useMemo } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { IgrSelect, IgrSelectItem, IgrButton } from 'igniteui-react';
import { IgrGridLite, IgrGridLiteColumn, type IgrCellContext } from 'igniteui-react/grid-lite';
import Chart from 'react-apexcharts';
import { apiGet } from '../api/client';
import type { Job, SlaSummary, TrendPoint } from '../api/types';
import { useConsole } from '../context/console';
import { useQuery } from '../hooks/useQuery';
import { ja } from '../i18n/ja';
import { fmtTime } from '../lib/format';
import { SlaChip, PriorityText, Freshness, UpdatedAt } from '../components/common';
import { cell } from "../lib/cellStyle";
import { fillGrid } from "../lib/gridFill";

// Chart colours: blue for selection/primary, amber and red reserved for operational meaning.
const C_AT_RISK = '#b26a00';
const C_BREACHED = '#c4161c';
const C_PRIMARY = '#155dfc';
const C_OK = '#1b6b35';

export default function SlaRisk() {
  const c = useConsole();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const state = params.get('state') ?? '';

  const summary = useQuery<SlaSummary>(`sla|${c.regionId}`, (s) => apiGet<SlaSummary>('/api/sla/summary', { regionId: c.regionId }, s), 60_000);
  const trend = useQuery<TrendPoint[]>('trend|30', (s) => apiGet<TrendPoint[]>('/api/sla/trend', { days: 30 }, s), 300_000);
  const atRisk = useQuery<Job[]>(`risk|${c.regionId}|${state}`, (s) =>
    apiGet<Job[]>('/api/sla/at-risk', { regionId: c.regionId, state, limit: 200 }, s), 30_000);

  const setState = (v: string) => {
    const next = new URLSearchParams(params);
    if (v) next.set('state', v); else next.delete('state');
    setParams(next, { replace: true });
  };

  const hourChart = useMemo(() => ({
    options: {
      chart: { id: 'due-by-hour', toolbar: { show: false }, fontFamily: 'inherit', animations: { enabled: false } },
      xaxis: { categories: (summary.data?.dueByHour ?? []).map((h) => `${String(h.hour).padStart(2, '0')}時`) },
      colors: [C_AT_RISK, C_BREACHED],
      plotOptions: { bar: { columnWidth: '60%' } },
      dataLabels: { enabled: false },
      legend: { position: 'top' as const },
      yaxis: { title: { text: '件数' } },
    },
    series: [
      { name: ja.sla.atRisk, data: (summary.data?.dueByHour ?? []).map((h) => h.atRisk) },
      { name: ja.sla.breached, data: (summary.data?.dueByHour ?? []).map((h) => h.breached) },
    ],
  }), [summary.data]);

  const trendChart = useMemo(() => ({
    options: {
      chart: { id: 'trend', toolbar: { show: false }, fontFamily: 'inherit', animations: { enabled: false } },
      xaxis: { categories: (trend.data ?? []).map((p) => p.date.slice(5)) },
      colors: [C_PRIMARY, C_OK],
      stroke: { width: 2 },
      markers: { size: 3 },
      yaxis: { min: 50, max: 100, labels: { formatter: (v: number) => `${v.toFixed(0)}%` } },
      legend: { position: 'top' as const },
      dataLabels: { enabled: false },
    },
    series: [
      { name: ja.sla.response, data: (trend.data ?? []).map((p) => p.responseCompliancePct) },
      { name: ja.sla.resolution, data: (trend.data ?? []).map((p) => p.resolutionCompliancePct) },
    ],
  }), [trend.data]);

  const cols = {
    job: (ctx: IgrCellContext<Job, any>) => (
      <span style={cell.stack}><span style={cell.l1}>{ctx.row!.data!.jobNumber}</span><span style={cell.l2}>{ctx.row!.data!.summary}</span></span>
    ),
    sla: (ctx: IgrCellContext<Job, any>) => <SlaChip state={ctx.row!.data!.sla.state} minutes={ctx.row!.data!.sla.minutesRemaining} />,
    reason: (ctx: IgrCellContext<Job, any>) => (
      <span style={cell.stack}><span style={cell.l1}>{ctx.row!.data!.sla.reason ?? '—'}</span><span style={cell.l2}>{ctx.row!.data!.siteName}</span></span>
    ),
    due: (ctx: IgrCellContext<Job, any>) => (
      <span style={cell.stack}><span style={cell.l1}>{fmtTime(ctx.row!.data!.sla.dueAt)}</span><span style={cell.l2}>{ctx.row!.data!.assignedTechnicianName ?? '未割当'}</span></span>
    ),
    action: (ctx: IgrCellContext<Job, any>) => (
      <IgrButton variant="flat" onClick={() => navigate(`/jobs/${ctx.row!.data!.id}`)}>{ja.common.details}</IgrButton>
    ),
    priority: (ctx: IgrCellContext<Job, any>) => <PriorityText priority={ctx.row!.data!.priority} />,
  };

  const counts = summary.data?.counts;
  const tiles = [
    { label: ja.sla.compliance, value: summary.data ? `${summary.data.compliancePct.toFixed(1)}%` : '—', key: 'comp' },
    { label: ja.sla.atRisk, value: counts ? String(counts.atRisk) : '—', key: 'risk', glyph: '▲' },
    { label: ja.sla.breached, value: counts ? String(counts.breached) : '—', key: 'breach', glyph: '✖' },
    { label: ja.sla.exposure, value: summary.data ? summary.data.penaltyExposure.toLocaleString('ja-JP') : '—', key: 'exp' },
  ];

  return (
    <section className="sla" aria-labelledby="sla-title">
      <div className="page-head">
        <h1 id="sla-title" className="page-title">{ja.sla.title}</h1>
        <div className="toolbar">
          <IgrSelect label={ja.common.slaState} value={state || 'all'} className="tb-filter"
            onChange={(e: CustomEvent<any>) => setState(e.detail.value === 'all' ? '' : String(e.detail.value))}>
            <IgrSelectItem value="all">{ja.common.all}</IgrSelectItem>
            <IgrSelectItem value="Breached">{ja.slaState.Breached}</IgrSelectItem>
            <IgrSelectItem value="AtRisk">{ja.slaState.AtRisk}</IgrSelectItem>
          </IgrSelect>
          <IgrButton variant="outlined" onClick={() => { summary.refetch(); atRisk.refetch(); }}>{ja.common.refresh}</IgrButton>
          <UpdatedAt at={summary.lastUpdated} />
        </div>
      </div>
      <Freshness stale={summary.stale} disconnected={summary.disconnected} onRetry={summary.refetch} />

      <div className="tiles" role="list">
        {tiles.map((t) => (
          <div className="tile" role="listitem" key={t.key}>
            <span className="tile-label">{t.glyph && <span aria-hidden="true">{t.glyph} </span>}{t.label}</span>
            <span className="tile-value">{t.value}</span>
          </div>
        ))}
      </div>

      <div className="charts">
        <div className="chart-card">
          <h2 className="pane-subtitle">{ja.sla.byHour}</h2>
          {summary.data && <Chart type="bar" height={240} options={hourChart.options} series={hourChart.series} />}
        </div>
        <div className="chart-card">
          <h2 className="pane-subtitle">{ja.sla.trend}</h2>
          {trend.data && <Chart type="line" height={240} options={trendChart.options} series={trendChart.series} />}
        </div>
      </div>

      <div className="pane">
        <div className="pane-head">
          <h2 className="pane-title">{ja.sla.atRisk} / {ja.sla.breached}</h2>
          <span className="muted">{atRisk.data ? `${atRisk.data.length} 件` : ''}</span>
        </div>
        <Freshness stale={atRisk.stale} disconnected={atRisk.disconnected} onRetry={atRisk.refetch} />
        {atRisk.loading && !atRisk.data && <p className="muted">{ja.state.loading}</p>}
        {atRisk.data && atRisk.data.length === 0 && <p className="empty">{ja.state.empty}</p>}
        {atRisk.data && atRisk.data.length > 0 && (
          <IgrGridLite ref={fillGrid} className="grid-fill" data={atRisk.data} autoGenerate={false}>
            <IgrGridLiteColumn field="jobNumber" header={ja.common.job} width="260px" cellTemplate={cols.job} />
            <IgrGridLiteColumn field="priority" header={ja.common.priority} width="110px" cellTemplate={cols.priority} />
            <IgrGridLiteColumn field="sla" header={ja.common.slaState} width="190px" cellTemplate={cols.sla} />
            <IgrGridLiteColumn field="siteName" header="原因" width="300px" cellTemplate={cols.reason} />
            <IgrGridLiteColumn field="responseDueAt" header={ja.common.due} width="170px" cellTemplate={cols.due} />
            <IgrGridLiteColumn field="id" header="" width="120px" cellTemplate={cols.action} />
          </IgrGridLite>
        )}
      </div>
    </section>
  );
}
