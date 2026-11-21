import { useMemo, useState } from 'react';
import { IgrButton, IgrSelect, IgrSelectItem, IgrInput, type IgrComponentValueChangedEventArgs } from 'igniteui-react';
import { IgrGridLite, IgrGridLiteColumn, type IgrCellContext } from 'igniteui-react/grid-lite';
import { apiGet, apiSend, ApiError } from '../api/client';
import type { Job, Paged, Dispatcher } from '../api/types';
import { useConsole } from '../context/console';
import { useQuery } from '../hooks/useQuery';
import { ja } from '../i18n/ja';
import { fmtTime } from '../lib/format';
import { SlaChip, PriorityText, StatusText, Freshness, UpdatedAt } from '../components/common';
import { cell } from "../lib/cellStyle";
import { fillGrid } from "../lib/gridFill";

const CLOSED = new Set(['Completed', 'Cancelled']);

export default function Handover() {
  const c = useConsole();
  const unresolved = useQuery<Paged<Job>>(`handover|${c.regionId}`, (s) =>
    apiGet<Paged<Job>>('/api/jobs', { regionId: c.regionId, openOnly: 'true', sort: '-priority,responseDueAt', pageSize: 500 }, s), 30_000);
  const [picked, setPicked] = useState<Set<number>>(new Set());
  const [incoming, setIncoming] = useState<number | null>(null);
  const [note, setNote] = useState('');
  const [result, setResult] = useState<{ kind: 'ok' | 'error'; text: string } | null>(null);
  const [busy, setBusy] = useState(false);

  const rows = useMemo(() => (unresolved.data?.items ?? []).filter((j) => !CLOSED.has(j.status)), [unresolved.data]);
  const incomingOptions = c.dispatchers.filter((d: Dispatcher) => d.id !== c.dispatcher?.id);
  const incomingName = incomingOptions.find((d) => d.id === incoming)?.fullName ?? '';

  const toggle = (id: number) => setPicked((prev) => {
    const next = new Set(prev);
    if (next.has(id)) next.delete(id); else next.add(id);
    return next;
  });

  async function record() {
    if (picked.size === 0) { setResult({ kind: 'error', text: ja.handover.selectAtLeastOne }); return; }
    setBusy(true);
    setResult(null);
    const body = `【引継ぎ】引継ぎ先：${incomingName || '未指定'} / ${note.trim() || '（メモなし）'}`;
    let ok = 0;
    try {
      for (const id of picked) {
        await apiSend('POST', `/api/jobs/${id}/notes`, {
          authorName: c.dispatcher?.fullName ?? 'Dispatcher',
          body,
          isInternal: true,
        });
        ok++;
      }
      setResult({ kind: 'ok', text: `${ja.handover.recorded}（${ok} 件）` });
      setPicked(new Set());
      setNote('');
    } catch (e) {
      setResult({ kind: 'error', text: `${ja.state.error}（${ok} 件記録済）${e instanceof ApiError ? ` ${e.message}` : ''}` });
    } finally {
      setBusy(false);
    }
  }

  const cols = {
    pick: (ctx: IgrCellContext<Job, any>) => (
      <button type="button" style={cell.btn(picked.has(ctx.row!.data!.id))} aria-pressed={picked.has(ctx.row!.data!.id)} onClick={() => toggle(ctx.row!.data!.id)}>
        <span style={cell.l1}><span aria-hidden="true">{picked.has(ctx.row!.data!.id) ? '☑ ' : '☐ '}</span>{ctx.row!.data!.jobNumber}</span>
        <span style={cell.l2}>{ctx.row!.data!.summary}</span>
      </button>
    ),
    // picked is read via closure; grid is re-rendered when picked changes because cols are rebuilt below.
    priority: (ctx: IgrCellContext<Job, any>) => <PriorityText priority={ctx.row!.data!.priority} />,
    status: (ctx: IgrCellContext<Job, any>) => <StatusText status={ctx.row!.data!.status} />,
    sla: (ctx: IgrCellContext<Job, any>) => <SlaChip state={ctx.row!.data!.sla.state} />,
    owner: (ctx: IgrCellContext<Job, any>) => (
      <span style={cell.stack}><span style={cell.l1}>{ctx.row!.data!.assignedTechnicianName ?? ja.common.none}</span><span style={cell.l2}>{ctx.row!.data!.siteName} · {fmtTime(ctx.row!.data!.responseDueAt)}</span></span>
    ),
  };

  return (
    <section className="handover" aria-labelledby="handover-title">
      <div className="page-head">
        <h1 id="handover-title" className="page-title">{ja.handover.title}</h1>
        <div className="toolbar">
          <span className="muted">{picked.size} 件選択中</span>
          <IgrButton variant="outlined" onClick={() => unresolved.refetch()}>{ja.common.refresh}</IgrButton>
          <UpdatedAt at={unresolved.lastUpdated} />
        </div>
      </div>
      <Freshness stale={unresolved.stale} disconnected={unresolved.disconnected} onRetry={unresolved.refetch} />
      {result && <div className={`banner banner-${result.kind === 'ok' ? 'ok' : 'error'}`} role="status">{result.text}</div>}

      <div className="handover-body">
        <div className="pane">
          <div className="pane-head"><h2 className="pane-title">{ja.handover.unresolved}</h2><span className="muted">{rows.length} 件</span></div>
          {unresolved.loading && !unresolved.data && <p className="muted">{ja.state.loading}</p>}
          {rows.length === 0 && unresolved.data && <p className="empty">{ja.state.empty}</p>}
          {rows.length > 0 && (
            <IgrGridLite ref={fillGrid} className="grid-fill" data={rows} autoGenerate={false}>
              <IgrGridLiteColumn field="jobNumber" header={ja.common.job} width="300px" cellTemplate={cols.pick} />
              <IgrGridLiteColumn field="priority" header={ja.common.priority} width="120px" cellTemplate={cols.priority} />
              <IgrGridLiteColumn field="status" header="状態" width="130px" cellTemplate={cols.status} />
              <IgrGridLiteColumn field="sla" header={ja.common.slaState} width="130px" cellTemplate={cols.sla} />
              <IgrGridLiteColumn field="assignedTechnicianName" header={ja.common.assignedTo} width="260px" cellTemplate={cols.owner} />
            </IgrGridLite>
          )}
        </div>
        <aside className="detail-pane" aria-label={ja.handover.title}>
          <h2 className="pane-title">{ja.handover.title}</h2>
          <p className="muted">{ja.common.assignedTo}：{c.dispatcher?.fullName ?? '—'}</p>
          <IgrSelect label={ja.handover.incoming} value={incoming ? String(incoming) : ''}
            onChange={(e: CustomEvent<any>) => setIncoming(Number(e.detail.value))}>
            {incomingOptions.map((d) => <IgrSelectItem key={d.id} value={String(d.id)}>{d.fullName}</IgrSelectItem>)}
          </IgrSelect>
          <IgrInput label={ja.handover.note} value={note}
            onChange={(e: IgrComponentValueChangedEventArgs) => setNote(String(e.detail ?? ''))} />
          <p className="muted small">{ja.handover.recorded}</p>
          <IgrButton variant="contained" disabled={busy || picked.size === 0 || incoming === null} onClick={() => void record()}>
            {ja.handover.complete}
          </IgrButton>
        </aside>
      </div>
    </section>
  );
}
