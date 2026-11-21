import {
  IgrButton, IgrCheckbox, IgrCombo, IgrInput, IgrLinearProgress, IgrList, IgrListHeader, IgrListItem, IgrTextarea,
} from 'igniteui-react';
import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../api/client';
import type { JobListItem } from '../../api/types';
import { Icon } from '../../components/Icon';
import { EmptyState, ErrorState, Freshness, LoadingBlock } from '../../components/States';
import { PriorityTag, SlaTag } from '../../components/Tags';
import { listApprovals, setApprovalStatus, type ApprovalRequest } from '../../lib/approvals';
import { impactCompare, nextAction, STATUS_META } from '../../lib/status';
import { dayTime, remaining } from '../../lib/time';
import { ledgerHrefFor } from '../../state/ledgerState';
import { outcomes } from '../../state/outcomeStore';
import { useSession } from '../../state/session';
import { usePolling } from '../../state/usePolling';

interface HandoverRecord {
  id: string;
  at: string;
  fromId: number;
  fromName: string;
  toId: number;
  toName: string;
  region: string;
  faults: number;
  restrictions: number;
  approvals: number;
  notes: string;
  acknowledgedAt: string | null;
}
const KEY = 'railops.handovers';
const loadRecords = (): HandoverRecord[] => { try { return JSON.parse(localStorage.getItem(KEY) ?? '[]'); } catch { return []; } };
const saveRecords = (r: HandoverRecord[]) => { try { localStorage.setItem(KEY, JSON.stringify(r.slice(0, 50))); } catch { /* ignore */ } };

interface Item { include: boolean; next: string }

