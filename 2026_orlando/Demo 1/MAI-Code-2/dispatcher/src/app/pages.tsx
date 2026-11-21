import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { IgrButton } from 'igniteui-react';
import { IgrGridLite, IgrGridLiteColumn, type IgrCellContext } from 'igniteui-react/grid-lite';
import Chart from 'react-apexcharts';
import type { ApexOptions } from 'apexcharts';
import ApexGantt, { type TaskInput } from 'apexgantt';
import 'apexgantt/ignite-ui';
import {
  formatDateTime, formatTime, slaLabel, type BoardTechnician, type Job,
  useDispatch,
} from './dispatch-data';

const priorityClass: Record<Job['priority'], string> = {
  Critical: 'critical', High: 'high', Medium: 'medium', Low: 'low',
};

function fillGridViewport(grid: HTMLElement | null) {
  if (!grid) return;
  const resize = () => {
    const viewport = grid.shadowRoot?.querySelector<HTMLElement>('igc-virtual-scroll');
    if (viewport) viewport.style.height = `${Math.max(grid.clientHeight - 42, 150)}px`;
  };
  const observer = new ResizeObserver(resize);
  observer.observe(grid);
  requestAnimationFrame(resize);
  return () => observer.disconnect();
}

function PageHeading({ eyebrow, title, description }: { eyebrow: string; title: string; description: string }) {
  return <section className="page-heading">
    <div><span className="eyebrow">{eyebrow}</span><h2>{title}</h2><p>{description}</p></div>
  </section>;
}

function SlaBadge({ job }: { job: Job }) {
  return <span className={`sla-badge ${job.sla.state.toLowerCase()}`}>{job.sla.state}<small>{slaLabel(job)}</small></span>;
}

function JobCell({ job, onOpen }: { job: Job; onOpen: () => void }) {
  return <button type="button" className="cell-link" onClick={onOpen}>
    <span className="cell-primary">{job.summary}</span>
    <span className="cell-secondary">{job.jobNumber} · {job.customerName}</span>
  </button>;
}

function LocationCell({ job }: { job: Job }) {
  return <div className="stacked-cell"><span>{job.siteName}</span><small>{job.siteCity} · {job.regionName}</small></div>;
}

function EmptyState({ title, body }: { title: string; body: string }) {
  return <div className="empty-state"><span className="empty-icon">○</span><strong>{title}</strong><p>{body}</p></div>;
}

function LoadingState() {
  return <div className="loading-state" role="status"><span className="loading-bar" />Synchronizing live operations data…</div>;
}

