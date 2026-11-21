import { useSyncExternalStore } from 'react';

/** Tracks whether the dispatch service is reachable; every API call reports in. */
export interface ConnectionState {
  online: boolean;
  lastOkAt: number | null;
  lastError: string | null;
}

let state: ConnectionState = { online: true, lastOkAt: null, lastError: null };
const listeners = new Set<() => void>();
const emit = () => listeners.forEach(l => l());

export const connection = {
  ok() {
    if (!state.online || state.lastError) {
      state = { online: true, lastOkAt: Date.now(), lastError: null };
      emit();
    } else {
      state = { ...state, lastOkAt: Date.now() };
      // No emit for routine successes: avoids re-rendering the shell on every request.
    }
  },
  fail(message: string) {
    state = { ...state, online: false, lastError: message };
    emit();
  },
  get: () => state,
  subscribe(l: () => void) {
    listeners.add(l);
    return () => listeners.delete(l);
  },
};

export function useConnection() {
  return useSyncExternalStore(connection.subscribe, connection.get);
}
