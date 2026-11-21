import { useCallback, useEffect, useEffectEvent, useRef, useState } from 'react';

export const POLL_MS = 30_000;
export const STALE_MS = 90_000;

export interface PollResult<T> {
  data: T | undefined;
  error: Error | undefined;
  /** True only while the first load (or a key change) is in flight. */
  loading: boolean;
  /** True while any request is in flight, including background refreshes. */
  refreshing: boolean;
  /** Wall-clock ms of the last successful load. */
  updatedAt: number | null;
  refresh: () => void;
}

/**
 * Loads data and re-polls on an interval. `key` identifies the query: changing it discards the old
 * data and shows the loading state. Background refresh failures keep the last good data (marked stale
 * by the caller via `updatedAt`) instead of blanking the view.
 */
export function usePolling<T>(
  key: string | null,
  loader: (signal: AbortSignal) => Promise<T>,
  intervalMs: number = POLL_MS,
): PollResult<T> {
  const [data, setData] = useState<T>();
  const [error, setError] = useState<Error>();
  const [loading, setLoading] = useState(key !== null);
  const [refreshing, setRefreshing] = useState(false);
  const [updatedAt, setUpdatedAt] = useState<number | null>(null);
  const [tick, setTick] = useState(0);
  const keyRef = useRef(key);

  const load = useEffectEvent((signal: AbortSignal) => loader(signal));

  useEffect(() => {
    if (key === null) return;
    const keyChanged = keyRef.current !== key;
    keyRef.current = key;
    if (keyChanged) {
      setData(undefined);
      setLoading(true);
    }
    const ctrl = new AbortController();
    setRefreshing(true);
    load(ctrl.signal)
      .then(d => {
        setData(d);
        setError(undefined);
        setUpdatedAt(Date.now());
      })
      .catch(e => {
        if ((e as Error).name !== 'AbortError') setError(e as Error);
      })
      .finally(() => {
        if (!ctrl.signal.aborted) {
          setLoading(false);
          setRefreshing(false);
        }
      });
    return () => ctrl.abort();
  }, [key, tick]);

  useEffect(() => {
    if (key === null || !intervalMs) return;
    const id = window.setInterval(() => setTick(t => t + 1), intervalMs);
    return () => window.clearInterval(id);
  }, [key, intervalMs]);

  const refresh = useCallback(() => setTick(t => t + 1), []);
  return { data, error, loading, refreshing, updatedAt, refresh };
}

/** Re-renders every `ms` so relative times ("12 s ago") and stale flags stay current. */
export function useNow(ms = 1000) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), ms);
    return () => window.clearInterval(id);
  }, [ms]);
  return now;
}
