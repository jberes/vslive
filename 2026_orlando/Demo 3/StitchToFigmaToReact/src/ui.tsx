import { useState, type ReactNode } from 'react';
import { ChevronLeft, ChevronRight, Search } from 'lucide-react';
import { IgrButton, IgrCard, IgrChip, IgrInput } from 'igniteui-react';

export function Action({ children, onClick, tone = 'neutral', type = 'button', disabled }: {
  children: ReactNode; onClick?: () => void; tone?: 'primary' | 'neutral' | 'white'; type?: 'button' | 'submit'; disabled?: boolean;
}) {
  return <IgrButton type={type} variant="contained" className={`action ${tone}`} onClick={onClick} disabled={disabled}>{children}</IgrButton>;
}

export function Panel({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <IgrCard className={`panel ${className}`}>{children}</IgrCard>;
}

export function Chip({ children, tone = 'neutral' }: { children: ReactNode; tone?: string }) {
  return <IgrChip className={`status ${tone}`}>{children}</IgrChip>;
}

export function SearchBox({ value, change, label, global = false }: { value: string; change: (value: string) => void; label: string; global?: boolean }) {
  return <IgrInput className={`search ${global ? 'search-global' : ''}`} aria-label={label} placeholder={label} value={value}
    onInput={(event) => change(event.detail)}>
    <span slot="prefix"><Search size={16} aria-hidden /></span>
  </IgrInput>;
}

export function Filters({ options, value, change, label }: {
  options: { label: string; count?: string }[]; value: string; change: (value: string) => void; label: string;
}) {
  return <div className="filters" role="group" aria-label={label}>{options.map((option) =>
    <button type="button" className={value === option.label ? 'selected' : ''} aria-pressed={value === option.label} key={option.label}
      onClick={() => change(option.label)}>{option.label}{option.count && <span>{option.count}</span>}</button>)}</div>;
}

export function Meter({ value, end = value, tone = 'pink', label }: { value: number; end?: number; tone?: string; label: string }) {
  return <div className={`meter ${tone}`} role="meter" aria-label={label} aria-valuemin={0} aria-valuemax={100} aria-valuenow={value}>
    <span className="meter-end" style={{ width: `${end}%` }} /><span className="meter-fill" style={{ width: `${value}%` }} />
  </div>;
}

export function Metric({ title, icon, tone, value, unit, caption, detail, meter, children }: {
  title: string; icon: ReactNode; tone: string; value: string; unit?: ReactNode; caption?: ReactNode; detail?: ReactNode;
  meter?: { value: number; tone: string }; children?: ReactNode;
}) {
  return <Panel className="metric"><div className="metric-top"><span className="eyebrow">{title}</span><span className={`icon-tile ${tone}`}>{icon}</span></div>
    <div className="metric-value"><strong>{value}</strong>{unit && <span>{unit}</span>}{children}</div>
    <div className="metric-caption"><span>{caption}</span>{detail && <span>{detail}</span>}</div>
    {meter && <Meter {...meter} label={title} />}
  </Panel>;
}

const PAGE_SIZE = 5;

export function usePaging<T>(rows: T[], resetKey: string) {
  const [selection, setSelection] = useState({ key: resetKey, page: 1 });
  const count = Math.max(1, Math.ceil(rows.length / PAGE_SIZE));
  const page = Math.min(selection.key === resetKey ? selection.page : 1, count);
  const offset = (page - 1) * PAGE_SIZE;
  return {
    page, count, total: rows.length, first: rows.length ? offset + 1 : 0, last: Math.min(offset + PAGE_SIZE, rows.length),
    visible: rows.slice(offset, offset + PAGE_SIZE), change: (next: number) => setSelection({ key: resetKey, page: next }),
  };
}

export function Pagination({ page, count, change }: { page: number; count: number; change: (next: number) => void }) {
  return <nav className="pagination" aria-label="Pagination">
    <button aria-label="Previous page" disabled={page === 1} onClick={() => change(page - 1)}><ChevronLeft size={15} /></button>
    {Array.from({ length: count }, (_, i) => i + 1).map((number) =>
      <button key={number} aria-current={page === number ? 'page' : undefined} onClick={() => change(number)}>{number}</button>)}
    <button aria-label="Next page" disabled={page >= count} onClick={() => change(page + 1)}><ChevronRight size={15} /></button>
  </nav>;
}
