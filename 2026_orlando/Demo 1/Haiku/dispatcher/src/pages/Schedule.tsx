import { useMemo, useRef, useState } from 'react';
import { IgrButton, IgrDateTimeInput, IgrSelect, IgrSelectItem, IgrInput, IgrCheckbox, type IgrComponentValueChangedEventArgs, type IgrCheckboxChangeEventArgs } from 'igniteui-react';
import { IgrGridLite, IgrGridLiteColumn, type IgrCellContext } from 'igniteui-react/grid-lite';
import { apiGet, apiSend, ApiError } from '../api/client';
import type { BoardTechnician, DispatchBoard, Job, RescheduleReason } from '../api/types';
import { useConsole } from '../context/console';
import { useQuery } from '../hooks/useQuery';
import { ja } from '../i18n/ja';
import { fmtDateTime, fmtTime, minutesBetween, toLocalIso } from '../lib/format';
import { SlaChip, Freshness, UpdatedAt, StatusText } from '../components/common';
import { ImpactDialog, IssueList, type ImpactIssue } from '../components/ImpactDialog';
import { cell } from "../lib/cellStyle";
import { fillGrid } from "../lib/gridFill";

const REASONS: RescheduleReason[] = ['CustomerRequest', 'TechnicianUnavailable', 'PartsDelay', 'Overrun', 'Weather', 'SlaRebalance', 'Other'];
const REASON_JA: Record<RescheduleReason, string> = {
  CustomerRequest: '顧客要望', TechnicianUnavailable: '作業員不可', PartsDelay: '部材遅延', Overrun: '作業超過',
  Weather: '天候', SlaRebalance: 'SLA再配分', Other: 'その他',
};

/** Impact preview computed from the board: overlaps, shift bounds and SLA. */
function previewImpact(job: Job, tech: BoardTechnician, start: Date, reason: RescheduleReason): ImpactIssue[] {
  const issues: ImpactIssue[] = [];
  const startIso = toLocalIso(start);
  const endIso = toLocalIso(new Date(start.getTime() + job.estimatedDurationMinutes * 60_000));
  if (tech.onTimeOff) issues.push({ code: 'TIME_OFF', severity: 'Blocking', message: '作業員は休暇中です。' });
  if (!tech.shiftStart || !tech.shiftEnd) issues.push({ code: 'NO_SHIFT', severity: 'Blocking', message: 'この日のシフトがありません。' });
  else if (startIso < tech.shiftStart || endIso > tech.shiftEnd) {
    issues.push({ code: 'AFTER_SHIFT', severity: 'Warning', message: `シフト時間外（${fmtTime(tech.shiftStart)}–${fmtTime(tech.shiftEnd)}）に作業が及びます。` });
  }
  for (const other of tech.jobs) {
    if (other.id === job.id || !other.scheduledStart || !other.scheduledEnd) continue;
    if (startIso < other.scheduledEnd && endIso > other.scheduledStart) {
      issues.push({ code: 'OVERLAP', severity: 'Blocking', message: `${other.jobNumber}（${fmtTime(other.scheduledStart)}–${fmtTime(other.scheduledEnd)}）と重なります。` });
    }
  }
  if (new Date(startIso).getTime() > new Date(job.responseDueAt).getTime() && reason !== 'CustomerRequest') {
    issues.push({ code: 'SLA_RESPONSE', severity: 'Warning', message: `応答期限 ${fmtDateTime(job.responseDueAt)} を超過します。` });
  }
  return issues;
}

