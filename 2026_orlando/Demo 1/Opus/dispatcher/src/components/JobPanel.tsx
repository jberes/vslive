import { IgrButton, IgrIconButton, IgrInput, IgrSelect, IgrSelectItem, IgrTextarea } from 'igniteui-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client';
import type { JobListItem, JobPriority, JobStatus } from '../api/types';
import { ConflictError, save } from '../lib/conflict';
import { hasSafetyRestriction, NEXT_STATUS, nextAction, riskCause, STATUS_META } from '../lib/status';
import { dayTime, duration, hm, remaining } from '../lib/time';
import { useSession } from '../state/session';
import { usePolling } from '../state/usePolling';
import { AssignDialog } from './AssignDialog';
import { ConflictDialog } from './ConflictDialog';
import { Icon } from './Icon';
import { RescheduleDialog } from './RescheduleDialog';
import { EmptyState, ErrorState, Freshness, LoadingBlock } from './States';
import { PriorityTag, SlaTag, StatusText } from './Tags';

export function JobPanel({
  jobId, listItem, onClose, onChanged, full = false,
}: {
  jobId: number | null;
  listItem?: JobListItem | null;
  onClose?: () => void;
  onChanged?: () => void;
  /** Full-page record (route /jobs/:id) shows the complete timeline and history. */
  full?: boolean;
}) {
  const s = useSession();
  const detail = usePolling(jobId ? `job-${jobId}` : null, sig => api.job(jobId!, sig));
  const [assignOpen, setAssignOpen] = useState(false);
  const [rescheduleOpen, setRescheduleOpen] = useState(false);
  const [conflict, setConflict] = useState<ConflictError | null>(null);
  const [retry, setRetry] = useState<(() => void) | null>(null);
  const [busy, setBusy] = useState(false);
  const [holdReason, setHoldReason] = useState('');
  const [holding, setHolding] = useState(false);
  const [note, setNote] = useState('');
  const [newPrio, setNewPrio] = useState<JobPriority | null>(null);

  if (!jobId) {
    return (
      <aside className="panel panel-empty" aria-label="Job details">
        <EmptyState icon="info" title="Select a job">
          Choose a row to see its location, restrictions, service targets and crew. Your filters stay as they are.
        </EmptyState>
      </aside>
    );
  }
  if (detail.loading || !detail.data) {
    return (
      <aside className="panel" aria-label="Job details">
        {detail.error ? <ErrorState error={detail.error} onRetry={detail.refresh} /> : <LoadingBlock label="Loading job…" rows={8} />}
      </aside>
    );
  }

  const d = detail.data;
  const j = d.job;
  const done = () => { detail.refresh(); onChanged?.(); };
  const who = s.user!;

  const run = async (label: string, action: () => Promise<unknown>, success: string, again?: () => void) => {
    setBusy(true);
    const r = await save({ seen: j, label, action, success: () => ({ title: success, detail: `${j.jobNumber} · recorded as ${who.fullName}` }) });
    setBusy(false);
    if (r.kind === 'conflict') { setConflict(r.error); setRetry(() => again ?? null); }
    if (r.kind === 'saved') done();
    return r;
  };

  const changeStatus = (to: JobStatus, extra?: { onHoldReason?: string }) =>
    run(
      'Status change',
      () => api.setStatus(j.id, { status: to, dispatcherId: who.id, ...extra }),
      `${STATUS_META[to].label}: status saved`,
    );

  const applyPriority = async () => {
    if (!newPrio || newPrio === j.priority) return;
    const r = await run('Priority change', () => api.updateJob(j.id, { priority: newPrio }), `Priority set to ${newPrio}`);
    if (r.kind === 'saved') {
      await api.addNote(j.id, { authorName: who.fullName, body: `Priority changed ${j.priority} → ${newPrio} by ${who.fullName} (${who.role}).` }).catch(() => undefined);
      setNewPrio(null);
    }
  };

  const addNote = async () => {
    if (!note.trim()) return;
    const r = await run('Note', () => api.addNote(j.id, { authorName: who.fullName, body: note.trim() }), 'Note added', undefined);
    if (r.kind === 'saved') setNote('');
  };

  const nexts = NEXT_STATUS[j.status] ?? [];
  const restriction = hasSafetyRestriction(d);
  const open = STATUS_META[j.status].open;
  const events = [...d.events].sort((a, b) => b.occurredAt.localeCompare(a.occurredAt));
  const notes = [...d.notes].sort((a, b) => b.createdAt.localeCompare(a.createdAt));

  return (
    <aside className={`panel ${full ? 'panel-full' : ''}`} aria-label={`Job ${j.jobNumber}`}>
      <header className="panel-head">
        <nav className="crumbs" aria-label="Location">
          <span>{j.regionName}</span><Icon name="chevron_right" />
          <span>{j.customerName}</span><Icon name="chevron_right" />
          <span>{j.siteName}</span>
          {d.assetTag ? <><Icon name="chevron_right" /><span>{d.assetTag}</span></> : null}
        </nav>
        <div className="panel-title">
          <h2>{j.summary}</h2>
          {onClose ? (
            <IgrIconButton variant="flat" aria-label="Close job details" onClick={onClose}><Icon name="close" /></IgrIconButton>
          ) : null}
        </div>
        <div className="panel-tags">
          <span className="mono">{j.jobNumber}</span>
          <PriorityTag priority={j.priority} />
          <SlaTag state={j.sla.state} />
          <StatusText status={j.status} />
        </div>
        {listItem && listItem.updatedAt !== j.updatedAt ? (
          <p className="callout tone-info small"><Icon name="info" /> This job changed since the queue last refreshed.</p>
        ) : null}
        <Freshness updatedAt={detail.updatedAt} refreshing={detail.refreshing} error={detail.error} />
      </header>

      {open ? (
        <div className="panel-actions">
          {j.status === 'Unassigned' ? (
            <IgrButton onClick={() => setAssignOpen(true)} disabled={busy}><Icon name="person_add" /> Assign crew</IgrButton>
          ) : (
            <>
              <IgrButton variant="outlined" onClick={() => setAssignOpen(true)} disabled={busy}><Icon name="swap" /> Reassign</IgrButton>
              <IgrButton variant="outlined" onClick={() => setRescheduleOpen(true)} disabled={busy}><Icon name="event" /> Reschedule</IgrButton>
            </>
          )}
          {nexts.filter(n => n !== 'OnHold').map(n => (
            <IgrButton key={n} variant={j.status === 'Unassigned' ? 'outlined' : 'contained'} onClick={() => changeStatus(n)} disabled={busy}>
              {j.status === 'OnHold' && n === 'Assigned' ? 'Release hold' : `Mark ${STATUS_META[n].label.toLowerCase()}`}
            </IgrButton>
          ))}
          {nexts.includes('OnHold') ? (
            <IgrButton variant="flat" onClick={() => setHolding(h => !h)} disabled={busy}><Icon name="pause" /> Hold</IgrButton>
          ) : null}
          {!full ? (
            <Link className="link-btn" to={`/jobs/${j.id}`}><Icon name="open_in_new" /> Full record</Link>
          ) : null}
        </div>
      ) : null}
      {holding ? (
        <div className="inline-form">
          <IgrInput label="Reason for hold (required)" value={holdReason} onInput={e => setHoldReason(e.detail)} />
          <IgrButton disabled={!holdReason.trim() || busy} onClick={async () => {
            const r = await changeStatus('OnHold', { onHoldReason: holdReason.trim() });
            if (r.kind === 'saved') { setHolding(false); setHoldReason(''); }
          }}>Put on hold</IgrButton>
        </div>
      ) : null}

      <div className="panel-scroll">
        {open && j.sla.state !== 'OnTrack' ? (
          <section className={`next-action tone-${j.sla.state === 'Breached' ? 'crit' : 'warn'}`}>
            <Icon name="arrow_up" />
            <div>
              <strong>Next action: {nextAction(j)}</strong>
              <span>{riskCause(j)}</span>
            </div>
          </section>
        ) : null}

        <section className="section">
          <h3><Icon name="shield" /> Safety &amp; access restrictions</h3>
          {restriction ? (
            <p className="callout tone-crit"><Icon name="octagon" /><span><strong>Operating restriction in force.</strong> {d.description}</span></p>
          ) : null}
          <dl className="kv">
            <dt>Track access</dt>
            <dd>{d.siteAccessNotes ? <strong>{d.siteAccessNotes}</strong> : <span className="muted">No access constraints recorded</span>}</dd>
            <dt>Location</dt>
            <dd>{d.siteAddress}, {j.siteCity}</dd>
            {d.assetTag ? <><dt>Asset</dt><dd>{d.assetTag}{d.assetDescription ? ` · ${d.assetDescription}` : ''}</dd></> : null}
            {d.onHoldReason ? <><dt>Hold reason</dt><dd>{d.onHoldReason}</dd></> : null}
          </dl>
        </section>

        <section className="section">
          <h3><Icon name="schedule" /> Service targets</h3>
          <dl className="kv">
            <dt>Response due</dt>
            <dd>{dayTime(j.responseDueAt, s.today)} {j.sla.responseMet === true ? <span className="txt-ok">· met</span> : j.sla.responseMet === false ? <span className="txt-crit">· missed</span> : null}</dd>
            <dt>Restoration due</dt>
            <dd>{dayTime(j.resolutionDueAt, s.today)}</dd>
            <dt>Current target</dt>
            <dd><SlaTag state={j.sla.state} compact /> {j.sla.stage.toLowerCase()} · {remaining(j.sla.minutesRemaining)}</dd>
            {j.sla.projectedAt ? <><dt>Projected</dt><dd>{dayTime(j.sla.projectedAt, s.today)}</dd></> : null}
            <dt>Policy</dt>
            <dd>{d.slaPolicyName} · {j.customerTier} line</dd>
          </dl>
        </section>

        <section className="section">
          <h3><Icon name="person" /> Crew &amp; schedule</h3>
          <dl className="kv">
            <dt>Owner (crew)</dt>
            <dd>{j.assignedTechnicianName ? <strong>{j.assignedTechnicianName}</strong> : <span className="txt-warn">Unowned — no crew assigned</span>}</dd>
            <dt>Scheduled</dt>
            <dd>{j.scheduledStart ? `${dayTime(j.scheduledStart, s.today)}–${hm(j.scheduledEnd)}` : 'Not scheduled'}</dd>
            <dt>Access window</dt>
            <dd>{dayTime(j.windowStart, s.today)}–{hm(j.windowEnd)}</dd>
            <dt>Qualification</dt>
            <dd>{j.requiredSkillName} · est. {duration(j.estimatedDurationMinutes)}</dd>
            {d.assignedByDispatcherName ? <><dt>Assigned by</dt><dd>{d.assignedByDispatcherName} at {dayTime(d.assignedAt, s.today)}</dd></> : null}
            {j.rescheduleCount ? <><dt>Reschedules</dt><dd>{j.rescheduleCount}</dd></> : null}
          </dl>
        </section>

        {open ? (
          <section className="section">
            <h3><Icon name="arrow_up" /> Priority</h3>
            <div className="inline-form">
              <IgrSelect
                aria-label="Priority"
                value={newPrio ?? j.priority}
                onChange={e => setNewPrio(e.detail.value as JobPriority)}
              >
                {(['Critical', 'High', 'Medium', 'Low'] as JobPriority[]).map(p => (
                  <IgrSelectItem key={p} value={p} disabled={!s.perms.canSetPriority(j.priority, p)}>
                    {p}{!s.perms.canSetPriority(j.priority, p) ? ' — senior dispatcher or supervisor' : ''}
                  </IgrSelectItem>
                ))}
              </IgrSelect>
              <IgrButton variant="outlined" disabled={!newPrio || newPrio === j.priority || busy} onClick={applyPriority}>Apply</IgrButton>
            </div>
            {newPrio && newPrio !== j.priority ? (
              <p className="small muted">Impact: the job moves in every queue ordered by impact; the change is recorded with your name.</p>
            ) : null}
          </section>
        ) : null}

        <section className="section">
          <h3><Icon name="history" /> Activity</h3>
          <div className="inline-form">
            <IgrTextarea label="Add a note" rows={2} value={note} onInput={e => setNote(e.detail)} />
            <IgrButton variant="outlined" disabled={!note.trim() || busy} onClick={addNote}>Add</IgrButton>
          </div>
          <ol className="timeline">
            {[...notes.map(n => ({ at: n.createdAt, who: n.authorName, text: n.body, kind: 'note' as const })),
              ...events.map(e => ({ at: e.occurredAt, who: e.dispatcherName ?? e.technicianName ?? 'System', text: `${e.eventType}${e.toStatus ? ` → ${STATUS_META[e.toStatus].label}` : ''}${e.details ? ` · ${e.details}` : ''}`, kind: 'event' as const }))]
              .sort((a, b) => b.at.localeCompare(a.at))
              .slice(0, full ? 200 : 6)
              .map((x, i) => (
                <li key={i} className={`tl-${x.kind}`}>
                  <span className="tl-time">{dayTime(x.at, s.today)}</span>
                  <span className="tl-who">{x.who}</span>
                  <span className="tl-text">{x.text}</span>
                </li>
              ))}
          </ol>
        </section>

        {full && d.reschedules.length ? (
          <section className="section">
            <h3><Icon name="event" /> Reschedule history</h3>
            <ol className="timeline">
              {d.reschedules.map(r => (
                <li key={r.id}>
                  <span className="tl-time">{dayTime(r.createdAt, s.today)}</span>
                  <span className="tl-who">{r.dispatcherName ?? 'System'}</span>
                  <span className="tl-text">
                    {r.reasonCode}: {r.fromTechnicianName ?? '—'} {dayTime(r.fromStart, s.today)} → {r.toTechnicianName ?? '—'} {dayTime(r.toStart, s.today)}
                    {r.comment ? ` · ${r.comment}` : ''}
                  </span>
                </li>
              ))}
            </ol>
          </section>
        ) : null}
      </div>

      <AssignDialog open={assignOpen} detail={d} onClose={() => setAssignOpen(false)} onSaved={done} onConflict={c => { setConflict(c); setRetry(null); }} />
      <RescheduleDialog open={rescheduleOpen} job={j} accessNotes={d.siteAccessNotes} onClose={() => setRescheduleOpen(false)} onSaved={done} onConflict={c => { setConflict(c); setRetry(null); }} />
      <ConflictDialog
        conflict={conflict}
        onClose={() => setConflict(null)}
        onReload={() => { setConflict(null); done(); }}
        onReapply={retry ? () => { setConflict(null); done(); retry(); } : undefined}
      />
    </aside>
  );
}
