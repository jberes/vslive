import type { CSSProperties } from 'react';

// Grid Lite renders templates inside its shadow DOM, so global class selectors do
// not reach them. Cell styling is therefore inline. Two-line cells: 8px vertical
// padding and 3px between lines, which keeps a 56px row from looking jammed.
export const cell = {
  stack: { display: 'flex', flexDirection: 'column', gap: 3, padding: '8px 0', minWidth: 0 } as CSSProperties,
  l1: { fontWeight: 600, color: '#1d2433' } as CSSProperties,
  l2: { fontSize: 12, color: '#5b6270' } as CSSProperties,
  btn: (selected: boolean): CSSProperties => ({
    display: 'flex',
    flexDirection: 'column',
    gap: 3,
    alignItems: 'flex-start',
    justifyContent: 'center',
    minHeight: 56,
    width: '100%',
    padding: '8px 10px',
    margin: 0,
    border: 0,
    background: selected ? '#e8ecfc' : 'transparent',
    boxShadow: selected ? 'inset 3px 0 0 #155dfc' : 'none',
    textAlign: 'left',
    cursor: 'pointer',
    font: 'inherit',
    color: '#1d2433',
  }),
};