/** Journey 5 — transfer unresolved faults, restrictions, pending approvals and next actions to a named owner. */
export default function HandoverPage() {
  const s = useSession();
  const jobs = usePolling(`ho|${s.regionId}|${s.today}`, sig => api.allJobs({ to: s.today, openOnly: true, regionId: s.regionId ?? undefined }, sig), 60_000);
  const timeOff = usePolling(`to|${s.regionId}`, sig => api.timeOff({ status: 'Pending', regionId: s.regionId ?? undefined }, sig), 120_000);
  const [items, setItems] = useState<Record<number, Item>>({});
  const [incomingId, setIncomingId] = useState<number | null>(null);
  const [notes, setNotes] = useState('');
  const [restrictionsRead, setRestrictionsRead] = useState(false);
  const [approvals, setApprovals] = useState<ApprovalRequest[]>(() => listApprovals());
  const [records, setRecords] = useState<HandoverRecord[]>(() => loadRecords());
  const [busy, setBusy] = useState(false);
  const [progress, setProgress] = useState(0);

  const all = useMemo(() => jobs.data ?? [], [jobs.data]);
  const restrictions = useMemo(() => all.filter(j => j.jobCategory === 'Emergency').sort(impactCompare), [all]);
  const faults = useMemo(() => all
    .filter(j => j.jobCategory !== 'Emergency' && (j.sla.state === 'Breached' || j.sla.state === 'AtRisk' || j.status === 'OnHold' || (j.priority === 'Critical')))
    .sort(impactCompare), [all]);

  // Seed each item once with the suggested next action; the dispatcher edits or excludes it.
  useEffect(() => {
    setItems(prev => {
      const next = { ...prev };
      for (const j of [...faults, ...restrictions]) {
        if (!next[j.id]) next[j.id] = { include: true, next: j.jobCategory === 'Emergency' ? `Keep restriction until repair confirmed · ${nextAction(j)}` : nextAction(j) };
      }
      return next;
    });
  }, [faults, restrictions]);

  const pendingLocal = approvals.filter(a => a.status === 'Pending');
  // Time-off decisions that affect the next week of rosters.
  const horizon = (() => { const d = new Date(s.opsNow()); d.setDate(d.getDate() + 7); return d.toISOString().slice(0, 10); })();
  const timeOffSoon = (timeOff.data ?? []).filter(t => (t.startDate ?? '') <= horizon);
  const incomingChoices = s.dispatchers.filter(d => d.id !== s.user!.id && (!s.regionId || d.regionId === s.regionId));
  const incoming = s.dispatchers.find(d => d.id === incomingId) ?? null;
  const included = [...faults, ...restrictions].filter(j => items[j.id]?.include);
  const missingNext = included.filter(j => !items[j.id]?.next.trim());

  const checks = [
    { ok: !!incoming, text: incoming ? `Incoming owner: ${incoming.fullName}` : 'Name the incoming owner' },
    { ok: missingNext.length === 0, text: missingNext.length ? `${missingNext.length} included item(s) have no next action` : `Next action set for all ${included.length} items` },
    { ok: restrictionsRead || restrictions.length === 0, text: restrictions.length ? 'Restrictions reviewed with incoming owner' : 'No restrictions in force' },
    { ok: true, text: `${pendingLocal.length + timeOffSoon.length} pending approvals listed` },
  ];
  const complete = checks.every(c => c.ok);
  const pct = Math.round((checks.filter(c => c.ok).length / checks.length) * 100);

  const setItem = (id: number, patch: Partial<Item>) => setItems(p => ({ ...p, [id]: { ...p[id], ...patch } }));

  const submit = async () => {
    if (!complete || !incoming) return;
    setBusy(true);
    setProgress(0);
    const at = s.opsNowIso();
    const header = `[Handover ${dayTime(at)}] ${s.user!.fullName} → ${incoming.fullName}.`;
    let done = 0;
    const failures: string[] = [];
    // Record a handover note on every transferred job (audit trail), a few at a time.
    for (let i = 0; i < included.length; i += 6) {
      await Promise.all(included.slice(i, i + 6).map(j =>
        api.addNote(j.id, { authorName: s.user!.fullName, body: `${header} Next action: ${items[j.id].next.trim()}` })
          .catch(() => { failures.push(j.jobNumber); })
          .finally(() => { done++; setProgress(Math.round((done / included.length) * 100)); })));
    }
    const rec: HandoverRecord = {
      id: String(Date.now()), at, fromId: s.user!.id, fromName: s.user!.fullName, toId: incoming.id, toName: incoming.fullName,
      region: s.regionName, faults: included.filter(j => j.jobCategory !== 'Emergency').length,
      restrictions: included.filter(j => j.jobCategory === 'Emergency').length,
      approvals: pendingLocal.length + timeOffSoon.length, notes: notes.trim(), acknowledgedAt: null,
    };
    const next = [rec, ...records];
    setRecords(next);
    saveRecords(next);
    setBusy(false);
    if (failures.length) {
      outcomes.push({ tone: 'warn', title: `Handover recorded with ${failures.length} note failure(s)`, detail: `Not noted: ${failures.slice(0, 5).join(', ')}. Retry those jobs from the ledger.` });
    } else {
      outcomes.push({ tone: 'ok', title: `Handover to ${incoming.fullName} recorded`, detail: `${included.length} items transferred · awaiting acknowledgement` });
    }
  };

  const acknowledge = (id: string) => {
    const next = records.map(r => (r.id === id ? { ...r, acknowledgedAt: s.opsNowIso() } : r));
    setRecords(next);
    saveRecords(next);
    outcomes.push({ tone: 'ok', title: 'Handover acknowledged', detail: `Ownership accepted by ${s.user!.fullName}` });
  };

  const decide = (a: ApprovalRequest, status: 'Approved' | 'Declined') => {
    setApprovalStatus(a.id, status);
    setApprovals(listApprovals());
    api.addNote(a.jobId, { authorName: s.user!.fullName, body: `[Approval ${status.toLowerCase()}] ${a.action} — by ${s.user!.fullName} (${s.user!.role}).` })
      .then(() => outcomes.push({ tone: status === 'Approved' ? 'ok' : 'info', title: `Request ${status.toLowerCase()}`, detail: `${a.jobNumber}${status === 'Approved' ? ' · apply the override from the job' : ''}` }))
      .catch(e => outcomes.push({ tone: 'crit', title: 'Decision not recorded', detail: (e as Error).message }));
  };

  const renderJob = (j: JobListItem) => {
    const it = items[j.id] ?? { include: true, next: '' };
    return (
      <IgrListItem key={j.id} className={`ho-item ${it.include ? '' : 'excluded'}`}>
        <IgrCheckbox slot="start" checked={it.include} onChange={e => setItem(j.id, { include: e.detail.checked })} aria-label={`Include ${j.jobNumber} in handover`} />
        <div slot="title" className="ho-title">
          <PriorityTag priority={j.priority} />
          <Link to={ledgerHrefFor(j.id)} className="mono">{j.jobNumber.replace(/^WO-\d+-/, 'WO-')}</Link>
          <strong>{j.summary}</strong>
        </div>
        <div slot="subtitle" className="ho-sub">
          <SlaTag state={j.sla.state} compact /> {remaining(j.sla.minutesRemaining)} · {j.siteName} · {STATUS_META[j.status].label} · {j.assignedTechnicianName ?? 'no owner'}
        </div>
        <IgrInput slot="end" className={`ho-next ${it.include && !it.next.trim() ? 'missing' : ''}`} aria-label={`Next action for ${j.jobNumber}`}
          placeholder="Next action (required)" value={it.next} disabled={!it.include}
          invalid={it.include && !it.next.trim()} onInput={e => setItem(j.id, { next: e.detail })} />
      </IgrListItem>
    );
  };

  return (
    <div className="handover">
      <header className="page-head">
        <div>
          <h1>Shift handover</h1>
          <p className="muted">{s.regionName} · from <strong>{s.user!.fullName}</strong> ({s.user!.shiftName} shift) to a named incoming owner</p>
        </div>
        <Freshness updatedAt={jobs.updatedAt} refreshing={jobs.refreshing} error={jobs.error} onRefresh={jobs.refresh} />
      </header>
      <div className="handover-body">
        <div className="ho-main">
          {jobs.loading ? <LoadingBlock label="Collecting unresolved work…" />
            : jobs.error && !jobs.data ? <ErrorState error={jobs.error} onRetry={jobs.refresh} />
            : (
              <>
                <section className="card">
                  <IgrList className="ho-list">
                    <IgrListHeader><span className="ho-h"><Icon name="shield" /> Restrictions in force · {restrictions.length}</span></IgrListHeader>
                    {restrictions.length ? restrictions.map(renderJob) : <IgrListItem><span slot="title" className="muted">No operating restrictions in force.</span></IgrListItem>}
                  </IgrList>
                  {restrictions.length ? (
                    <div className="ho-ack">
                      <IgrCheckbox checked={restrictionsRead} onChange={e => setRestrictionsRead(e.detail.checked)}>
                        I have reviewed every restriction with the incoming owner
                      </IgrCheckbox>
                    </div>
                  ) : null}
                </section>
                <section className="card">
                  <IgrList className="ho-list">
                    <IgrListHeader><span className="ho-h"><Icon name="warning" /> Unresolved faults · {faults.length}</span><span className="ho-hint">breached, at risk, on hold or critical</span></IgrListHeader>
                    {faults.length ? faults.map(renderJob) : <IgrListItem><span slot="title" className="muted">No unresolved faults.</span></IgrListItem>}
                  </IgrList>
                </section>
                <section className="card">
                  <IgrList className="ho-list">
                    <IgrListHeader><span className="ho-h"><Icon name="lock" /> Pending approvals · {pendingLocal.length + timeOffSoon.length}</span><span className="ho-hint">override requests and time off starting within 7 days</span></IgrListHeader>
                    {pendingLocal.map(a => (
                      <IgrListItem key={a.id}>
                        <Icon slot="start" name="lock" />
                        <span slot="title"><strong>{a.jobNumber}</strong> · {a.action}</span>
                        <span slot="subtitle">{a.reason} · requested by {a.requestedBy} {dayTime(a.requestedAt, s.today)}</span>
                        {s.perms.canOverride ? (
                          <span slot="end" className="ho-actions">
                            <IgrButton variant="outlined" onClick={() => decide(a, 'Declined')}>Decline</IgrButton>
                            <IgrButton onClick={() => decide(a, 'Approved')}>Approve</IgrButton>
                          </span>
                        ) : <span slot="end" className="tag tone-warn tag-compact">Awaiting supervisor</span>}
                      </IgrListItem>
                    ))}
                    {timeOffSoon.map(t => (
                      <IgrListItem key={`to-${t.id}`}>
                        <Icon slot="start" name="event" />
                        <span slot="title"><strong>Time off</strong> · {t.technicianName} · {t.type}</span>
                        <span slot="subtitle">{t.startDate}{t.endDate && t.endDate !== t.startDate ? ` to ${t.endDate}` : ''} · {t.reason ?? 'no reason given'}</span>
                        <span slot="end" className="tag tone-neutral tag-compact">Workforce system</span>
                      </IgrListItem>
                    ))}
                    {!pendingLocal.length && !timeOffSoon.length ? <IgrListItem><span slot="title" className="muted">Nothing awaiting approval.</span></IgrListItem> : null}
                  </IgrList>
                </section>
              </>
            )}
        </div>

        <aside className="ho-side">
          <section className="card">
            <div className="card-head"><h2>Hand over</h2><span className="muted small">{pct}% complete</span></div>
            <div className="card-body ho-form">
              <IgrCombo label="Incoming owner" singleSelect data={incomingChoices.map(d => ({ ...d, label: `${d.fullName} · ${d.shiftName} · ${d.role === 'SeniorDispatcher' ? 'Senior' : d.role}` }))}
                valueKey="id" displayKey="label" placeholder="Choose dispatcher" value={incomingId ? [incomingId] : []}
                onChange={e => setIncomingId((e.detail.newValue[0] as number) ?? null)} required />
              <IgrLinearProgress value={pct} max={100} aria-label="Handover completeness" />
              <ul className="checks">
                {checks.map(c => (
                  <li key={c.text} className={c.ok ? 'txt-ok' : 'txt-warn'}>
                    <Icon name={c.ok ? 'check_circle' : 'warning'} /> <span>{c.text}</span>
                  </li>
                ))}
              </ul>
              <IgrTextarea label="Shift notes for incoming owner" rows={4} value={notes} onInput={e => setNotes(e.detail)} />
              {busy ? <IgrLinearProgress value={progress} max={100} aria-label="Recording handover notes" /> : null}
              <IgrButton disabled={!complete || busy} onClick={submit}>
                <Icon name="handover" /> {incoming ? `Hand over to ${incoming.fullName}` : 'Hand over'}
              </IgrButton>
              <p className="small muted">Writes a handover note with the next action on each of the {included.length} included jobs and asks the incoming owner to acknowledge.</p>
            </div>
          </section>
          <section className="card ho-history">
            <div className="card-head"><h2>Recent handovers</h2></div>
            {records.length ? (
              <ul className="hist">
                {records.slice(0, 8).map(r => (
                  <li key={r.id}>
                    <div><strong>{r.fromName}</strong> → <strong>{r.toName}</strong></div>
                    <div className="muted small">{dayTime(r.at, s.today)} · {r.faults} faults · {r.restrictions} restrictions · {r.approvals} approvals</div>
                    {r.acknowledgedAt ? (
                      <span className="tag tone-ok tag-compact"><Icon name="check_circle" /> Acknowledged {dayTime(r.acknowledgedAt, s.today)}</span>
                    ) : r.toId === s.user!.id ? (
                      <IgrButton variant="outlined" onClick={() => acknowledge(r.id)}>Acknowledge ownership</IgrButton>
                    ) : (
                      <span className="tag tone-warn tag-compact"><Icon name="schedule" /> Awaiting acknowledgement</span>
                    )}
                  </li>
                ))}
              </ul>
            ) : <EmptyState icon="handover" title="No handovers yet this session" />}
          </section>
        </aside>
      </div>
    </div>
  );
}
