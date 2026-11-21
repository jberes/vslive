import type { Tone } from '../lib/status';

/**
 * Explicit save outcomes. Every mutation ends with one of these messages (saved / blocked / conflict /
 * failed); they persist until dismissed so a dispatcher never has to guess whether a change landed.
 */
export interface Outcome {
  id: number;
  tone: Tone;
  title: string;
  detail?: string;
  at: number;
}

let items: Outcome[] = [];
let seq = 1;
const listeners = new Set<() => void>();
const emit = () => listeners.forEach(l => l());

export const outcomes = {
  push(o: Omit<Outcome, 'id' | 'at'>) {
    items = [{ ...o, id: seq++, at: Date.now() }, ...items].slice(0, 3);
    emit();
  },
  dismiss(id: number) {
    items = items.filter(i => i.id !== id);
    emit();
  },
  subscribe(l: () => void) {
    listeners.add(l);
    return () => listeners.delete(l);
  },
  get: () => items,
};
