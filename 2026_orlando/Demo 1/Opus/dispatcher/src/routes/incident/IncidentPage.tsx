import {
  IgrButton, IgrCheckbox, IgrCombo, IgrInput, IgrRadio, IgrRadioGroup, IgrSelect, IgrSelectItem, IgrTextarea,
} from 'igniteui-react';
import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../../api/client';
import type { Asset, Customer, JobDetail, JobPriority, Site } from '../../api/types';
import { AssignDialog } from '../../components/AssignDialog';
import { Icon } from '../../components/Icon';
import { PriorityTag, SlaTag, StatusText } from '../../components/Tags';
import { dayTime, toApi } from '../../lib/time';
import { ledgerHrefFor } from '../../state/ledgerState';
import { outcomes } from '../../state/outcomeStore';
import { useSession } from '../../state/session';
import { save } from '../../lib/conflict';

const IMPACTS = [
  { id: 'blocked', label: 'Line blocked — no train movements', severity: 3 },
  { id: 'restricted', label: 'Speed or signal restriction — trains delayed', severity: 2 },
  { id: 'station', label: 'Station facility degraded — passengers affected', severity: 1 },
  { id: 'none', label: 'No immediate service impact', severity: 0 },
] as const;
type ImpactId = (typeof IMPACTS)[number]['id'];

