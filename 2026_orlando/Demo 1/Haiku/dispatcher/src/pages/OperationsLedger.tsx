import { useMemo, useRef, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { IgrButton, IgrSelect, IgrSelectItem, IgrInput, IgrDialog, IgrCheckbox, IgrLinearProgress, type IgrComponentValueChangedEventArgs, type IgrCheckboxChangeEventArgs } from 'igniteui-react';
import { IgrGridLite, IgrGridLiteColumn, type IgrCellContext } from 'igniteui-react/grid-lite';
import { apiGet, apiSend, ApiError } from '../api/client';
import type { Job, JobDetail, Paged, Recommendation, Validation, JobPriority } from '../api/types';
import { useConsole } from '../context/console';
import { useQuery } from '../hooks/useQuery';
import { ja } from '../i18n/ja';
import { fmtDateTime, fmtTime } from '../lib/format';
import { SlaChip, PriorityText, Freshness, UpdatedAt } from '../components/common';
import { ImpactDialog, IssueList } from '../components/ImpactDialog';
import { cell } from "../lib/cellStyle";
import { fillGrid } from "../lib/gridFill";

type Outcome = { kind: 'saved' | 'rejected' | 'conflict'; text: string } | null;
const PRIORITIES: JobPriority[] = ['Critical', 'High', 'Medium', 'Low'];
const SLA_STATES = ['AtRisk', 'Breached', 'OnTrack', 'Met'] as const;

export default function OperationsLedger() {
  const { jobId } = useParams();
  const selectedId = jobId ? Number(jobId) : null;
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const c = useConsole();
  const priority = params.get('priority') ?? '';
  const slaState = params.get('sla') ?? '';
  const q = params.get('q') ?? '';

  const setFilter = (k: string, v: string) => {
    const next = new URLSearchParams(params);
    if (v) next.set(k, v); else next.delete(k);
    setParams(next, { replace: true });
  };

  // Queue: unassigned work in the current region, filtered by URL state.
  const queueKey = `queue|${c.regionId}|${priority}|${slaState}|${q}`;
  const queue = useQuery<Paged<Job>>(queueKey, (s) => apiGet<Paged<Job>>('/api/jobs', {
    status: 'Unassigned', regionId: c.regionId, priority, slaState, search: q, sort: '-priority,responseDueAt', pageSize: 200,
  }, s));

  // Severity first (Critical → Low), then the earliest response deadline.
  const queueRows = useMemo(() => {
    const rank: Record<JobPriority, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 };
    return [...(queue.data?.items ?? [])].sort((a, b) =>
      rank[a.priority] - rank[b.priority] || a.responseDueAt.localeCompare(b.responseDueAt));
  }, [queue.data]);

  const detail = useQuery<JobDetail>(selectedId ? `job|${selectedId}` : null, (s) => apiGet<JobDetail>(`/api/jobs/${selectedId}`, undefined, s), 30_000);
  const recs = useQuery<Recommendation[]>(selectedId ? `rec|${selectedId}` : null, (s) =>
    apiGet<Recommendation[]>(`/api/jobs/${selectedId}/recommendations`, { limit: 15, crossRegion: 'true' }, s), 30_000);

  const [techId, setTechId] = useState<number | null>(null);
  const [override, setOverride] = useState(false);
  const [outcome, setOutcome] = useState<Outcome>(null);
  const [busy, setBusy] = useState(false);
  const dialogRef = useRef<IgrDialog>(null);

  const selectedRec = recs.data?.find((r) => r.technicianId === techId) ?? null;
  const validation = useQuery<Validation>(
    selectedId && techId ? `val|${selectedId}|${techId}` : null,
    (s) => apiGet<Validation>(`/api/jobs/${selectedId}/validate-assignment`, { technicianId: techId!, scheduledStart: selectedRec?.earliestStart ?? undefined }, s),
    0,
  );

  // Conflict check: re-read the job and compare its version before saving.
  async function confirmAssign() {
    if (!selectedId || !techId || !detail.data) return;
    setBusy(true);
    try {
      const latest = await apiGet<JobDetail>(`/api/jobs/${selectedId}`);
      if (latest.job.updatedAt !== detail.data.job.updatedAt) {
        setOutcome({ kind: 'conflict', text: ja.state.conflict });
        detail.refetch();
        dialogRef.current?.hide();
        return;
      }
      const res = await apiSend<{ job: Job }>('POST', `/api/jobs/${selectedId}/assign`, {
        technicianId: techId,
        scheduledStart: selectedRec?.earliestStart ?? null,
        dispatcherId: c.dispatcher?.id ?? null,
        force: override,
        note: null,
      });
      setOutcome({ kind: 'saved', text: `${ja.assign.saved}: ${res.job.jobNumber} → ${res.job.assignedTechnicianName ?? ''}` });
      dialogRef.current?.hide();
      setTechId(null);
      setOverride(false);
      queue.refetch();
      detail.refetch();
      recs.refetch();
    } catch (e) {
      const issues = e instanceof ApiError ? (e.problem.data?.issues ?? []) : [];
      setOutcome({ kind: 'rejected', text: `${ja.assign.rejected}${issues.length ? '：' + issues.map((i) => i.code).join(', ') : ''}` });
      dialogRef.current?.hide();
    } finally {
      setBusy(false);
    }
  }

  const select = (id: number) => {
    setTechId(null);
    setOverride(false);
    setOutcome(null);
    navigate(`/jobs/${id}${params.toString() ? `?${params}` : ''}`);
  };

  // Grid templates are memoised so the grid does not re-create cells on every render.
  const columns = useMemo(() => ({
    job: (ctx: IgrCellContext<Job, any>) => {
      const j = ctx.row!.data!;
      return (
        <button type="button" style={cell.btn(j.id === selectedId)} aria-pressed={j.id === selectedId} onClick={() => select(j.id)}>
          <span style={cell.l1}>{j.jobNumber}</span>
          <span style={cell.l2}>{j.summary}</span>
        </button>
      );
    },
    site: (ctx: IgrCellContext<Job, any>) => (
      <span style={cell.stack}><span style={cell.l1}>{ctx.row!.data!.siteName}</span><span style={cell.l2}>{ctx.row!.data!.siteCity} · {ctx.row!.data!.regionName}</span></span>
    ),
    priority: (ctx: IgrCellContext<Job, any>) => <PriorityText priority={ctx.row!.data!.priority} />,
    sla: (ctx: IgrCellContext<Job, any>) => <SlaChip state={ctx.row!.data!.sla.state} minutes={ctx.row!.data!.sla.minutesRemaining} />,
    skill: (ctx: IgrCellContext<Job, any>) => (
      <span style={cell.stack}><span style={cell.l1}>{ctx.row!.data!.requiredSkillName}</span><span style={cell.l2}>{ctx.row!.data!.estimatedDurationMinutes}分 · {ctx.row!.data!.customerTier}</span></span>
    ),
    due: (ctx: IgrCellContext<Job, any>) => (
      <span style={cell.stack}><span style={cell.l1}>{fmtTime(ctx.row!.data!.responseDueAt)}</span><span style={cell.l2}>解決 {fmtTime(ctx.row!.data!.resolutionDueAt)}</span></span>
    ),
    // selectedId is read at render time; the grid re-renders when queue data or selection changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }), [selectedId, params]);

  const job = detail.data?.job;

  return (
    <section className="ledger" aria-labelledby="ledger-title">
      <div className="page-head">
        <h1 id="ledger-title" className="page-title">{ja.assign.title}</h1>
        <div className="toolbar" role="search">
          <IgrInput className="tb-search" type="search" label={ja.common.search} value={q}
            onChange={(e: IgrComponentValueChangedEventArgs) => setFilter('q', String(e.detail ?? ''))} />
          <IgrSelect className="tb-filter" label={ja.common.priority} value={priority || 'all'}
            onChange={(e: CustomEvent<any>) => setFilter('priority', e.detail.value === 'all' ? '' : String(e.detail.value))}>
            <IgrSelectItem value="all">{ja.common.all}</IgrSelectItem>
            {PRIORITIES.map((p) => <IgrSelectItem key={p} value={p}>{ja.priority[p]}</IgrSelectItem>)}
          </IgrSelect>
          <IgrSelect className="tb-filter" label={ja.common.slaState} value={slaState || 'all'}
            onChange={(e: CustomEvent<any>) => setFilter('sla', e.detail.value === 'all' ? '' : String(e.detail.value))}>
            <IgrSelectItem value="all">{ja.common.all}</IgrSelectItem>
            {SLA_STATES.map((s) => <IgrSelectItem key={s} value={s}>{ja.slaState[s]}</IgrSelectItem>)}
          </IgrSelect>
          <IgrButton variant="outlined" onClick={() => queue.refetch()}>{ja.common.refresh}</IgrButton>
          <UpdatedAt at={queue.lastUpdated} />
        </div>
      </div>

      <Freshness stale={queue.stale} disconnected={queue.disconnected} onRetry={queue.refetch} />
      {outcome && <div className={`banner banner-${outcome.kind === 'saved' ? 'ok' : outcome.kind === 'conflict' ? 'warn' : 'error'}`} role="status">{outcome.text}</div>}

      <div className="ledger-body">
        <div className="queue-pane">
          <div className="pane-head">
            <h2 className="pane-title">{ja.assign.queue}</h2>
            <span className="muted">{queue.data ? `${queue.data.totalCount} 件` : ''}</span>
          </div>
          {queue.loading && !queue.data && <p className="muted" role="status">{ja.state.loading}</p>}
          {queue.data && queue.data.items.length === 0 && <p className="empty">{ja.state.empty}</p>}
          {queue.data && queue.data.items.length > 0 && (
            <IgrGridLite ref={fillGrid} className="grid-fill" data={queueRows} autoGenerate={false}>
              <IgrGridLiteColumn field="jobNumber" header={ja.common.job} width="260px" cellTemplate={columns.job} />
              <IgrGridLiteColumn field="siteName" header={ja.common.site} width="230px" cellTemplate={columns.site} />
              <IgrGridLiteColumn field="priority" header={ja.common.priority} dataType="string" width="110px" sortable cellTemplate={columns.priority} />
              <IgrGridLiteColumn field="sla" header={ja.common.slaState} width="150px" cellTemplate={columns.sla} />
              <IgrGridLiteColumn field="requiredSkillName" header={ja.common.skill} width="220px" cellTemplate={columns.skill} />
              <IgrGridLiteColumn field="responseDueAt" header={ja.common.due} width="130px" cellTemplate={columns.due} />
            </IgrGridLite>
          )}
        </div>

        <aside className="detail-pane" aria-label={ja.common.details}>
          {!selectedId && <p className="empty">{ja.common.noSelection}</p>}
          {selectedId && detail.loading && !job && <IgrLinearProgress indeterminate />}
          {job && detail.data && (
            <>
              <div className="detail-head">
                <h2 className="pane-title">{job.jobNumber}</h2>
                <SlaChip state={job.sla.state} minutes={job.sla.minutesRemaining} />
              </div>
              <p className="detail-summary">{job.summary}</p>
              <dl className="facts">
                <dt>{ja.common.priority}</dt><dd><PriorityText priority={job.priority} /></dd>
                <dt>{ja.common.site}</dt><dd>{job.siteName}（{detail.data.siteAddress}）</dd>
                <dt>{ja.common.skill}</dt><dd>{job.requiredSkillName}</dd>
                <dt>{ja.common.window}</dt><dd>{fmtDateTime(job.windowStart)} – {fmtTime(job.windowEnd)}</dd>
                <dt>{ja.common.due}</dt><dd>応答 {fmtDateTime(job.responseDueAt)} / 解決 {fmtDateTime(job.resolutionDueAt)}</dd>
                <dt>{ja.common.assignedTo}</dt><dd>{job.assignedTechnicianName ?? ja.common.none}</dd>
              </dl>
              {detail.data.siteAccessNotes && <p className="access-note"><strong>▲ 入場条件：</strong>{detail.data.siteAccessNotes}</p>}
              <Freshness stale={detail.stale} disconnected={detail.disconnected} onRetry={detail.refetch} />

              <div className="pane-head">
                <h3 className="pane-subtitle">{ja.assign.recommendations}</h3>
                <UpdatedAt at={recs.lastUpdated} />
              </div>
              {recs.loading && !recs.data && <p className="muted">{ja.state.loading}</p>}
              {recs.data && recs.data.length === 0 && <p className="empty">{ja.assign.noRecommendations}</p>}
              {recs.data && recs.data.length > 0 && (
                <IgrGridLite ref={fillGrid} className="grid-rec" data={recs.data} autoGenerate={false}>
                  <IgrGridLiteColumn field="technicianName" header={ja.common.crew} width="190px" cellTemplate={(ctx: IgrCellContext<Recommendation, any>) => (
                    <button type="button" style={cell.btn(ctx.row!.data!.technicianId === techId)} aria-pressed={ctx.row!.data!.technicianId === techId} onClick={() => setTechId(ctx.row!.data!.technicianId)}>
                      <span style={cell.l1}>{ctx.row!.data!.technicianName}</span>
                      <span style={cell.l2}>{ctx.row!.data!.depotName} · 習熟 {ctx.row!.data!.proficiency}</span>
                    </button>
                  )} />
                  <IgrGridLiteColumn field="earliestStart" header={ja.assign.earliest} width="130px" cellTemplate={(ctx: IgrCellContext<Recommendation, any>) => (
                    <span style={cell.stack}><span style={cell.l1}>{fmtTime(ctx.row!.data!.earliestStart)}</span><span style={cell.l2}>移動 {ctx.row!.data!.travelMinutes}分</span></span>
                  )} />
                  <IgrGridLiteColumn field="jobsToday" header={ja.schedule.utilization} width="120px" cellTemplate={(ctx: IgrCellContext<Recommendation, any>) => (
                    <span style={cell.stack}><span style={cell.l1}>{ctx.row!.data!.jobsToday}/{ctx.row!.data!.maxJobsPerDay} 件</span><span style={cell.l2}>{ctx.row!.data!.utilizationPct.toFixed(0)}%</span></span>
                  )} />
                </IgrGridLite>
              )}

              {selectedRec && (
                <div className="assign-panel">
                  <p><strong>{selectedRec.technicianName}</strong> を選択中</p>
                  {validation.data && <IssueList issues={validation.data.issues} />}
                  {validation.data?.canAssign && validation.data.issues.length === 0 && <p className="ok-line">✔ 割当に問題はありません</p>}
                  <IgrButton variant="contained" onClick={() => dialogRef.current?.show()} disabled={!validation.data}>
                    {ja.assign.assignButton}
                  </IgrButton>
                </div>
              )}
            </>
          )}
        </aside>
      </div>

      <ImpactDialog
        ref={dialogRef}
        title={`${ja.assign.impact}：${job?.jobNumber ?? ''}`}
        confirmLabel={ja.assign.assignButton}
        confirmDisabled={busy || !validation.data || (!validation.data.canAssign && !(c.canOverride && override))}
        onConfirm={confirmAssign}
      >
        {selectedRec && validation.data && (
          <>
            <dl className="facts">
              <dt>{ja.common.crew}</dt><dd>{selectedRec.technicianName}</dd>
              <dt>{ja.assign.proposedStart}</dt><dd>{fmtDateTime(validation.data.proposedStart)}</dd>
              <dt>{ja.assign.proposedEnd}</dt><dd>{fmtDateTime(validation.data.proposedEnd)}</dd>
            </dl>
            <IssueList issues={validation.data.issues} />
            {!validation.data.canAssign && c.canOverride && (
              <IgrCheckbox checked={override} onChange={(e: IgrCheckboxChangeEventArgs) => setOverride(e.detail.checked)}>
                {ja.common.force}
              </IgrCheckbox>
            )}
          </>
        )}
      </ImpactDialog>
    </section>
  );
}

export type { IgrDialog };
