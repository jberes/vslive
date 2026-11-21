import { useCallback, useEffect, useRef, useState } from 'react';

export const POLL_MS = 30_000;
export const STALE_AFTER_MS = 90_000;

export interface QueryState<T> {
  data: T | undefined;
  error: unknown;
  loading: boolean;
  /** Time of the last successful response (ms since epoch). */
  lastUpdated: number | null;
  /** True when the last success is older than STALE_AFTER_MS. */
  stale: boolean;
  /** True when the most recent request failed (network or server). */
  disconnected: boolean;
  refetch: () => void;
}

/**
 * Polls a read endpoint. Keeps the last good data on failure so the screen
 * never blanks during a disconnect; `disconnected` and `stale` tell the UI.
 * `key` null disables the query.
 */
export function useQuery<T>(key: string | null, fetcher: (signal: AbortSignal) => Promise<T>, pollMs = POLL_MS): QueryState<T> {
  const [data, setData] = useState<T | undefined>(undefined);
  const [error, setError] = useState<unknown>(null);
  const [loading, setLoading] = useState(false);
  const [lastUpdated, setLastUpdated] = useState<number | null>(null);
  const [disconnected, setDisconnected] = useState(false);
  const [tick, setTick] = useState(0);
  const [now, setNow] = useState(() => Date.now());
  const fetcherRef = useRef(fetcher);
  fetcherRef.current = fetcher;

  useEffect(() => {
    if (key === null) return;
    const ac = new AbortController();
    setLoading(true);
    fetcherRef.current(ac.signal)
      .then((d) => {
        setData(d);
        setError(null);
        setDisconnected(false);
        setLastUpdated(Date.now());
      })
      .catch((e: unknown) => {
        if (ac.signal.aborted) return;
        setError(e);
        setDisconnected(true);
      })
      .finally(() => {
        if (!ac.signal.aborted) setLoading(false);
      });
    return () => ac.abort();
  }, [key, tick]);

  useEffect(() => {
    if (key === null || pollMs <= 0) return;
    const id = window.setInterval(() => setTick((t) => t + 1), pollMs);
    return () => window.clearInterval(id);
  }, [key, pollMs]);

  // Re-evaluate staleness every 5 s without refetching.
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), 5_000);
    return () => window.clearInterval(id);
  }, []);

  const refetch = useCallback(() => setTick((t) => t + 1), []);
  const stale = lastUpdated !== null && now - lastUpdated > STALE_AFTER_MS;
  return { data, error, loading, lastUpdated, stale, disconnected, refetch };
}