function DetailPanel() {
  const data = useDispatch();
  const detail = data.detail;
  const [assignMode, setAssignMode] = useState(false);
  const [technicianId, setTechnicianId] = useState('');
  const [start, setStart] = useState('');
  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => {
    queueMicrotask(() => {
      setAssignMode(false);
      setTechnicianId('');
      setStart('');
      setActionError(null);
    });
  }, [data.selectedId]);

  if (!data.selectedId) return null;
  if (!detail || detail.job.id !== data.selectedId) return <aside className="detail-panel detail-loading">Loading job record…</aside>;
  const job = detail.job;

  async function openAssignment() {
    setActionError(null);
    await data.loadRecommendations(job.id);
    setAssignMode(true);
    setStart(job.windowStart.slice(0, 16));
  }

  async function submitAssignment(event: FormEvent) {
    event.preventDefault();
    if (!technicianId || !start) return;
    setSaving(true);
    setActionError(null);
    try {
      await data.assignJob(job.id, Number(technicianId), start);
      setAssignMode(false);
    } catch (reason) {
      setActionError(reason instanceof Error ? reason.message : 'Assignment was not saved.');
    } finally {
      setSaving(false);
    }
  }

  return <aside className="detail-panel" aria-label="Selected job details">
    <div className="detail-top">
      <div><span className={`priority-label ${priorityClass[job.priority]}`}>{job.priority}</span><span className="job-number">{job.jobNumber}</span></div>
      <button className="icon-button" onClick={() => data.selectJob(null)} aria-label="Close job details">×</button>
    </div>
    <h3>{job.summary}</h3>
    <p className="detail-description">{detail.description}</p>
    <div className="detail-status"><SlaBadge job={job} /><span>{job.status}</span></div>

    <section className="detail-section">
      <h4>Operational context</h4>
      <dl className="detail-grid">
        <div><dt>Asset / location</dt><dd>{detail.assetTag}<small>{job.siteName}</small></dd></div>
        <div><dt>Service corridor</dt><dd>{job.regionName}<small>{job.customerName}</small></dd></div>
        <div><dt>Response target</dt><dd>{formatTime(job.responseDueAt)}<small>{formatTime(job.resolutionDueAt)} restoration</small></dd></div>
        <div><dt>Access & safety</dt><dd>{detail.siteAccessNotes || 'Standard access'}<small>{detail.siteAddress}</small></dd></div>
      </dl>
    </section>

    <section className="detail-section">
      <h4>Current ownership</h4>
      <div className="owner-card">
        <div className="avatar small">{job.assignedTechnicianName?.split(' ').map((part) => part[0]).join('') || '?'}</div>
        <div><strong>{job.assignedTechnicianName || 'Awaiting assignment'}</strong><span>{job.requiredSkillName}</span></div>
        <span className="status-dot" />
      </div>
    </section>

    {!assignMode && job.status === 'Unassigned' &&
      <IgrButton className="primary-action" onClick={() => void openAssignment()}>Match & assign crew</IgrButton>}

    {assignMode && <form className="assignment-form" onSubmit={(event) => void submitAssignment(event)}>
      <h4>Assignment impact review</h4>
      <label>Qualified, available crew
        <select value={technicianId} onChange={(event) => setTechnicianId(event.target.value)} required>
          <option value="">Choose a recommendation</option>
          {data.recommendations.map((person) => <option key={person.technicianId} value={person.technicianId}>
            {person.technicianName} · {person.travelMinutes} min travel · {person.utilizationPct}% load
          </option>)}
        </select>
      </label>
      <label>Dispatch time
        <input type="datetime-local" value={start} onChange={(event) => setStart(event.target.value)} required />
      </label>
      <div className="impact-note"><strong>Safety gate</strong><span>Qualifications, shift, access window, workload, travel, and SLA impact are validated again before save.</span></div>
      {data.recommendations.find((person) => person.technicianId === Number(technicianId))?.issues.map((issue) =>
        <div key={issue.code} className={`validation-issue ${issue.severity.toLowerCase()}`}>{issue.message}</div>)}
      {actionError && <div className="validation-issue blocking" role="alert">{actionError}</div>}
      <div className="button-row">
        <IgrButton variant="flat" type="button" onClick={() => setAssignMode(false)}>Cancel</IgrButton>
        <IgrButton disabled={saving || !technicianId}>{saving ? 'Validating…' : 'Confirm assignment'}</IgrButton>
      </div>
    </form>}

    <section className="detail-section">
      <h4>Recent activity</h4>
      <ol className="activity-list">
        {detail.events.slice(0, 3).map((event) => <li key={event.id}>
          <span className="activity-mark" /><div><strong>{event.eventType}</strong><span>{formatDateTime(event.occurredAt)}</span><p>{event.details}</p></div>
        </li>)}
      </ol>
    </section>
  </aside>;
}

function DashboardCards() {
  const { summary } = useDispatch();
  if (!summary) return null;
  const cards = [
    { label: 'Active jobs', value: summary.counts.total, note: 'Across selected scope', tone: 'neutral' },
    { label: 'At risk', value: summary.counts.atRisk, note: 'Action window narrowing', tone: 'amber' },
    { label: 'Breached', value: summary.counts.breached, note: 'Immediate attention', tone: 'red' },
    { label: 'SLA compliance', value: `${summary.compliancePct}%`, note: 'Response + restoration', tone: 'blue' },
  ];
  return <div className="metric-grid">{cards.map((card) => <article key={card.label} className={`metric-card ${card.tone}`}>
    <span>{card.label}</span><strong>{card.value}</strong><small>{card.note}</small>
  </article>)}</div>;
}

