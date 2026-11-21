import {
  IgrButton, IgrCheckbox, IgrDatePicker, IgrDateTimeInput, IgrDialog, IgrInput, IgrSelect, IgrSelectItem, IgrSwitch,
} from 'igniteui-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { api } from '../api/client';
import type { AssignmentValidation, JobListItem, Recommendation, RescheduleReason } from '../api/types';
import { addApproval } from '../lib/approvals';
import { ConflictError, save } from '../lib/conflict';
import { STATUS_META } from '../lib/status';
import { dayTime, hm, parse, toApi, toDateKey } from '../lib/time';
import { outcomes } from '../state/outcomeStore';
import { useSession } from '../state/session';
import { Icon } from './Icon';
import { IssueList } from './IssueList';

const REASONS: { id: RescheduleReason; label: string }[] = [
  { id: 'TechnicianUnavailable', label: 'Crew unavailable' },
  { id: 'Overrun', label: 'Previous job overran' },
  { id: 'PartsDelay', label: 'Parts / equipment delay' },
  { id: 'Weather', label: 'Weather' },
  { id: 'SlaRebalance', label: 'SLA rebalance' },
  { id: 'CustomerRequest', label: 'Line owner request (re-baselines SLA)' },
  { id: 'Other', label: 'Other' },
];

/**
 * Journey 2 — reschedule a crew's job. Shows the affected commitments for the receiving crew, the access
 * window, validation and SLA consequences BEFORE the change is confirmed.
 */
