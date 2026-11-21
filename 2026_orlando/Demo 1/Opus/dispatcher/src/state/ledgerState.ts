// Ledger filters + selection live in the URL (shareable, back/forward friendly) and are mirrored to
// sessionStorage so leaving the ledger (job record, SLA view, …) and coming back restores them.
const KEY = 'railops.ledgerSearch';

export function rememberLedgerSearch(search: string) {
  try { sessionStorage.setItem(KEY, search); } catch { /* ignore */ }
}
export function ledgerHref(): string {
  try {
    const s = sessionStorage.getItem(KEY);
    return `/ledger${s ?? ''}`;
  } catch {
    return '/ledger';
  }
}
/** Ledger link that also selects a job, keeping the other remembered filters. */
export function ledgerHrefFor(jobId: number): string {
  const href = ledgerHref();
  const [path, query = ''] = href.split('?');
  const p = new URLSearchParams(query);
  p.set('job', String(jobId));
  return `${path}?${p.toString()}`;
}