export function Ledger() {
  const data = useDispatch();
  const visibleJobs = useMemo(() => data.jobs.filter((job) => !data.search ||
    `${job.summary} ${job.siteName} ${job.jobNumber} ${job.assignedTechnicianName ?? ''}`.toLowerCase().includes(data.search.toLowerCase())), [data.jobs, data.search]);
  if (data.loading) return <><PageHeading eyebrow="LEDGER" title="Operations Ledger" description="A shared, actionable view of today’s infrastructure commitments." /><LoadingState /></>;

  return <div className={`route-layout ${data.selectedId ? 'with-detail' : ''}`}>
    <div className="route-main">
      <PageHeading eyebrow="TODAY · OPERATIONS LEDGER" title="Highest-impact work, owned and actionable." description="Triage faults by operational consequence, SLA exposure, access, and crew readiness." />
      <DashboardCards />
      <section className="panel ledger-panel">
        <div className="panel-toolbar">
          <div><h3>Active work queue</h3><p>{data.totalJobs.toLocaleString()} jobs in scope · live updates every 60 seconds</p></div>
          <div className="toolbar-filters">
            <label className="search-field"><span className="sr-only">Search jobs</span><input value={data.search} onChange={(event) => data.setSearch(event.target.value)} placeholder="Search job, asset, location, or owner" /></label>
            <select aria-label="Filter by priority" value={data.priority} onChange={(event) => data.setPriority(event.target.value)}>
              <option value="">All priorities</option><option>Critical</option><option>High</option><option>Medium</option><option>Low</option>
            </select>
            <select aria-label="Filter by status" value={data.status} onChange={(event) => data.setStatus(event.target.value)}>
              <option value="">All statuses</option><option>Unassigned</option><option>Assigned</option><option>Dispatched</option><option>EnRoute</option><option>OnSite</option><option>OnHold</option>
            </select>
          </div>
        </div>
        {visibleJobs.length === 0 ? <EmptyState title="No matching work" body="Adjust the region, priority, status, or search filters to widen the queue." /> :
          <IgrGridLite ref={fillGridViewport} className="jobs-grid" data={visibleJobs} autoGenerate={false}>
            <IgrGridLiteColumn field="summary" header="Job / impact" sortable cellTemplate={(ctx: IgrCellContext<Job>) => {
              const job = ctx.row.data;
              return job ? <JobCell job={job} onOpen={() => data.selectJob(job.id)} /> : null;
            }} />
            <IgrGridLiteColumn field="siteName" header="Location" sortable cellTemplate={(ctx: IgrCellContext<Job>) =>
              ctx.row.data ? <LocationCell job={ctx.row.data} /> : null} />
            <IgrGridLiteColumn field="priority" header="Priority" sortable cellTemplate={(ctx: IgrCellContext<Job>) => {
              const job = ctx.row.data;
              return job ? <span className={`priority-pill ${priorityClass[job.priority]}`}>{job.priority}</span> : null;
            }} />
            <IgrGridLiteColumn field="sla" header="SLA state / remaining" sortable cellTemplate={(ctx: IgrCellContext<Job>) =>
              ctx.row.data ? <SlaBadge job={ctx.row.data} /> : null} />
            <IgrGridLiteColumn field="requiredSkillName" header="Qualification" sortable cellTemplate={(ctx: IgrCellContext<Job>) =>
              ctx.row.data ? <div className="stacked-cell"><span>{ctx.row.data.requiredSkillName}</span><small>{ctx.row.data.status}</small></div> : null} />
            <IgrGridLiteColumn field="assignedTechnicianName" header="Owner / next action" sortable cellTemplate={(ctx: IgrCellContext<Job>) => {
              const job = ctx.row.data;
              return job ? <div className="stacked-cell"><span>{job.assignedTechnicianName || 'Awaiting assignment'}</span><small>{job.status === 'Unassigned' ? 'Match a qualified crew' : `Due ${formatTime(job.sla.dueAt)}`}</small></div> : null;
            }} />
          </IgrGridLite>}
      </section>
      <div className="data-footnote"><span className="pulse-dot" />API connected · operations data current at {formatTime(data.board?.now)}</div>
    </div>
    <DetailPanel />
  </div>;
}

