import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';
import { apiGet } from '../api/client';
import type { Clock, Dispatcher } from '../api/types';
import { useQuery } from '../hooks/useQuery';

/** Persistent location context and acting dispatcher, shared by every route. */
export interface ConsoleState {
  regionId: number | null;
  setRegionId: (id: number | null) => void;
  dispatcher: Dispatcher | null;
  dispatchers: Dispatcher[];
  setDispatcherId: (id: number) => void;
  canOverride: boolean;
  clock: ReturnType<typeof useQuery<Clock>>;
  dispatcherQuery: ReturnType<typeof useQuery<Dispatcher[]>>;
}

const ConsoleContext = createContext<ConsoleState | null>(null);

export function ConsoleProvider({ children }: { children: ReactNode }) {
  const [regionId, setRegionId] = useState<number | null>(null);
  const [dispatcherId, setDispatcherId] = useState<number | null>(null);

  const dispatcherQuery = useQuery<Dispatcher[]>('dispatchers', (s) =>
    apiGet<{ items?: Dispatcher[] } | Dispatcher[]>('/api/dispatchers', { isActive: undefined }, s).then((r) =>
      Array.isArray(r) ? r : (r.items ?? []),
    ),
  );
  const clock = useQuery<Clock>('clock', (s) => apiGet<Clock>('/api/meta/clock', undefined, s), 60_000);

  const dispatchers = useMemo(() => (dispatcherQuery.data ?? []).filter((d) => d.isActive), [dispatcherQuery.data]);
  const dispatcher = useMemo(() => {
    if (dispatcherId !== null) return dispatchers.find((d) => d.id === dispatcherId) ?? null;
    return dispatchers.find((d) => d.role === 'Supervisor') ?? dispatchers[0] ?? null;
  }, [dispatchers, dispatcherId]);

  const value: ConsoleState = {
    regionId,
    setRegionId,
    dispatcher,
    dispatchers,
    setDispatcherId,
    canOverride: dispatcher?.role === 'Supervisor' || dispatcher?.role === 'SeniorDispatcher',
    clock,
    dispatcherQuery,
  };
  return <ConsoleContext value={value}>{children}</ConsoleContext>;
}

export function useConsole(): ConsoleState {
  const ctx = useContext(ConsoleContext);
  if (!ctx) throw new Error('useConsole must be used inside ConsoleProvider');
  return ctx;
}