export function RescheduleDialog({
  open, job, accessNotes, onClose, onSaved, onConflict,
}: {
  open: boolean;
  job: JobListItem;
  accessNotes?: string | null;
  onClose: () => void;
  onSaved: () => void;
  onConflict: (c: ConflictError) => void;
}) {
  const s = useSession();
  const ref = useRef<IgrDialog>(null);
  const initial = parse(job.scheduledStart) ?? s.opsNow();
  const [crewId, setCrewId] = useState<number | null>(job.assignedTechnicianId);
  const [date, setDate] = useState<Date>(initial);
  const [time, setTime] = useState<Date>(initial);
  const [reason, setReason] = useState<RescheduleReason>('TechnicianUnavailable');
  const [comment, setComment] = useState('');
  const [updateWindow, setUpdateWindow] = useState(false);
  const [crews, setCrews] = useState<Recommendation[]>([]);
  const [validation, setValidation] = useState<AssignmentValidation | null>(null);
  const [commitments, setCommitments] = useState<JobListItem[] | null>(null);
  const [override, setOverride] = useState(false);
  const [overrideReason, setOverrideReason] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (open) {
      const st = parse(job.scheduledStart) ?? s.opsNow();
      setCrewId(job.assignedTechnicianId); setDate(st); setTime(st); setReason('TechnicianUnavailable');
      setComment(''); setUpdateWindow(false); setOverride(false); setOverrideReason('');
      ref.current?.show();
      api.recommendations(job.id, { limit: 12, includeIneligible: true }).then(setCrews).catch(() => setCrews([]));
    } else {
      ref.current?.hide();
    }
  }, [open]); // eslint-disable-line react-hooks/exhaustive-deps

  const newStart = useMemo(() => {
    const d = new Date(date);
    d.setHours(time.getHours(), time.getMinutes(), 0, 0);
    return d;
  }, [date, time]);
  const newEnd = new Date(newStart.getTime() + job.estimatedDurationMinutes * 60_000);
  const startIso = toApi(newStart);

  // Dry run + the receiving crew's commitments that day, re-evaluated whenever crew or time changes.
  useEffect(() => {
    if (!open || !crewId) return;
    let alive = true;
    setValidation(null);
    setCommitments(null);
    const t = window.setTimeout(() => {
      api.validateAssignment(job.id, crewId, startIso).then(v => alive && setValidation(v)).catch(() => undefined);
      const day = toDateKey(newStart);
      api.techSchedule(crewId, day, day).then(r => alive && setCommitments(r.jobs.filter(x => x.id !== job.id))).catch(() => alive && setCommitments([]));
    }, 250);
    return () => { alive = false; window.clearTimeout(t); };
  }, [open, crewId, startIso, job.id]); // eslint-disable-line react-hooks/exhaustive-deps

  const blocking = validation?.issues.filter(i => i.severity === 'Blocking') ?? [];
  const crewName = crewId === job.assignedTechnicianId ? job.assignedTechnicianName : crews.find(c => c.technicianId === crewId)?.technicianName;
  const startMs = newStart.getTime();
  const endMs = newEnd.getTime();
  const overlaps = (x: JobListItem) => {
    const a = parse(x.scheduledStart)?.getTime() ?? 0;
    const b = parse(x.scheduledEnd)?.getTime() ?? 0;
    return a < endMs && b > startMs;
  };

  // SLA and access-window consequences, kept separate (service target vs access constraint).
  const slaEffects: { tone: 'crit' | 'warn' | 'ok' | 'info'; text: string }[] = [];
  if (reason === 'CustomerRequest') {
    slaEffects.push({ tone: 'info', text: 'Line owner request: response and restoration targets are re-baselined to the new time.' });
  } else {
    if (job.sla.responseMet === null && job.responseDueAt && startIso > job.responseDueAt) {
      slaEffects.push({ tone: 'crit', text: `Response target ${dayTime(job.responseDueAt, s.today)} will be missed (original commitment kept).` });
    }
    if (toApi(newEnd) > job.resolutionDueAt) {
      slaEffects.push({ tone: 'crit', text: `Restoration target ${dayTime(job.resolutionDueAt, s.today)} will be missed.` });
    }
    if (!slaEffects.length) slaEffects.push({ tone: 'ok', text: 'Response and restoration targets remain achievable.' });
  }
  const outsideWindow = !updateWindow && (startIso < job.windowStart || startIso > job.windowEnd);

  const canConfirm = !!crewId && !!validation && (blocking.length === 0 || (override && s.perms.canOverride && overrideReason.trim().length > 4));

  const confirm = async () => {
    if (!crewId) return;
    setBusy(true);
    const forced = blocking.length > 0;
    const r = await save({
      seen: job,
      label: 'Reschedule',
      action: () => api.reschedule(job.id, {
        newScheduledStart: startIso,
        technicianId: crewId !== job.assignedTechnicianId ? crewId : undefined,
        reasonCode: reason,
        comment: [comment.trim(), forced ? `SUPERVISOR OVERRIDE by ${s.user!.fullName}: ${overrideReason.trim()}` : ''].filter(Boolean).join(' | ') || undefined,
        dispatcherId: s.user!.id,
        updateWindow,
        force: forced,
      }),
      success: () => ({
        title: `${job.jobNumber} moved to ${dayTime(startIso, s.today)}`,
        detail: `${crewName ?? 'Crew'} · reason ${REASONS.find(x => x.id === reason)?.label} · recorded as ${s.user!.fullName}`,
      }),
    });
    setBusy(false);
    if (r.kind === 'saved') { onSaved(); onClose(); }
    if (r.kind === 'conflict') { onClose(); onConflict(r.error); }
  };

  const requestApproval = async () => {
    setBusy(true);
    const why = blocking.map(b => b.message).join(' ');
    try {
      await api.addNote(job.id, { authorName: s.user!.fullName, body: `[Approval requested] Reschedule to ${dayTime(startIso)} with ${crewName}. Blocking: ${why}` });
      addApproval({ jobId: job.id, jobNumber: job.jobNumber, summary: job.summary, action: `Reschedule to ${dayTime(startIso)} (${crewName})`, reason: why, requestedBy: s.user!.fullName, requestedAt: s.opsNowIso() });
      outcomes.push({ tone: 'info', title: 'Supervisor approval requested', detail: `${job.jobNumber} · listed under pending approvals in handover` });
      onClose();
    } catch (e) {
      outcomes.push({ tone: 'crit', title: 'Approval request failed — nothing recorded', detail: (e as Error).message });
    } finally {
      setBusy(false);
    }
  };

  return (
    <IgrDialog ref={ref} className="dlg-wide" title={`Reschedule · ${job.jobNumber}`} onClosed={onClose}>
      <div className="dialog-body resched">
        <div className="resched-form">
          <p className="muted">
            <strong>{job.summary}</strong><br />
            {job.siteName} · now {job.scheduledStart ? `${dayTime(job.scheduledStart, s.today)}–${hm(job.scheduledEnd)}` : 'unscheduled'} with {job.assignedTechnicianName ?? 'no crew'}
          </p>
          <IgrSelect label="Crew" value={crewId ? String(crewId) : ''} onChange={e => setCrewId(Number(e.detail.value))}>
            {job.assignedTechnicianId ? (
              <IgrSelectItem value={String(job.assignedTechnicianId)}>{job.assignedTechnicianName} (current)</IgrSelectItem>
            ) : null}
            {crews.filter(c => c.technicianId !== job.assignedTechnicianId).map(c => (
              <IgrSelectItem key={c.technicianId} value={String(c.technicianId)}>
                {c.technicianName} · {c.depotName}{c.issues.some(i => i.severity === 'Blocking') ? ' — not eligible' : ''}
              </IgrSelectItem>
            ))}
          </IgrSelect>
          <div className="row2">
            <IgrDatePicker label="Date" value={date} onChange={e => e.detail && setDate(e.detail)} />
            <IgrDateTimeInput label="Start (24 h)" inputFormat="HH:mm" displayFormat="HH:mm" value={time} onChange={e => e.detail && setTime(e.detail)} />
          </div>
          <IgrSelect label="Reason" value={reason} onChange={e => { const r = e.detail.value as RescheduleReason; setReason(r); setUpdateWindow(r === 'CustomerRequest'); }}>
            {REASONS.map(r => <IgrSelectItem key={r.id} value={r.id}>{r.label}</IgrSelectItem>)}
          </IgrSelect>
          <IgrSwitch checked={updateWindow} onChange={e => setUpdateWindow(e.detail.checked)}>Move the agreed access window with the job</IgrSwitch>
          <IgrInput label="Comment (recorded)" value={comment} onInput={e => setComment(e.detail)} />
          {blocking.length && s.perms.canOverride ? (
            <>
              <IgrCheckbox checked={override} onChange={e => setOverride(e.detail.checked)}>Override blocking issues (supervisor)</IgrCheckbox>
              {override ? <IgrInput label="Override reason (recorded)" value={overrideReason} onInput={e => setOverrideReason(e.detail)} /> : null}
            </>
          ) : null}
        </div>

        <div className="impact">
          <h3><Icon name="info" /> Impact preview</h3>
          <p>
            New slot <strong>{dayTime(startIso, s.today)}–{hm(newEnd)}</strong> with <strong>{crewName ?? '—'}</strong>
          </p>
          <h4>Validation</h4>
          {validation ? <IssueList issues={validation.issues} /> : <p className="muted">Checking shift, overlaps and qualifications…</p>}

          <h4>Service targets</h4>
          <ul className="issues">
            {slaEffects.map(e => (
              <li key={e.text} className={`issue tone-${e.tone}`}>
                <Icon name={e.tone === 'crit' ? 'octagon' : e.tone === 'ok' ? 'check_circle' : 'info'} /> <span>{e.text}</span>
              </li>
            ))}
          </ul>

          <h4>Access window</h4>
          <ul className="issues">
            <li className={`issue tone-${outsideWindow ? 'warn' : 'ok'}`}>
              <Icon name={outsideWindow ? 'warning' : 'check_circle'} />
              <span>
                {updateWindow ? 'Window moves with the job.' : `Agreed window ${dayTime(job.windowStart, s.today)}–${hm(job.windowEnd)}${outsideWindow ? ' — new start is outside it; access must be re-arranged.' : ' — new start fits.'}`}
                {accessNotes ? <> Access: <strong>{accessNotes}</strong></> : null}
              </span>
            </li>
          </ul>

          <h4>Affected commitments · {crewName ?? 'crew'} on {toDateKey(newStart)}</h4>
          {commitments === null ? <p className="muted">Loading crew schedule…</p>
            : !commitments.length ? <p className="muted">No other jobs booked for this crew that day.</p>
            : (
              <ul className="commitments">
                {commitments
                  .filter(c => STATUS_META[c.status].open)
                  .sort((a, b) => (a.scheduledStart ?? '').localeCompare(b.scheduledStart ?? ''))
                  .map(c => {
                    const clash = overlaps(c);
                    const after = (parse(c.scheduledStart)?.getTime() ?? 0) >= endMs;
                    return (
                      <li key={c.id} className={clash ? 'clash' : ''}>
                        <span className="mono">{hm(c.scheduledStart)}–{hm(c.scheduledEnd)}</span>
                        <span>{c.summary} · {c.siteName}</span>
                        {clash ? <span className="tag tone-crit tag-compact"><Icon name="octagon" /> Overlaps</span>
                          : after ? <span className="tag tone-neutral tag-compact">Downstream</span> : null}
                      </li>
                    );
                  })}
              </ul>
            )}
        </div>
      </div>
      <IgrButton slot="footer" variant="flat" onClick={onClose}>Cancel</IgrButton>
      {blocking.length && !s.perms.canOverride ? (
        <IgrButton slot="footer" variant="outlined" disabled={busy} onClick={requestApproval}><Icon name="lock" /> Request supervisor approval</IgrButton>
      ) : null}
      <IgrButton slot="footer" disabled={!canConfirm || busy} onClick={confirm}>Confirm reschedule</IgrButton>
    </IgrDialog>
  );
}
