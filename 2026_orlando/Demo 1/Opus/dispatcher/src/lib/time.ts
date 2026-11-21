// All API timestamps are operations-local (EST) without an offset. Parsing them with `new Date(iso)`
// treats them as local wall-clock time, which is exactly what the console displays (24-hour, EST).

const pad = (n: number) => String(n).padStart(2, '0');
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

export const parse = (iso: string | null | undefined): Date | null => (iso ? new Date(iso) : null);

/** Date → API local ISO string "yyyy-MM-ddTHH:mm:00". */
export function toApi(d: Date): string {
  return `${toDateKey(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}:00`;
}
/** Date → "yyyy-MM-dd". */
export function toDateKey(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}
export function fromDateKey(key: string): Date {
  const [y, m, d] = key.split('-').map(Number);
  return new Date(y, m - 1, d);
}

/** "14:05" */
export function hm(iso: string | Date | null | undefined): string {
  const d = iso instanceof Date ? iso : parse(iso ?? null);
  return d ? `${pad(d.getHours())}:${pad(d.getMinutes())}` : '—';
}
/** "14:05:09" */
export function hms(ms: number): string {
  const d = new Date(ms);
  return `${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
}
/** "17 Nov 14:05", or just "14:05" when it falls on `today`. */
export function dayTime(iso: string | null | undefined, today?: string): string {
  const d = parse(iso ?? null);
  if (!d) return '—';
  if (today && iso!.startsWith(today)) return hm(d);
  return `${d.getDate()} ${MONTHS[d.getMonth()]} ${hm(d)}`;
}
/** "Tue 17 Nov 2026" */
export function longDate(key: string): string {
  const d = fromDateKey(key);
  return `${['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'][d.getDay()]} ${d.getDate()} ${MONTHS[d.getMonth()]} ${d.getFullYear()}`;
}

/** 135 → "2 h 15 min"; negatives are rendered as the absolute value (caller adds "overdue"). */
export function duration(mins: number | null | undefined): string {
  if (mins === null || mins === undefined) return '—';
  const m = Math.abs(Math.round(mins));
  if (m < 60) return `${m} min`;
  const h = Math.floor(m / 60);
  const r = m % 60;
  if (h >= 48) return `${Math.round(h / 24)} d`;
  return r ? `${h} h ${r} min` : `${h} h`;
}

/** "in 1 h 20 min" / "45 min overdue" */
export function remaining(mins: number | null | undefined): string {
  if (mins === null || mins === undefined) return '—';
  return mins < 0 ? `${duration(mins)} overdue` : `${duration(mins)} left`;
}

/** Seconds/minutes since a wall-clock ms timestamp, for freshness labels. */
export function ago(ms: number | null, now: number): string {
  if (!ms) return 'never';
  const s = Math.max(0, Math.round((now - ms) / 1000));
  if (s < 60) return `${s} s ago`;
  return `${Math.floor(s / 60)} min ago`;
}
