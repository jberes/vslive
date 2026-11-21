// Operations-local, 24-hour time. The API sends timestamps without an offset.
export function fmtTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleTimeString('ja-JP', { hour: '2-digit', minute: '2-digit', hour12: false });
}

export function fmtDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return `${d.toLocaleDateString('ja-JP', { month: '2-digit', day: '2-digit' })} ${fmtTime(iso)}`;
}

/** Duration for SLA chips: "残り 24分" before the due time, "36分" once it has passed. */
export function fmtMinutes(mins: number | null | undefined): string {
  if (mins === null || mins === undefined) return '—';
  const abs = Math.abs(mins);
  const h = Math.floor(abs / 60);
  const m = abs % 60;
  const body = h > 0 ? `${h}時間${String(m).padStart(2, '0')}分` : `${m}分`;
  return mins < 0 ? body : `残り ${body}`;
}

/** Converts a Date to the API's local ISO format: yyyy-MM-ddTHH:mm:ss */
export function toLocalIso(d: Date): string {
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
}

export function parseLocal(iso: string): Date {
  return new Date(iso);
}

export function minutesBetween(a: string, b: string): number {
  return Math.round((new Date(b).getTime() - new Date(a).getTime()) / 60000);
}