export function Corridor() {
  const data = useDispatch();
  const summary = data.summary;
  const chartOptions: ApexOptions = {
    chart: { type: 'bar', toolbar: { show: false }, stacked: true, fontFamily: 'Segoe UI, sans-serif' },
    plotOptions: { bar: { horizontal: true, borderRadius: 3, barHeight: '54%' } },
    xaxis: { categories: summary?.byRegion.map((group) => group.label.replace('NEC ', '').replace(' Division', '')) ?? [] },
    colors: ['#155dfc', '#e5a400'],
    legend: { position: 'top', horizontalAlign: 'right' },
    grid: { borderColor: '#e7eaf0' },
    dataLabels: { enabled: false },
    tooltip: { theme: 'light' },
  };
  const selectedRegion = data.regions.find((region) => region.id === data.regionId);
  const jobs = data.riskJobs.filter((job) => job.status !== 'Completed' && job.status !== 'Cancelled').slice(0, 40);

  return <div className={`route-layout ${data.selectedId ? 'with-detail' : ''}`}>
    <div className="route-main">
      <PageHeading eyebrow="CORRIDOR CONTROL" title="See where operational risk is clustering." description="Keep line hierarchy and service exposure visible while crews and access windows change." />
      <div className="corridor-layout">
        <section className="panel corridor-chart">
          <div className="panel-heading"><div><span className="eyebrow">REGIONAL EXPOSURE</span><h3>{selectedRegion?.name || 'Atlantic Corridor network'}</h3></div><span className="scope-label">{summary?.counts.total ?? 0} jobs today</span></div>
          {summary ? <Chart options={chartOptions} series={[
            { name: 'Open within target', data: summary.byRegion.map((group) => group.counts.total - group.counts.breached - group.counts.atRisk) },
            { name: 'At risk + breached', data: summary.byRegion.map((group) => group.counts.atRisk + group.counts.breached) },
          ]} type="bar" height={320} /> : <LoadingState />}
          <div className="corridor-legend"><span><i className="blue-dot" />Open within target</span><span><i className="amber-dot" />Needs attention</span><span>Exposure uses SLA + service impact, not proximity alone.</span></div>
        </section>
        <section className="panel corridor-focus">
          <span className="eyebrow">CONTROL POINTS</span>
          <h3>Location risk watch</h3>
          <p>Open the highest-impact unresolved jobs without losing the current region filter.</p>
          <div className="focus-stats"><div><strong>{jobs.filter((job) => job.priority === 'Critical').length}</strong><span>Critical</span></div><div><strong>{new Set(jobs.map((job) => job.siteName)).size}</strong><span>Locations</span></div></div>
          <div className="policy-note"><strong>Authority boundary</strong><span>Dispatcher adjustments stay within qualifications and access rules. Required approvals remain mandatory.</span></div>
        </section>
      </div>
      <section className="panel">
        <div className="panel-toolbar"><div><h3>Highest-impact corridor jobs</h3><p>Breached and at-risk work, synchronized with the ledger selection.</p></div></div>
        {jobs.length ? <IgrGridLite ref={fillGridViewport} className="jobs-grid compact" data={jobs} autoGenerate={false}>
          <IgrGridLiteColumn field="summary" header="Fault / asset" cellTemplate={(ctx: IgrCellContext<Job>) => ctx.row.data ?
            <JobCell job={ctx.row.data} onOpen={() => data.selectJob(ctx.row.data!.id)} /> : null} />
          <IgrGridLiteColumn field="siteName" header="Location" cellTemplate={(ctx: IgrCellContext<Job>) => ctx.row.data ? <LocationCell job={ctx.row.data} /> : null} />
          <IgrGridLiteColumn field="priority" header="Impact" cellTemplate={(ctx: IgrCellContext<Job>) => ctx.row.data ? <span className={`priority-pill ${priorityClass[ctx.row.data.priority]}`}>{ctx.row.data.priority}</span> : null} />
          <IgrGridLiteColumn field="sla" header="SLA" cellTemplate={(ctx: IgrCellContext<Job>) => ctx.row.data ? <SlaBadge job={ctx.row.data} /> : null} />
          <IgrGridLiteColumn field="status" header="Status / owner" cellTemplate={(ctx: IgrCellContext<Job>) => ctx.row.data ?
            <div className="stacked-cell"><span>{ctx.row.data.status}</span><small>{ctx.row.data.assignedTechnicianName || 'No owner'}</small></div> : null} />
        </IgrGridLite> : <EmptyState title="No corridor exceptions" body="No at-risk or breached jobs are present in the current region scope." />}
      </section>
    </div>
    <DetailPanel />
  </div>;
}

