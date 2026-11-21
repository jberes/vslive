import { createContext, use, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api } from '../api/client';
import type { Dispatcher, Lookups, OpsClock } from '../api/types';
import { permissionsFor, type Permissions } from '../lib/permissions';
import { toApi } from '../lib/time';

const LS_USER = 'railops.dispatcherId';
const LS_REGION = 'railops.regionId';

function readLs(key: string): number | null {
  try {
    const v = localStorage.getItem(key);
    return v ? Number(v) : null;
  } catch {
    return null;
  }
}
function writeLs(key: string, v: number | null) {
  try {
    if (v === null) localStorage.removeItem(key);
    else localStorage.setItem(key, String(v));
  } catch { /* storage unavailable: session-only */ }
}

export interface Session {
  ready: boolean;
  bootError: string | null;
  lookups: Lookups | null;
  dispatchers: Dispatcher[];
  user: Dispatcher | null;
  setUserId: (id: number) => void;
  perms: Permissions;
  /** Persistent location context: null = all regions. */
  regionId: number | null;
  setRegionId: (id: number | null) => void;
  regionName: string;
  /** Operations "today" (yyyy-MM-dd) from the API clock — never the browser date. */
  today: string;
  /** Current operations time as a Date (API clock + elapsed local time). */
  opsNow: () => Date;
  opsNowIso: () => string;
  /** ms to add to a wall-clock timestamp to express it in operations time. */
  opsOffset: number;
  retryBoot: () => void;
}

const SessionContext = createContext<Session | null>(null);

export function SessionProvider({ children }: { children: ReactNode }) {
  const [lookups, setLookups] = useState<Lookups | null>(null);
  const [dispatchers, setDispatchers] = useState<Dispatcher[]>([]);
  const [clock, setClock] = useState<{ c: OpsClock; at: number } | null>(null);
  const [userId, setUserIdState] = useState<number | null>(() => readLs(LS_USER));
  const [regionId, setRegionIdState] = useState<number | null>(() => readLs(LS_REGION));
  const [bootError, setBootError] = useState<string | null>(null);
  const [bootTick, setBootTick] = useState(0);

  useEffect(() => {
    let alive = true;
    setBootError(null);
    Promise.all([api.lookups(), api.dispatchers(), api.clock()])
      .then(([l, d, c]) => {
        if (!alive) return;
        setLookups(l);
        setDispatchers(d.filter(x => x.isActive).sort((a, b) => a.fullName.localeCompare(b.fullName)));
        setClock({ c, at: Date.now() });
      })
      .catch(e => alive && setBootError((e as Error).message));
    return () => { alive = false; };
  }, [bootTick]);

  // Re-sync the simulated operations clock every minute.
  useEffect(() => {
    const id = window.setInterval(() => {
      api.clock().then(c => setClock({ c, at: Date.now() })).catch(() => { /* connection store reports it */ });
    }, 60_000);
    return () => window.clearInterval(id);
  }, []);

  // Default user: first Day-shift dispatcher in the remembered region (or NEC North).
  const user = useMemo(() => {
    if (!dispatchers.length) return null;
    return (
      dispatchers.find(d => d.id === userId) ??
      dispatchers.find(d => d.role === 'Dispatcher' && d.regionId === (regionId ?? 1) && d.shiftName === 'Day') ??
      dispatchers[0]
    );
  }, [dispatchers, userId, regionId]);

  // First visit: the location context defaults to the signed-in dispatcher's region.
  useEffect(() => {
    if (user && regionId === null && readLs(LS_REGION) === null) {
      setRegionIdState(user.regionId);
      writeLs(LS_REGION, user.regionId);
    }
  }, [user, regionId]);

  const setUserId = useCallback((id: number) => {
    setUserIdState(id);
    writeLs(LS_USER, id);
  }, []);
  const setRegionId = useCallback((id: number | null) => {
    setRegionIdState(id);
    writeLs(LS_REGION, id ?? 0);
  }, []);

  const opsNow = useCallback(() => {
    if (!clock) return new Date();
    return new Date(new Date(clock.c.now).getTime() + (Date.now() - clock.at));
  }, [clock]);

  const effectiveRegion = regionId ? regionId : null;
  const value: Session = {
    ready: !!lookups && !!clock && !!user,
    bootError,
    lookups,
    dispatchers,
    user,
    setUserId,
    perms: permissionsFor(user?.role ?? 'Dispatcher'),
    regionId: effectiveRegion,
    setRegionId,
    regionName: lookups?.regions.find(r => r.id === effectiveRegion)?.name ?? 'All regions',
    today: clock?.c.today ?? '',
    opsNow,
    opsNowIso: () => toApi(opsNow()),
    opsOffset: clock ? new Date(clock.c.now).getTime() - clock.at : 0,
    retryBoot: () => setBootTick(t => t + 1),
  };
  return <SessionContext value={value}>{children}</SessionContext>;
}

export function useSession(): Session {
  const s = use(SessionContext);
  if (!s) throw new Error('useSession must be used inside SessionProvider');
  return s;
}
