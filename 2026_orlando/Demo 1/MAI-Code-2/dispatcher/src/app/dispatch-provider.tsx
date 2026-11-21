import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import {
  apiFetch, DispatchContext, type Board, type DispatchState, type Job, type JobDetail,
  type Recommendation, type Region, type SlaSummary,
} from './dispatch-data';

interface Page<T> {
  items: T[];
  totalCount: number;
}

function queryString(values: Record<string, string | number | null>) {
  const query = new URLSearchParams();
  Object.entries(values).forEach(([key, value]) => {
    if (value !== null && value !== '') query.set(key, String(value));
  });
  return query.toString();
}

export function DispatchProvider({ children }: { children: ReactNode }) {
  const [today, setToday] = useState('');
  const [jobs, setJobs] = useState<Job[]>([]);
  const [riskJobs, setRiskJobs] = useState<Job[]>([]);
  const [totalJobs, setTotalJobs] = useState(0);
  const [board, setBoard] = useState<Board | null>(null);
  const [summary, setSummary] = useState<SlaSummary | null>(null);
  const [regions, setRegions] = useState<Region[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [detail, setDetail] = useState<JobDetail | null>(null);
  const [recommendations, setRecommendations] = useState<Recommendation[]>([]);
  const [regionId, setRegionId] = useState<number | null>(null);
  const [priority, setPriority] = useState('');
  const [status, setStatus] = useState('');
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<string | null>(null);

  const loadDetail = useCallback(async (id: number) => {
    setDetail(await apiFetch<JobDetail>(`/jobs/${id}`));
  }, []);

  const refresh = useCallback(async () => {
    setRefreshing(true);
    setError(null);
    try {
      const clock = today ? null : await apiFetch<{ today: string }>('/meta/clock');
      const date = today || clock?.today || '';
      if (!today && date) setToday(date);
      const [jobPage, riskData, boardData, summaryData, lookupData] = await Promise.all([
        apiFetch<Page<Job>>(`/jobs?${queryString({
          date, page: 1, pageSize: 100, regionId, priority, status, search, openOnly: 'true',
          sort: 'priority,responseDueAt',
        })}`),
        apiFetch<Job[]>(`/sla/at-risk?${queryString({ limit: 100, regionId })}`),
        apiFetch<Board>(`/dispatch/board?${queryString({ date, regionId })}`),
        apiFetch<SlaSummary>(`/sla/summary?${queryString({ date, regionId })}`),
        apiFetch<{ regions: Region[] }>('/meta/lookups'),
      ]);
      setJobs(jobPage.items);
      setRiskJobs(riskData);
      setTotalJobs(jobPage.totalCount);
      setBoard(boardData);
      setSummary(summaryData);
      setRegions(lookupData.regions);
      setLastUpdated(new Date().toISOString());
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to refresh operations data.');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [priority, regionId, search, status, today]);

  useEffect(() => {
    queueMicrotask(() => void refresh());
  }, [refresh]);

  useEffect(() => {
    const timer = window.setInterval(() => void refresh(), 60_000);
    return () => window.clearInterval(timer);
  }, [refresh]);

  useEffect(() => {
    if (selectedId == null) {
      queueMicrotask(() => {
        setDetail(null);
        setRecommendations([]);
      });
      return;
    }
    queueMicrotask(() => {
      void loadDetail(selectedId).catch((reason: unknown) => {
        setError(reason instanceof Error ? reason.message : 'Unable to load job details.');
      });
    });
  }, [loadDetail, selectedId]);

  const loadRecommendations = useCallback(async (id: number) => {
    setRecommendations(await apiFetch<Recommendation[]>(`/jobs/${id}/recommendations?limit=6&includeIneligible=false`));
  }, []);

  const assignJob = useCallback(async (jobId: number, technicianId: number, scheduledStart: string) => {
    setMessage(null);
    await apiFetch(`/jobs/${jobId}/assign`, {
      method: 'POST',
      body: JSON.stringify({ technicianId, scheduledStart, dispatcherId: 1, force: false, note: 'Assigned from operations ledger.' }),
    });
    setMessage('Assignment saved. Crew and access window confirmed.');
    await Promise.all([refresh(), loadDetail(jobId)]);
  }, [loadDetail, refresh]);

  const rescheduleJob = useCallback(async (jobId: number, technicianId: number, newStart: string) => {
    setMessage(null);
    await apiFetch(`/jobs/${jobId}/reschedule`, {
      method: 'POST',
      body: JSON.stringify({
        newScheduledStart: newStart,
        technicianId,
        reasonCode: 'TechnicianUnavailable',
        comment: 'Schedule change confirmed in Shift Planner.',
        dispatcherId: 1,
        updateWindow: false,
        force: false,
      }),
    });
    setMessage('Schedule change saved. Conflicts were checked and downstream work remains visible.');
    await refresh();
  }, [refresh]);

  const addHandoverNote = useCallback(async (jobId: number, body: string) => {
    await apiFetch(`/jobs/${jobId}/notes`, {
      method: 'POST',
      body: JSON.stringify({ authorType: 'Dispatcher', authorName: 'Alex Morgan', body, isInternal: true }),
    });
    setMessage('Handover note saved and attached to the job record.');
    await loadDetail(jobId);
  }, [loadDetail]);

  const value = useMemo<DispatchState>(() => ({
    jobs, riskJobs, totalJobs, board, summary, regions, selectedId, detail, recommendations,
    loading, refreshing, error, message, lastUpdated, regionId, priority, status, search,
    setRegionId, setPriority, setStatus, setSearch,
    selectJob: setSelectedId, refresh, loadRecommendations, assignJob, rescheduleJob, addHandoverNote,
  }), [
    jobs, riskJobs, totalJobs, board, summary, regions, selectedId, detail, recommendations,
    loading, refreshing, error, message, lastUpdated, regionId, priority, status, search,
    refresh, loadRecommendations, assignJob, rescheduleJob, addHandoverNote,
  ]);

  return <DispatchContext value={value}>{children}</DispatchContext>;
}