interface PendingMove { job: Job; technician: BoardTechnician; newStart: string }

function GanttBoard({ technicians, onPreview }: { technicians: BoardTechnician[]; onPreview: (move: PendingMove) => void }) {
  const host = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!host.current || !technicians.length) return;
    const jobLookup = new Map<string, { job: Job; technician: BoardTechnician }>();
    const tasks: TaskInput[] = technicians.flatMap((technician) => {
      const parentId = `tech-${technician.technicianId}`;
      const shiftStart = technician.shiftStart || `${technicians[0].jobs[0]?.windowStart.slice(0, 10) ?? '2026-11-17'}T06:00:00`;
      const shiftEnd = technician.shiftEnd || `${shiftStart.slice(0, 10)}T18:00:00`;
      const parent: TaskInput = { id: parentId, name: technician.technicianName, startTime: shiftStart, endTime: shiftEnd, progress: technician.utilizationPct, showSummaryBar: true, collapsed: false };
      const children = technician.jobs.filter((job) => job.scheduledStart && job.scheduledEnd).map((job) => {
        const id = `job-${job.id}`;
        jobLookup.set(id, { job, technician });
        return {
          id, parentId, name: job.summary, startTime: job.scheduledStart!, endTime: job.scheduledEnd!,
          progress: job.status === 'Completed' ? 100 : job.status === 'OnSite' ? 65 : 0,
          barBackgroundColor: job.sla.state === 'Breached' ? '#c43131' : job.sla.state === 'AtRisk' ? '#d99800' : '#155dfc',
          barTextColor: '#fff',
        };
      });
      return [parent, ...children];
    });
    const chart = new ApexGantt(host.current, {
      series: tasks,
      inputDateFormat: 'YYYY-MM-DDTHH:mm:ss',
      height: 590,
      rowHeight: 56,
      tasksContainerWidth: 310,
      pixelsPerDay: 700,
      snapUnit: 'minute',
      snapValue: 15,
      columnConfig: [
        { key: 'name', title: 'Crew / commitment', minWidth: '260px', flexGrow: 2 },
      ],
      sortBy: [],
      enableTaskDrag: true,
      enableTaskResize: false,
      enableSelection: true,
      enableQuickFilter: true,
      persistState: { key: 'dispatch-shift-planner' },
      beforeTaskUpdate: ({ task, updates }) => {
        const item = jobLookup.get(task.id);
        if (!item || !updates.startTime) return false;
        onPreview({ ...item, newStart: updates.startTime });
        return false;
      },
      barLabel: { position: 'inside', field: 'name' },
    });
    chart.render();
    return () => chart.destroy();
  }, [onPreview, technicians]);
  return <div className="gantt-host" ref={host} aria-label="Crew schedule Gantt chart" />;
}