/** Journey 4 — coordinate an urgent infrastructure fault. */
export default function IncidentPage() {
  const s = useSession();
  const [lines, setLines] = useState<Customer[]>([]);
  const [sites, setSites] = useState<Site[]>([]);
  const [assets, setAssets] = useState<(Asset & { assetType: string })[]>([]);
  const [lineId, setLineId] = useState<number | null>(null);
  const [siteId, setSiteId] = useState<number | null>(null);
  const [assetId, setAssetId] = useState<number | null>(null);
  const [jobTypeId, setJobTypeId] = useState<number | null>(null);
  const [priority, setPriority] = useState<JobPriority>('Critical');
  const [impact, setImpact] = useState<ImpactId>('restricted');
  const [restriction, setRestriction] = useState(true);
  const [restrictionText, setRestrictionText] = useState('');
  const [source, setSource] = useState('Phone');
  const [reporter, setReporter] = useState('');
  const [description, setDescription] = useState('');
  const [touched, setTouched] = useState(false);
  const [busy, setBusy] = useState(false);
  const [created, setCreated] = useState<JobDetail | null>(null);
  const [assignOpen, setAssignOpen] = useState(false);

  useEffect(() => {
    api.customers(s.regionId ?? undefined).then(r => setLines(r.items)).catch(() => setLines([]));
    setLineId(null);
  }, [s.regionId]);
  useEffect(() => {
    setSites([]); setSiteId(null);
    if (lineId) api.customerSites(lineId).then(setSites).catch(() => setSites([]));
  }, [lineId]);
  useEffect(() => {
    setAssets([]); setAssetId(null);
    if (siteId) api.siteAssets(siteId).then(setAssets).catch(() => setAssets([]));
  }, [siteId]);

  const jobTypes = useMemo(() => [...s.lookups!.jobTypes].sort((a, b) =>
    (a.category === 'Emergency' ? 0 : 1) - (b.category === 'Emergency' ? 0 : 1) || a.name.localeCompare(b.name)), [s.lookups]);
  const jobType = jobTypes.find(t => t.id === jobTypeId);
  const site = sites.find(x => x.id === siteId);
  const line = lines.find(l => l.id === lineId);
  const supervisors = s.dispatchers.filter(d => d.role === 'Supervisor' && (!s.regionId || d.regionId === s.regionId));
  const seniors = s.dispatchers.filter(d => d.role === 'SeniorDispatcher' && (!s.regionId || d.regionId === s.regionId));
  const impactMeta = IMPACTS.find(i => i.id === impact)!;

  // Escalation path is derived from severity + service impact + safety restriction.
  const escalation = useMemo(() => {
    const steps: { who: string; what: string; required: boolean }[] = [];
    if (priority === 'Critical' || impactMeta.severity >= 2) {
      steps.push({ who: `Shift supervisor${supervisors[0] ? ` · ${supervisors[0].fullName}` : ''}`, what: 'Notify now; owns escalation and any override', required: true });
    }
    if (impactMeta.severity >= 2) {
      steps.push({ who: 'Operations control (train dispatch)', what: impact === 'blocked' ? 'Block the affected track and confirm protection before crews go on track' : 'Apply and broadcast the speed / signal restriction', required: true });
    }
    if (restriction) {
      steps.push({ who: 'Roadway worker protection', what: 'Restriction recorded on the job; crew must confirm track access before starting', required: true });
    }
    if (priority === 'High' && impactMeta.severity < 2) {
      steps.push({ who: `Senior dispatcher${seniors[0] ? ` · ${seniors[0].fullName}` : ''}`, what: 'Review within 15 minutes', required: false });
    }
    if (impact === 'station') steps.push({ who: 'Station operations', what: 'Passenger information and accessibility alternatives', required: false });
    if (!steps.length) steps.push({ who: 'Dispatcher (you)', what: 'Standard queue; assign within normal SLA', required: false });
    return steps;
  }, [priority, impact, impactMeta.severity, restriction, supervisors, seniors]);

  const errors = {
    line: !lineId ? 'Choose the line' : null,
    site: !siteId ? 'Choose the station or track location' : null,
    type: !jobTypeId ? 'Choose the fault type' : null,
    restriction: restriction && !restrictionText.trim() ? 'Describe the restriction in force' : null,
  };
  const valid = !Object.values(errors).some(Boolean);

  const submit = async () => {
    setTouched(true);
    if (!valid || !jobType || !site) return;
    setBusy(true);
    const now = s.opsNow();
    const windowEnd = new Date(now.getTime() + (priority === 'Critical' ? 60 : 120) * 60_000);
    const summary = `${jobType.name}${impact === 'blocked' ? ' — line blocked' : ''}`;
    const desc = [
      priority === 'Critical' ? 'EMERGENCY' : 'URGENT',
      restriction ? `safety restriction in place: ${restrictionText.trim()}.` : '',
      `Service impact: ${impactMeta.label}.`,
      description.trim(),
    ].filter(Boolean).join(' ');
    const r = await save({
      label: 'Urgent fault',
      action: async () => {
        const job = await api.createJob({
          siteId: site.id, assetId: assetId ?? undefined, jobTypeId: jobType.id, priority, source, summary, description: desc,
          windowStart: toApi(now), windowEnd: toApi(windowEnd), estimatedDurationMinutes: jobType.defaultDurationMinutes, dispatcherId: s.user!.id,
        });
        // Escalation record: who must be told and what they must do, attached to the job's audit trail.
        await api.addNote(job.job.id, {
          authorName: s.user!.fullName,
          body: `[Escalation] Raised by ${s.user!.fullName} at ${dayTime(toApi(now))} via ${source}${reporter ? ` (reported by ${reporter})` : ''}. ` +
            escalation.map(e => `${e.who}: ${e.what}`).join(' | '),
        });
        return api.job(job.job.id);
      },
      success: d => ({ title: `${d.job.jobNumber} recorded and escalated`, detail: `${escalation.filter(e => e.required).length} required notifications logged · next: dispatch responders` }),
    });
    setBusy(false);
    if (r.kind === 'saved') setCreated(r.value);
    if (r.kind === 'failed' && !(r.message.includes('Network'))) {
      outcomes.push({ tone: 'info', title: 'Your entries are kept', detail: 'Fix the problem and submit again.' });
    }
  };

  const markDispatched = async () => {
    if (!created) return;
    const r = await save({
      seen: created.job, label: 'Dispatch',
      action: () => api.setStatus(created.job.id, { status: 'Dispatched', dispatcherId: s.user!.id, note: 'Dispatched from urgent fault console' }),
      success: () => ({ title: `${created.job.jobNumber} dispatched`, detail: `${created.job.assignedTechnicianName} notified` }),
    });
    if (r.kind === 'saved') setCreated(await api.job(created.job.id));
  };

  const show = (e: string | null) => (touched && e ? e : undefined);

  if (created) {
    const j = created.job;
    return (
      <div className="incident">
        <header className="page-head"><div><h1>Urgent fault recorded</h1><p className="muted">Escalation logged on the job. Dispatch qualified responders next.</p></div></header>
        <div className="incident-done card">
          <div className="card-body done-grid">
            <div className="done-main">
              <div className="panel-tags"><span className="mono">{j.jobNumber}</span><PriorityTag priority={j.priority} /><SlaTag state={j.sla.state} /><StatusText status={j.status} /></div>
              <h2>{j.summary}</h2>
              <p>{j.siteName} · {j.customerName} · response due <strong>{dayTime(j.responseDueAt, s.today)}</strong></p>
              {created.siteAccessNotes ? <p className="callout tone-warn"><Icon name="lock" /><span>Track access: <strong>{created.siteAccessNotes}</strong></span></p> : null}
              <div className="done-actions">
                {j.status === 'Unassigned' ? (
                  <IgrButton onClick={() => setAssignOpen(true)}><Icon name="person_add" /> Choose qualified responders</IgrButton>
                ) : j.status === 'Assigned' ? (
                  <IgrButton onClick={markDispatched}><Icon name="send" /> Dispatch {j.assignedTechnicianName}</IgrButton>
                ) : (
                  <span className="tag tone-ok"><Icon name="check_circle" /> {j.assignedTechnicianName} {j.status === 'Dispatched' ? 'dispatched' : j.status}</span>
                )}
                <Link className="link-btn" to={ledgerHrefFor(j.id)}><Icon name="list" /> Track in ledger</Link>
                <IgrButton variant="flat" onClick={() => { setCreated(null); setTouched(false); setDescription(''); setRestrictionText(''); }}>Record another fault</IgrButton>
              </div>
            </div>
            <div className="impact">
              <h3><Icon name="arrow_up" /> Escalation record</h3>
              <ol className="esc-list">
                {escalation.map(e => <li key={e.who}><strong>{e.who}</strong><span>{e.what}</span>{e.required ? <span className="tag tone-crit tag-compact">Required</span> : null}</li>)}
              </ol>
            </div>
          </div>
        </div>
        <AssignDialog open={assignOpen} detail={created} onClose={() => setAssignOpen(false)}
          onSaved={() => api.job(j.id).then(setCreated)} onConflict={() => api.job(j.id).then(setCreated)} />
      </div>
    );
  }

  return (
    <div className="incident">
      <header className="page-head">
        <div>
          <h1><Icon name="bolt" /> Report urgent fault</h1>
          <p className="muted">{s.regionName} · record location and service impact, escalate, then dispatch qualified responders</p>
        </div>
      </header>
      <div className="incident-body">
        <form className="card incident-form" onSubmit={e => { e.preventDefault(); submit(); }} noValidate>
          <div className="card-head"><h2>1 · Location</h2></div>
          <div className="card-body form-grid">
            <IgrCombo label="Line / subdivision" singleSelect data={lines} valueKey="id" displayKey="name" placeholder="Choose line"
              value={lineId ? [lineId] : []} onChange={e => setLineId((e.detail.newValue[0] as number) ?? null)}
              invalid={!!show(errors.line)} required>
              {show(errors.line) ? <span slot="helper-text">{errors.line}</span> : null}
            </IgrCombo>
            <IgrCombo label="Station / track / location" singleSelect data={sites} valueKey="id" displayKey="name" placeholder={lineId ? 'Choose location' : 'Choose a line first'}
              disabled={!lineId} value={siteId ? [siteId] : []} onChange={e => setSiteId((e.detail.newValue[0] as number) ?? null)}
              invalid={!!show(errors.site)} required>
              {show(errors.site) ? <span slot="helper-text">{errors.site}</span> : null}
            </IgrCombo>
            <IgrSelect label="Asset (optional)" disabled={!assets.length} value={assetId ? String(assetId) : ''} onChange={e => setAssetId(Number(e.detail.value) || null)}>
              {assets.map(a => <IgrSelectItem key={a.id} value={String(a.id)}>{a.assetTag} · {a.assetType}</IgrSelectItem>)}
            </IgrSelect>
            {site?.accessNotes ? <p className="callout tone-warn span2"><Icon name="lock" /><span>Access at this location: <strong>{site.accessNotes}</strong></span></p> : null}
          </div>

          <div className="card-head"><h2>2 · Fault and service impact</h2></div>
          <div className="card-body form-grid">
            <IgrCombo label="Fault type" singleSelect data={jobTypes} valueKey="id" displayKey="name" groupKey="category" placeholder="Choose fault type"
              value={jobTypeId ? [jobTypeId] : []} onChange={e => setJobTypeId((e.detail.newValue[0] as number) ?? null)} invalid={!!show(errors.type)} required>
              {show(errors.type) ? <span slot="helper-text">{errors.type}</span> : jobType ? <span slot="helper-text">Needs {jobType.requiredSkillName} · est. {jobType.defaultDurationMinutes} min</span> : null}
            </IgrCombo>
            <IgrSelect label="Reported via" value={source} onChange={e => setSource(e.detail.value ?? 'Phone')}>
              {(s.lookups!.enums.jobSource ?? ['Phone']).map(x => <IgrSelectItem key={x} value={x}>{x === 'IoTAlert' ? 'Wayside alarm' : x}</IgrSelectItem>)}
            </IgrSelect>
            <fieldset className="fs">
              <legend>Severity</legend>
              <IgrRadioGroup alignment="horizontal" name="sev" value={priority} onChange={e => setPriority(e.detail.value as JobPriority)}>
                <IgrRadio value="Critical" checked={priority === 'Critical'}>Critical — safety or line blocked</IgrRadio>
                <IgrRadio value="High" checked={priority === 'High'}>High — degraded service</IgrRadio>
              </IgrRadioGroup>
              <span className="small muted">Any dispatcher may raise a safety-critical fault; Critical notifies the shift supervisor.</span>
            </fieldset>
            <IgrInput label="Reported by (name / radio call sign)" value={reporter} onInput={e => setReporter(e.detail)} />
            <fieldset className="fs span2">
              <legend>Service impact</legend>
              <IgrRadioGroup name="impact" value={impact} onChange={e => setImpact(e.detail.value as ImpactId)}>
                {IMPACTS.map(i => <IgrRadio key={i.id} value={i.id} checked={impact === i.id}>{i.label}</IgrRadio>)}
              </IgrRadioGroup>
            </fieldset>
          </div>

          <div className="card-head"><h2>3 · Safety restriction</h2></div>
          <div className="card-body form-grid">
            <IgrCheckbox className="span2" checked={restriction} onChange={e => setRestriction(e.detail.checked)}>An operating or safety restriction is in force</IgrCheckbox>
            {restriction ? (
              <IgrInput className="span2" label="Restriction (e.g. 10 mph over MP 44.1–44.6, Track 2 out of service)" value={restrictionText}
                onInput={e => setRestrictionText(e.detail)} invalid={!!show(errors.restriction)} required>
                {show(errors.restriction) ? <span slot="helper-text">{errors.restriction}</span> : null}
              </IgrInput>
            ) : null}
            <IgrTextarea className="span2" label="What was observed (optional)" rows={3} value={description} onInput={e => setDescription(e.detail)} />
          </div>
          <div className="form-foot">
            {touched && !valid ? <span className="txt-crit" role="alert"><Icon name="octagon" /> Complete the highlighted fields.</span> : <span className="muted small">Recorded as {s.user!.fullName} · {dayTime(s.opsNowIso())} EST</span>}
            <IgrButton type="submit" disabled={busy}><Icon name="arrow_up" /> Record &amp; escalate</IgrButton>
          </div>
        </form>

        <aside className="card esc-card" aria-label="Escalation path">
          <div className="card-head"><h2>Escalation path</h2><span className="muted small">Updates as you fill the form</span></div>
          <div className="card-body">
            <div className="esc-summary">
              <PriorityTag priority={priority} />
              <span>{impactMeta.label}</span>
              {restriction ? <span className="tag tone-crit tag-compact"><Icon name="shield" /> Restriction</span> : null}
            </div>
            <ol className="esc-list">
              {escalation.map(e => (
                <li key={e.who}>
                  <strong>{e.who}</strong>
                  <span>{e.what}</span>
                  {e.required ? <span className="tag tone-crit tag-compact">Required</span> : <span className="tag tone-neutral tag-compact">Advisory</span>}
                </li>
              ))}
            </ol>
            <h4>Location context</h4>
            <dl className="kv">
              <dt>Region</dt><dd>{s.regionName}</dd>
              <dt>Line</dt><dd>{line?.name ?? '—'}</dd>
              <dt>Location</dt><dd>{site ? `${site.name}, ${site.city}` : '—'}</dd>
              <dt>Response target</dt><dd>{priority === 'Critical' ? 'Reactive · per line tier policy (typically ≤ 2 h)' : 'Reactive · per line tier policy'}</dd>
            </dl>
            <p className="small muted" style={{ marginTop: 12 }}>
              After recording, the console ranks crews by qualification, availability, travel and authorized access — proximity alone never decides.
            </p>
          </div>
        </aside>
      </div>
    </div>
  );
}

