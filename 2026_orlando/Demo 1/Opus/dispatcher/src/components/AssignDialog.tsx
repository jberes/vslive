import { IgrButton, IgrCheckbox, IgrDialog, IgrInput, IgrSwitch } from 'igniteui-react';
import { IgrGridLite, IgrGridLiteColumn, type IgrCellContext } from 'igniteui-react/grid-lite';
import { useEffect, useMemo, useRef, useState } from 'react';
import { api } from '../api/client';
import type { AssignmentValidation, JobDetail, Recommendation } from '../api/types';
import { addApproval } from '../lib/approvals';
import { ConflictError, save } from '../lib/conflict';
import { dayTime, hm } from '../lib/time';
import { outcomes } from '../state/outcomeStore';
import { useSession } from '../state/session';
import { Icon } from './Icon';
import { IssueList } from './IssueList';
import { LoadingBlock, EmptyState } from './States';

type RecRow = Recommendation & { rank: number; blocking: number };

/**
 * Journey 1 — assign a maintenance job. Crews are ranked by the API on qualification, proficiency,
 * shift, earliest free slot, travel, workload and response-SLA fit (proximity alone never decides).
 * The dispatcher confirms track access and ownership; blocking issues need a supervisor.
 */
export function AssignDialog({
  open, detail, onClose, onSaved, onConflict,
}: {
  open: boolean;
  detail: JobDetail;
  onClose: () => void;
  onSaved: () => void;
  onConflict: (c: ConflictError) => void;
}) {
  const s = useSession();
  const ref = useRef<IgrDialog>(null);
  const j = detail.job;
  const [recs, setRecs] = useState<RecRow[] | null>(null);
  const [recError, setRecError] = useState<string | null>(null);
  const [showIneligible, setShowIneligible] = useState(false);
  const [crossRegion, setCrossRegion] = useState(false);
  const [pick, setPick] = useState<number | null>(null);
  const [validation, setValidation] = useState<AssignmentValidation | null>(null);
  const [validating, setValidating] = useState(false);
  const [accessOk, setAccessOk] = useState(false);
  const [ownerOk, setOwnerOk] = useState(true);
  const [override, setOverride] = useState(false);
  const [overrideReason, setOverrideReason] = useState('');
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (open) ref.current?.show();
    else ref.current?.hide();
  }, [open]);

  // Load ranked crews whenever the dialog opens or the eligibility options change.
  useEffect(() => {
    if (!open) return;
    setRecs(null);
    setRecError(null);
    api.recommendations(j.id, { limit: 12, includeIneligible: showIneligible, crossRegion })
      .then(r => {
        const rows = r.map((x, i) => ({ ...x, rank: i + 1, blocking: x.issues.filter(z => z.severity === 'Blocking').length }))
          .sort((a, b) => (a.blocking ? 1 : 0) - (b.blocking ? 1 : 0) || a.rank - b.rank); // eligible crews first
        setRecs(rows);
        setPick(p => p ?? rows.find(x => !x.blocking)?.technicianId ?? null);
      })
      .catch(e => setRecError((e as Error).message));
  }, [open, j.id, showIneligible, crossRegion]);

  // Dry-run the assignment for the picked crew so the impact is visible before confirming.
  useEffect(() => {
    if (!open || !pick) { setValidation(null); return; }
    setValidating(true);
    api.validateAssignment(j.id, pick)
      .then(setValidation)
      .catch(() => setValidation(null))
      .finally(() => setValidating(false));
    setOverride(false);
  }, [open, pick, j.id]);

  useEffect(() => {
    if (!open) { setPick(null); setAccessOk(false); setOwnerOk(true); setNote(''); setOverrideReason(''); setOverride(false); }
  }, [open]);

  const picked = recs?.find(r => r.technicianId === pick) ?? null;
  const blocking = validation?.issues.filter(i => i.severity === 'Blocking') ?? [];
  const needsAccess = !!detail.siteAccessNotes;
  const canConfirm = !!picked && !!validation && ownerOk && (!needsAccess || accessOk) &&
    (blocking.length === 0 || (override && s.perms.canOverride && overrideReason.trim().length > 4));

  const templates = useMemo(() => ({
    pick: (ctx: IgrCellContext<RecRow>) => {
      const r = ctx.row.data!;
      return (
        <label className="pick">
          <input
            type="radio"
            name="crew-pick"
            checked={r.technicianId === pick}
            onChange={() => setPick(r.technicianId)}
            aria-label={`Select ${r.technicianName}`}
          />
          <span className="cell2">
            <span className="l1 strong">{r.technicianName}</span>
            <span className="l2 muted">{r.level} · {r.depotName}</span>
          </span>
        </label>
      );
    },
    qual: (ctx: IgrCellContext<RecRow>) => {
      const r = ctx.row.data!;
      return (
        <div className="cell2">
          <span className="l1">Proficiency {r.proficiency}/5</span>
          <span className={`l2 ${r.blocking ? 'txt-crit' : r.issues.length ? 'txt-warn' : 'txt-ok'}`}>
            {r.blocking ? `${r.blocking} blocking issue${r.blocking > 1 ? 's' : ''}` : r.issues.length ? `${r.issues.length} warning${r.issues.length > 1 ? 's' : ''}` : 'Qualified, no issues'}
          </span>
        </div>
      );
    },
    avail: (ctx: IgrCellContext<RecRow>) => {
      const r = ctx.row.data!;
      return (
        <div className="cell2">
          <span className="l1">Earliest {r.earliestStart ? dayTime(r.earliestStart, s.today) : '—'}</span>
          <span className="l2 muted">{r.fieldStatus} · shift {hm(r.shiftStart)}–{hm(r.shiftEnd)}</span>
        </div>
      );
    },
    travel: (ctx: IgrCellContext<RecRow>) => (
      <div className="cell2">
        <span className="l1">{ctx.row.data!.travelMinutes} min</span>
        <span className="l2 muted">{ctx.row.data!.distanceKm.toFixed(1)} km</span>
      </div>
    ),
    load: (ctx: IgrCellContext<RecRow>) => (
      <div className="cell2">
        <span className="l1">{ctx.row.data!.jobsToday}/{ctx.row.data!.maxJobsPerDay} jobs</span>
        <span className="l2 muted">{Math.round(ctx.row.data!.utilizationPct)}% booked</span>
      </div>
    ),
    sla: (ctx: IgrCellContext<RecRow>) => (
      ctx.row.data!.meetsResponseSla
        ? <span className="tag tone-ok tag-compact"><Icon name="check_circle" /> Meets response</span>
        : <span className="tag tone-warn tag-compact"><Icon name="warning" /> Misses response</span>
    ),
  }), [pick, s.today]);

  const confirm = async () => {
    if (!picked || !validation) return;
    setBusy(true);
    const forced = blocking.length > 0;
    const r = await save({
      seen: j,
      label: 'Assignment',
      action: () => api.assign(j.id, {
        technicianId: picked.technicianId,
        dispatcherId: s.user!.id,
        force: forced,
        note: [
          note.trim(),
          needsAccess ? `Access confirmed: ${detail.siteAccessNotes}` : '',
          forced ? `SUPERVISOR OVERRIDE by ${s.user!.fullName}: ${overrideReason.trim()} (issues: ${blocking.map(b => b.code).join(', ')})` : '',
        ].filter(Boolean).join(' | ') || undefined,
      }),
      success: res => ({
        title: `${j.jobNumber} assigned to ${picked.technicianName}`,
        detail: `Starts ${dayTime(res.job.job.scheduledStart, s.today)} · owner recorded as ${s.user!.fullName}${forced ? ' · override recorded' : ''}`,
      }),
    });
    setBusy(false);
    if (r.kind === 'saved') { onSaved(); onClose(); }
    if (r.kind === 'conflict') { onClose(); onConflict(r.error); }
  };

  const requestApproval = async () => {
    if (!picked) return;
    setBusy(true);
    const reason = `Blocking: ${blocking.map(b => b.message).join(' ')}`;
    try {
      await api.addNote(j.id, {
        authorName: s.user!.fullName,
        body: `[Approval requested] Assign ${picked.technicianName} despite blocking issues. ${reason} Requested by ${s.user!.fullName}.`,
      });
      addApproval({
        jobId: j.id, jobNumber: j.jobNumber, summary: j.summary, action: `Assign ${picked.technicianName}`, reason,
        requestedBy: s.user!.fullName, requestedAt: s.opsNowIso(),
      });
      outcomes.push({ tone: 'info', title: 'Supervisor approval requested', detail: `${j.jobNumber} · listed under pending approvals in handover` });
      onClose();
    } catch (e) {
      outcomes.push({ tone: 'crit', title: 'Approval request failed — nothing recorded', detail: (e as Error).message });
    } finally {
      setBusy(false);
    }
  };

  return (
    <IgrDialog ref={ref} className="dlg-wide" title={`Assign crew · ${j.jobNumber}`} onClosed={onClose}>
      <div className="dialog-body assign">
        <div className="assign-job">
          <strong>{j.summary}</strong>
          <span className="muted">{j.siteName} · {j.customerName} · needs <strong>{j.requiredSkillName}</strong> · response due {dayTime(j.responseDueAt, s.today)}</span>
        </div>
        <div className="assign-opts">
          <IgrSwitch checked={showIneligible} onChange={e => setShowIneligible(e.detail.checked)}>Show ineligible crews</IgrSwitch>
          <IgrSwitch checked={crossRegion} onChange={e => setCrossRegion(e.detail.checked)}>Include other regions</IgrSwitch>
          <span className="muted small">Ranked on qualification, availability, shift, travel, workload and SLA fit.</span>
        </div>
        <div className="rec-grid">
          {recError ? <p className="callout tone-crit"><Icon name="octagon" /> {recError}</p>
            : !recs ? <LoadingBlock label="Ranking qualified crews…" rows={4} />
            : !recs.length ? <EmptyState icon="warning" title="No qualified crew available">Try including other regions or ineligible crews (supervisor override).</EmptyState>
            : (
              <IgrGridLite data={recs} adoptRootStyles>
                <IgrGridLiteColumn field="technicianName" header="Crew" width="270px" cellTemplate={templates.pick} />
                <IgrGridLiteColumn field="proficiency" header="Qualification" dataType="number" sortable cellTemplate={templates.qual} />
                <IgrGridLiteColumn field="earliestStart" header="Availability" sortable cellTemplate={templates.avail} />
                <IgrGridLiteColumn field="travelMinutes" header="Travel" dataType="number" sortable width="110px" cellTemplate={templates.travel} />
                <IgrGridLiteColumn field="jobsToday" header="Workload" dataType="number" sortable width="130px" cellTemplate={templates.load} />
                <IgrGridLiteColumn field="meetsResponseSla" header="Response SLA" width="170px" cellTemplate={templates.sla} />
              </IgrGridLite>
            )}
        </div>

        <div className="impact">
          <h3><Icon name="info" /> Impact preview {picked ? `· ${picked.technicianName}` : ''}</h3>
          {!picked ? <p className="muted">Select a crew to preview the assignment.</p>
            : validating || !validation ? <p className="muted">Checking qualifications, shift, overlaps and SLA…</p>
            : (
              <>
                <p>
                  Proposed <strong>{dayTime(validation.proposedStart, s.today)}–{hm(validation.proposedEnd)}</strong>
                  {' '}· travel {picked.travelMinutes} min · {picked.meetsResponseSla ? 'meets' : <strong className="txt-warn">misses</strong>} the response target
                </p>
                <IssueList issues={validation.issues} />
              </>
            )}
          <div className="confirm-checks">
            {needsAccess ? (
              <IgrCheckbox checked={accessOk} onChange={e => setAccessOk(e.detail.checked)}>
                Track access arranged: <strong>{detail.siteAccessNotes}</strong>
              </IgrCheckbox>
            ) : null}
            <IgrCheckbox checked={ownerOk} onChange={e => setOwnerOk(e.detail.checked)}>
              I take dispatch ownership ({s.user!.fullName})
            </IgrCheckbox>
            {blocking.length && s.perms.canOverride ? (
              <>
                <IgrCheckbox checked={override} onChange={e => setOverride(e.detail.checked)}>Override blocking issues (supervisor)</IgrCheckbox>
                {override ? <IgrInput label="Override reason (recorded)" value={overrideReason} onInput={e => setOverrideReason(e.detail)} /> : null}
              </>
            ) : null}
            <IgrInput label="Note to crew / log (optional)" value={note} onInput={e => setNote(e.detail)} />
          </div>
        </div>
      </div>
      <IgrButton slot="footer" variant="flat" onClick={onClose}>Cancel</IgrButton>
      {blocking.length && !s.perms.canOverride ? (
        <IgrButton slot="footer" variant="outlined" disabled={busy} onClick={requestApproval}>
          <Icon name="lock" /> Request supervisor approval
        </IgrButton>
      ) : null}
      <IgrButton slot="footer" disabled={!canConfirm || busy} onClick={confirm}>
        {picked ? `Assign ${picked.technicianName}` : 'Assign'}
      </IgrButton>
    </IgrDialog>
  );
}