export function Planner() {
  const data = useDispatch();
  const [pending, setPending] = useState<PendingMove | null>(null);
  const [saving, setSaving] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const technicians = useMemo(() => (data.board?.technicians ?? [])
    .filter((tech) => tech.shiftStart)
    .sort((a, b) => b.utilizationPct - a.utilizationPct)
    .slice(0, 14), [data.board]);
  const previewMove = useCallback((move: PendingMove) => {
    setPreviewError(null);
    setPending(move);
  }, []);

  async function confirmMove() {
    if (!pending) return;
    setSaving(true);
    setPreviewError(null);
    try {
      await data.rescheduleJob(pending.job.id, pending.technician.technicianId, pending.newStart);
      setPending(null);
    } catch (reason) {
      setPreviewError(reason instanceof Error ? reason.message : 'The schedule change was not saved.');
    } finally {
      setSaving(false);
    }
  }

  return <>
    <PageHeading eyebrow="SHIFT PLANNER" title="Move work with the whole shift in view." description="A consequential change is previewed before save, preserving the access window, commitments, and SLA calculation." />
    <div className="planner-layout">
      <section className="panel schedule-panel">
        <div className="panel-toolbar"><div><h3>Crew commitments</h3><p>Drag a job in 15-minute increments to preview the change. Shift rows are read-only summaries.</p></div><span className="scope-label">{technicians.length} crews · {data.board?.unassigned.length ?? 0} unassigned</span></div>
        {data.loading ? <LoadingState /> : technicians.length ? <GanttBoard technicians={technicians} onPreview={previewMove} /> :
          <EmptyState title="No crews on shift" body="Try a different region or check that the operations API is connected." />}
      </section>
      <aside className="schedule-sidebar">
        <section className="side-card">
          <span className="eyebrow">EXCEPTION QUEUE</span><h3>Unassigned work</h3>
          <div className="exception-list">{data.board?.unassigned.slice(0, 6).map((job) => <button key={job.id} onClick={() => data.selectJob(job.id)}>
            <span className={`priority-dot ${priorityClass[job.priority]}`} /><div><strong>{job.summary}</strong><small>{job.siteName} · {formatTime(job.sla.dueAt)}</small></div>
          </button>)}</div>
        </section>
        {pending ? <section className="impact-preview">
          <span className="eyebrow">CHANGE PREVIEW</span><h3>Confirm schedule impact</h3>
          <div className="move-summary"><span>{formatDateTime(pending.job.scheduledStart)}</span><b>→</b><strong>{formatDateTime(pending.newStart)}</strong></div>
          <dl><div><dt>Job</dt><dd>{pending.job.summary}</dd></div><div><dt>Crew</dt><dd>{pending.technician.technicianName}</dd></div><div><dt>Access window</dt><dd>{formatTime(pending.job.windowStart)}–{formatTime(pending.job.windowEnd)}</dd></div><div><dt>SLA</dt><dd>{slaLabel(pending.job)}</dd></div></dl>
          <div className="impact-note"><strong>Revalidation on save</strong><span>Qualifications, overlap, shift bounds, travel, and downstream commitments are checked by the API.</span></div>
          {previewError && <div className="validation-issue blocking">{previewError}</div>}
          <div className="button-row"><IgrButton variant="flat" onClick={() => setPending(null)}>Keep original</IgrButton><IgrButton disabled={saving} onClick={() => void confirmMove()}>{saving ? 'Saving…' : 'Confirm move'}</IgrButton></div>
        </section> : <section className="side-card quiet"><strong>No pending schedule changes</strong><span>Select or drag a commitment to open a pre-save impact preview.</span></section>}
      </aside>
    </div>
    <DetailPanel />
  </>;
}

