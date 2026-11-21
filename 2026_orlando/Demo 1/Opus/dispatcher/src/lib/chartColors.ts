/** Resolves an Ignite UI palette token (e.g. --ig-error-500) to a concrete color for chart series. */
export function token(name: string, fallback = '#666'): string {
  const v = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return v || fallback;
}

/**
 * Semantic series colors, pinned to the Fluent palette tokens so charts use the same status language
 * as grids and tags: red = breached, amber = at risk, slate-blue = on track / neutral, green = met.
 */
export const chartColors = () => ({
  breached: token('--ig-error-500'),
  atRisk: token('--ig-warn-200'),
  onTrack: token('--ig-primary-300'),
  met: token('--ig-success-300'),
  primary: token('--ig-primary-500'),
  neutral: 'hsl(220 14% 78%)',
  text: 'hsl(220 10% 38%)',
  grid: 'hsl(220 14% 90%)',
});