export default function Schedule() {
  const c = useConsole();
  const date = (c.clock.data?.today ?? new Date().toISOString().slice(0, 10));
  const board = useQuery<DispatchBoard>(`board|${date}|${c.regionId}`, (s) =>
    apiGet<DispatchBoard>('/api/dispatch/board', { date, regionId: c.regionId }, s), 30_000);

  const [techId, setTechId] = useState<number | null>(null);
  const [jobId, setJobId] = useState<number | null>(null);
  const [start, setStart] = useState<Date | null>(null);
  const [reason, setReason] = useState<RescheduleReason>('TechnicianUnavailable');
  const [comment, setComment] = useState('');
  const [override, setOverride] = useState(false);
  const [outcome, setOutcome] = useState<{ kind: 'saved' | 'rejected' | 'conflict'; text: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const dialogRef = useRef<import('igniteui-react').IgrDialog>(null);

  const tech = board.data?.technicians.find((t) => t.technicianId === techId) ?? null;
  const job = tech?.jobs.find((j) => j.id === jobId) ?? null;
  const impact = useMemo(() => (job && tech && start ? previewImpact(job, tech, start, reason) : []), [job, tech, start, reason]);
  const blocking = impact.some((i) => i.severity === 'Blocking');

  async function apply() {
    if (!job || !start || !tech) return;
    setBusy(true);
    try {
      const latest = await apiGet<{ job: Job }>(`/api/jobs/${job.id}`);
      if (latest.job.updatedAt !== job.updatedAt) {
        setOutcome({ kind: 'conflict', text: ja.state.conflict });
        board.refetch();
        dialogRef.current?.hide();
        return;
      }
      await apiSend('POST', `/api/jobs/${job.id}/reschedule`, {
        newScheduledStart: toLocalIso(start),
        technicianId: tech.technicianId,
        reasonCode: reason,
        comment: comment || null,
        dispatcherId: c.dispatcher?.id ?? null,
        updateWindow: false,
        force: override,
      });
      setOutcome({ kind: 'saved', text: `${ja.schedule.saved}: ${job.jobNumber}` });
      dialogRef.current?.hide();
      setJobId(null);
      setComment('');
      setOverride(false);
      board.refetch();
    } catch (e) {
      const issues = e instanceof ApiError ? (e.problem.data?.issues ?? []) : [];
      setOutcome({ kind: 'rejected', text: `${ja.schedule.rejected}${issues.length ? '：' + issues.map((i) => i.code).join(', ') : ''}` });
      dialogRef.current?.hide();
    } finally {
      setBusy(false);
    }
  }

  const techCols = {
    name: (ctx: IgrCellContext<BoardTechnician, any>) => (
      <button type="button" style={cell.btn(ctx.row!.data!.technicianId === techId)} aria-pressed={ctx.row!.data!.technicianId === techId}
        onClick={() => { setTechId(ctx.row!.data!.technicianId); setJobId(null); setStart(null); }}>
        <span style={cell.l1}>{ctx.row!.data!.technicianName}</span>
        <span style={cell.l2}>{ctx.row!.data!.depotName} · {ctx.row!.data!.level}</span>
      </button>
    ),
    shift: (ctx: IgrCellContext<BoardTechnician, any>) => (
      <span style={cell.stack}>
        <span style={cell.l1}>{ctx.row!.data!.onTimeOff ? '休暇' : `${fmtTime(ctx.row!.data!.shiftStart)}–${fmtTime(ctx.row!.data!.shiftEnd)}`}</span>
        <span style={cell.l2}>{ctx.row!.data!.skillCodes}</span>
      </span>
    ),
    util: (ctx: IgrCellContext<BoardTechnician, any>) => (
      <span style={cell.stack}>
        <span style={cell.l1}>{ctx.row!.data!.utilizationPct.toFixed(0)}%</span>
        <span style={cell.l2}>{ctx.row!.data!.jobs.length} 件 · {ctx.row!.data!.bookedMinutes}分</span>
      </span>
    ),
  };

  const jobCols = {
    job: (ctx: IgrCellContext<Job, any>) => (
      <button type="button" style={cell.btn(ctx.row!.data!.id === jobId)} aria-pressed={ctx.row!.data!.id === jobId} onClick={() => {
        setJobId(ctx.row!.data!.id);
        setStart(ctx.row!.data!.scheduledStart ? new Date(ctx.row!.data!.scheduledStart) : null);
      }}>
        <span style={cell.l1}>{ctx.row!.data!.jobNumber}</span>
        <span style={cell.l2}>{ctx.row!.data!.summary}</span>
      </button>
    ),
    time: (ctx: IgrCellContext<Job, any>) => (
      <span style={cell.stack}><span style={cell.l1}>{fmtTime(ctx.row!.data!.scheduledStart)}–{fmtTime(ctx.row!.data!.scheduledEnd)}</span><span style={cell.l2}>{ctx.row!.data!.estimatedDurationMinutes}分</span></span>
    ),
    status: (ctx: IgrCellContext<Job, any>) => <StatusText status={ctx.row!.data!.status} />,
    sla: (ctx: IgrCellContext<Job, any>) => <SlaChip state={ctx.row!.data!.sla.state} />,
  };
  void minutesBetween;

  return (
    <section className="schedule" aria-labelledby="sched-title">
      <div className="page-head">
        <h1 id="sched-title" className="page-title">{ja.schedule.title}</h1>
        <div className="toolbar">
          <span className="muted">{date}</span>
          <IgrButton variant="outlined" onClick={() => board.refetch()}>{ja.common.refresh}</IgrButton>
          <UpdatedAt at={board.lastUpdated} />
        </div>
      </div>
      <Freshness stale={board.stale} disconnected={board.disconnected} onRetry={board.refetch} />
      {outcome && <div className={`banner banner-${outcome.kind === 'saved' ? 'ok' : outcome.kind === 'conflict' ? 'warn' : 'error'}`} role="status">{outcome.text}</div>}

      <div className="sched-body">
        <div className="pane">
          <div className="pane-head"><h2 className="pane-title">{ja.schedule.board}</h2><span className="muted">{board.data?.technicians.length ?? 0} 名</span></div>
          {board.loading && !board.data && <p className="muted">{ja.state.loading}</p>}
          {board.data && (
            <IgrGridLite ref={fillGrid} className="grid-fill" data={board.data.technicians} autoGenerate={false}>
              <IgrGridLiteColumn field="technicianName" header={ja.common.crew} width="230px" cellTemplate={techCols.name} />
              <IgrGridLiteColumn field="shiftStart" header="シフト" width="170px" cellTemplate={techCols.shift} />
              <IgrGridLiteColumn field="utilizationPct" header={ja.schedule.utilization} width="130px" cellTemplate={techCols.util} />
            </IgrGridLite>
          )}
        </div>

        <div className="pane">
          <div className="pane-head"><h2 className="pane-title">{ja.schedule.dayJobs}{tech ? `：${tech.technicianName}` : ''}</h2></div>
          {!tech && <p className="empty">{ja.common.noSelection}</p>}
          {tech && tech.jobs.length === 0 && <p className="empty">{ja.state.empty}</p>}
          {tech && tech.jobs.length > 0 && (
            <IgrGridLite ref={fillGrid} className="grid-fill" data={tech.jobs} autoGenerate={false}>
              <IgrGridLiteColumn field="jobNumber" header={ja.common.job} width="250px" cellTemplate={jobCols.job} />
              <IgrGridLiteColumn field="scheduledStart" header={ja.common.window} width="140px" cellTemplate={jobCols.time} />
              <IgrGridLiteColumn field="status" header="状態" width="120px" cellTemplate={jobCols.status} />
              <IgrGridLiteColumn field="sla" header={ja.common.slaState} width="130px" cellTemplate={jobCols.sla} />
            </IgrGridLite>
          )}
        </div>

        <aside className="detail-pane" aria-label={ja.schedule.impact}>
          {(!job || !tech) && <p className="empty">{ja.common.noSelection}</p>}
          {job && tech && (
            <>
              <h2 className="pane-title">{job.jobNumber}</h2>
              <p className="detail-summary">{job.summary}</p>
              <dl className="facts">
                <dt>{ja.common.assignedTo}</dt><dd>{tech.technicianName}</dd>
                <dt>現在の時刻</dt><dd>{fmtDateTime(job.scheduledStart)}</dd>
                <dt>{ja.common.due}</dt><dd>{fmtDateTime(job.responseDueAt)}</dd>
              </dl>
              <IgrDateTimeInput displayFormat="yyyy/MM/dd HH:mm" inputFormat="yyyy/MM/dd HH:mm" label={ja.schedule.newStart} value={start ?? undefined}
                onChange={(e: CustomEvent<any>) => setStart(e.detail ?? null)} />
              <IgrSelect label={ja.schedule.reason} value={reason}
                onChange={(e: CustomEvent<any>) => setReason(e.detail.value as RescheduleReason)}>
                {REASONS.map((r) => <IgrSelectItem key={r} value={r}>{REASON_JA[r]}</IgrSelectItem>)}
              </IgrSelect>
              <IgrInput label={ja.schedule.comment} value={comment}
                onChange={(e: IgrComponentValueChangedEventArgs) => setComment(String(e.detail ?? ''))} />
              <h3 className="pane-subtitle">{ja.schedule.impact}</h3>
              {start ? <IssueList issues={impact} /> : <p className="muted">新しい開始日時を入力してください。</p>}
              {start && impact.length === 0 && <p className="ok-line">✔ 影響はありません</p>}
              {blocking && c.canOverride && (
                <IgrCheckbox checked={override} onChange={(e: IgrCheckboxChangeEventArgs) => setOverride(e.detail.checked)}>{ja.common.force}</IgrCheckbox>
              )}
              <IgrButton variant="contained" disabled={!start || (blocking && !override) || busy} onClick={() => dialogRef.current?.show()}>
                {ja.schedule.apply}
              </IgrButton>
            </>
          )}
        </aside>
      </div>

      <ImpactDialog ref={dialogRef} title={`${ja.schedule.impact}：${job?.jobNumber ?? ''}`} confirmLabel={ja.schedule.apply}
        confirmDisabled={busy || !start || (blocking && !override)} onConfirm={apply}>
        {start && <p>{tech?.technicianName}：{fmtDateTime(toLocalIso(start))} へ移動（{REASON_JA[reason]}）</p>}
        <IssueList issues={impact} />
      </ImpactDialog>
    </section>
  );
}