export function Handover() {
  const data = useDispatch();
  const [owner, setOwner] = useState('Jordan Lee · Incoming S3');
  const [note, setNote] = useState('');
  const [jobId, setJobId] = useState<number | ''>('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const handoverRows = useMemo(() => data.jobs.filter((job) =>
    !['Completed', 'Cancelled'].includes(job.status) && (job.sla.state === 'AtRisk' || job.sla.state === 'Breached' || job.status === 'OnHold' || job.status === 'Unassigned')
  ).slice(0, 60), [data.jobs]);

  async function submitNote(event: FormEvent) {
    event.preventDefault();
    if (!jobId || !note.trim()) return;
    setSaving(true);
    setError(null);
    try {
      await data.addHandoverNote(Number(jobId), `HANDOVER to ${owner}: ${note.trim()}`);
      setNote('');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Handover note was not saved.');
    } finally {
      setSaving(false);
    }
  }

  return <div className={`route-layout ${data.selectedId ? 'with-detail' : ''}`}>
    <div className="route-main">
      <PageHeading eyebrow="SHIFT HANDOVER" title="Transfer ownership, restrictions, and next action." description="Capture unresolved work and pending operational approvals for a named incoming owner." />
      <div className="handover-overview">
        <section className="panel handover-summary"><div><span className="eyebrow">CURRENT SHIFT</span><h3>S2 · 06:00–14:00</h3></div><div className="handover-flow"><span className="avatar">AM</span><div><small>Outgoing</small><strong>Alex Morgan</strong></div><b>→</b><span className="avatar incoming">JL</span><div><small>Incoming owner</small><strong>{owner}</strong></div></div></section>
        <section className="panel owner-form"><label>Named incoming owner<select value={owner} onChange={(event) => setOwner(event.target.value)}><option>Jordan Lee · Incoming S3</option><option>Taylor Brooks · Incoming S3</option><option>Morgan Chen · Shift supervisor</option></select></label><div className="handover-ready"><span>Ready for review</span><strong>{handoverRows.length} unresolved items</strong></div></section>
      </div>
      <section className="panel">
        <div className="panel-toolbar"><div><h3>Unresolved faults and restrictions</h3><p>Every item requires current status, owner, and an explicit next action.</p></div><span className="scope-label">Incoming review required</span></div>
        {handoverRows.length ? <IgrGridLite ref={fillGridViewport} className="jobs-grid compact" data={handoverRows} autoGenerate={false}>
          <IgrGridLiteColumn field="summary" header="Handover item" cellTemplate={(ctx: IgrCellContext<Job>) => ctx.row.data ?
            <JobCell job={ctx.row.data} onOpen={() => data.selectJob(ctx.row.data!.id)} /> : null} />
          <IgrGridLiteColumn field="priority" header="Priority" cellTemplate={(ctx: IgrCellContext<Job>) => ctx.row.data ? <span className={`priority-pill ${priorityClass[ctx.row.data.priority]}`}>{ctx.row.data.priority}</span> : null} />
          <IgrGridLiteColumn field="sla" header="Risk" cellTemplate={(ctx: IgrCellContext<Job>) => ctx.row.data ? <SlaBadge job={ctx.row.data} /> : null} />
          <IgrGridLiteColumn field="assignedTechnicianName" header="Owner" cellTemplate={(ctx: IgrCellContext<Job>) => ctx.row.data ?
            <div className="stacked-cell"><span>{ctx.row.data.assignedTechnicianName || 'Unassigned'}</span><small>{ctx.row.data.status}</small></div> : null} />
          <IgrGridLiteColumn field="jobNumber" header="Required next action" cellTemplate={(ctx: IgrCellContext<Job>) => ctx.row.data ?
            <button className="outline-button" onClick={() => { data.selectJob(ctx.row.data!.id); setJobId(ctx.row.data!.id); }}>Add handover note</button> : null} />
        </IgrGridLite> : <EmptyState title="No unresolved work" body="The active queue has no handover exceptions in the current scope." />}
      </section>
      <form className="panel handover-note" onSubmit={(event) => void submitNote(event)}>
        <div><span className="eyebrow">AUDITABLE HANDOVER NOTE</span><h3>Attach next action to the job record</h3><p>Use a job selection to capture the owner, approval dependency, and next operational step.</p></div>
        <label>Job<select value={jobId} onChange={(event) => setJobId(event.target.value ? Number(event.target.value) : '')} required><option value="">Select unresolved job</option>{handoverRows.map((job) => <option key={job.id} value={job.id}>{job.jobNumber} · {job.summary}</option>)}</select></label>
        <label className="note-field">Next action<textarea value={note} onChange={(event) => setNote(event.target.value)} placeholder="Owner, restriction or approval dependency, exact next action…" rows={4} required /></label>
        {error && <div className="validation-issue blocking">{error}</div>}
        <IgrButton disabled={saving || !jobId || !note.trim()}>{saving ? 'Saving handover…' : 'Save to job record'}</IgrButton>
      </form>
    </div>
    <DetailPanel />
  </div>;
}
