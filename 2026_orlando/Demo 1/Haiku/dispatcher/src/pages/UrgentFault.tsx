import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { IgrButton, IgrInput, IgrSelect, IgrSelectItem, IgrDateTimeInput, type IgrComponentValueChangedEventArgs } from 'igniteui-react';
import { apiGet, apiSend, ApiError } from '../api/client';
import type { Job, JobType, Paged, Site } from '../api/types';
import { useConsole } from '../context/console';
import { ja } from '../i18n/ja';
import { toLocalIso } from '../lib/format';

export default function UrgentFault() {
  const c = useConsole();
  const navigate = useNavigate();
  const [siteQuery, setSiteQuery] = useState('');
  const [sites, setSites] = useState<Site[]>([]);
  const [siteId, setSiteId] = useState<number | null>(null);
  const [jobTypes, setJobTypes] = useState<JobType[] | null>(null);
  const [jobTypeId, setJobTypeId] = useState<number | null>(null);
  const [priority, setPriority] = useState<'Critical' | 'High'>('Critical');
  const [summary, setSummary] = useState('');
  const [impact, setImpact] = useState('');
  const [start, setStart] = useState<Date>(() => new Date());
  const [end, setEnd] = useState<Date>(() => new Date(Date.now() + 4 * 3600_000));
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function searchSites() {
    try {
      const r = await apiGet<Paged<Site>>('/api/sites', { search: siteQuery, pageSize: 20, regionId: c.regionId });
      setSites(r.items);
    } catch {
      setError(ja.state.error);
    }
  }

  async function loadJobTypes() {
    if (jobTypes) return;
    try {
      const r = await apiGet<JobType[]>('/api/job-types');
      setJobTypes(r);
    } catch {
      setError(ja.state.error);
    }
  }

  const valid = siteId !== null && jobTypeId !== null && summary.trim().length > 0 && end > start;

  async function submit() {
    if (!valid || siteId === null || jobTypeId === null) return;
    setBusy(true);
    setError(null);
    try {
      const created = await apiSend<Job>('POST', '/api/jobs', {
        siteId,
        jobTypeId,
        priority,
        source: 'Phone',
        summary: summary.trim(),
        description: impact.trim() || null,
        windowStart: toLocalIso(start),
        windowEnd: toLocalIso(end),
        dispatcherId: c.dispatcher?.id ?? null,
      });
      navigate(`/jobs/${created.id}`);
    } catch (e) {
      setError(e instanceof ApiError ? (e.problem.detail ?? e.message) : ja.state.error);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="urgent" aria-labelledby="urgent-title">
      <div className="page-head">
        <h1 id="urgent-title" className="page-title">{ja.urgent.title}</h1>
      </div>
      {error && <div className="banner banner-error" role="alert">{error}</div>}
      <form className="form-grid" onSubmit={(e) => { e.preventDefault(); void submit(); }}>
        <fieldset className="form-section">
          <legend>{ja.urgent.site}</legend>
          <div className="search-row">
            <IgrInput label={ja.common.search} value={siteQuery}
              onChange={(e: IgrComponentValueChangedEventArgs) => setSiteQuery(String(e.detail ?? ''))} />
            <IgrButton variant="outlined" onClick={() => void searchSites()}>{ja.common.search}</IgrButton>
          </div>
          <IgrSelect label={ja.urgent.site} value={siteId ? String(siteId) : ''} disabled={sites.length === 0}
            onChange={(e: CustomEvent<any>) => setSiteId(Number(e.detail.value))}>
            {sites.map((s) => <IgrSelectItem key={s.id} value={String(s.id)}>{`${s.name}（${s.city}）`}</IgrSelectItem>)}
          </IgrSelect>
        </fieldset>

        <fieldset className="form-section">
          <legend>{ja.urgent.jobType}</legend>
          <IgrSelect label={ja.urgent.jobType} value={jobTypeId ? String(jobTypeId) : ''} onOpening={() => void loadJobTypes()}
            onChange={(e: CustomEvent<any>) => setJobTypeId(Number(e.detail.value))}>
            {(jobTypes ?? []).map((t) => <IgrSelectItem key={t.id} value={String(t.id)}>{`${t.name}（${t.category}）`}</IgrSelectItem>)}
          </IgrSelect>
          <IgrSelect label={ja.urgent.priority} value={priority}
            onChange={(e: CustomEvent<any>) => setPriority(e.detail.value as 'Critical' | 'High')}>
            <IgrSelectItem value="Critical">{ja.priority.Critical}</IgrSelectItem>
            <IgrSelectItem value="High">{ja.priority.High}</IgrSelectItem>
          </IgrSelect>
        </fieldset>

        <fieldset className="form-section form-wide">
          <legend>{ja.urgent.summary}</legend>
          <IgrInput label={ja.urgent.summary} value={summary} required
            onChange={(e: IgrComponentValueChangedEventArgs) => setSummary(String(e.detail ?? ''))} />
          <IgrInput label={ja.urgent.description} value={impact}
            onChange={(e: IgrComponentValueChangedEventArgs) => setImpact(String(e.detail ?? ''))} />
        </fieldset>

        <fieldset className="form-section">
          <legend>{ja.urgent.windowStart} / {ja.urgent.windowEnd}</legend>
          <IgrDateTimeInput displayFormat="yyyy/MM/dd HH:mm" inputFormat="yyyy/MM/dd HH:mm" label={ja.urgent.windowStart} value={start} onChange={(e: CustomEvent<any>) => e.detail && setStart(e.detail)} />
          <IgrDateTimeInput displayFormat="yyyy/MM/dd HH:mm" inputFormat="yyyy/MM/dd HH:mm" label={ja.urgent.windowEnd} value={end} onChange={(e: CustomEvent<any>) => e.detail && setEnd(e.detail)} />
          {end <= start && <p className="issue-blocking">✖ 受付終了は受付開始より後にしてください。</p>}
        </fieldset>

        <div className="form-actions">
          <span className="muted">{ja.urgent.dispatcher}：{c.dispatcher?.fullName ?? '—'}</span>
          <IgrButton type="submit" variant="contained" disabled={!valid || busy}>{ja.urgent.submit}</IgrButton>
        </div>
      </form>
    </section>
  );
}
