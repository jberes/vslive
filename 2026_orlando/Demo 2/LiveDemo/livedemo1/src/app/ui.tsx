import type { ReactNode } from 'react';
import { IgrButton, IgrDialog, IgrIcon, IgrIconButton, IgrSelect, IgrSelectItem, registerIconFromText } from 'igniteui-react';
import Chart from 'react-apexcharts';
import type { ApexOptions } from 'apexcharts';

const paths: Record<string, string> = {
  home: 'M3 11 12 3l9 8v10h-6v-7H9v7H3z',
  activity: 'M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18m-4 5h1v2H8zm7 0h1v2h-1zM7 13h10a5 5 0 0 1-10 0',
  food: 'M3 13h18v3H3zm0 5h18v3H3zM4 11a8 8 0 0 1 16 0zM17 2h3v6h-3z',
  moon: 'M20 15A9 9 0 0 1 9 3a9 9 0 1 0 11 12',
  leaf: 'M12 21C1 17 2 9 3 8c5 0 8 3 9 6 1-3 4-6 9-6 1 6-3 11-9 13M12 2c5 4 5 7 0 11-5-4-5-7 0-11',
  stats: 'M3 3h18v18H3zm4 12h2v4H7zm4-8h2v12h-2zm4 4h2v8h-2z',
  profile: 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20m0 3a3 3 0 1 1 0 6 3 3 0 0 1 0-6m-6 13c0-6 12-6 12 0z',
  settings: 'm9 2 6 0 1 4 4 1 2 5-3 3 0 5-5 2-3-3-5-1-2-5 3-3zM12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8',
  lock: 'M6 10V7a6 6 0 0 1 12 0v3h3v12H3V10zm3 0h6V7a3 3 0 0 0-6 0zm2 4v4h2v-4z',
  search: 'M10 2a8 8 0 1 0 5 14l6 6 2-2-6-6a8 8 0 0 0-7-12m0 3a5 5 0 1 1 0 10 5 5 0 0 1 0-10',
  bell: 'M4 17h16l-2-4V8a6 6 0 0 0-12 0v5zm5 2h6a3 3 0 0 1-6 0',
  menu: 'M3 5h18v2H3zm0 6h18v2H3zm0 6h18v2H3z',
  run: 'M15 2a2 2 0 1 0 0 4 2 2 0 0 0 0-4m-4 5-5 3 1 2 4-2-1 5-4 5 2 2 5-6 2 6h3l-2-9-3-2 1-3 3 4h5v-2h-4l-3-4z',
  bike: 'M6 11a5 5 0 1 0 0 10 5 5 0 0 0 0-10m0 2a3 3 0 1 1 0 6 3 3 0 0 1 0-6m12-2a5 5 0 1 0 0 10 5 5 0 0 0 0-10m0 2a3 3 0 1 1 0 6 3 3 0 0 1 0-6M13 2l-4 6 5 4v5h2v-6l-4-3 2-3 3 3h5V6h-4l-3-4z',
  yoga: 'M12 2a2 2 0 1 0 0 4 2 2 0 0 0 0-4M2 7l8 3v5l-6 4v3l8-5 8 5v-3l-6-4v-5l8-3-1-2-9 3-9-3z',
  fitness: 'm3 2 4 4-2 2 11 11 2-2 4 4-3 3-4-4 2-2L6 7 4 9 0 5z',
  heart: 'M12 21 3 12C-4 4 6-3 12 5c6-8 16-1 9 7z',
  'heart-outline': 'M12 21 3 12C-4 4 6-3 12 5c6-8 16-1 9 7zM12 18l7-7c5-5-2-10-7-3-5-7-12-2-7 3z',
  fire: 'M13 1c2 6-5 7-2 12 3-1 3-4 3-5 8 7 6 15-3 15C0 23 1 12 6 8c-1 5 1 6 2 7-1-8 6-7 5-14',
  clock: 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20m-1 4h2v6l4 3-1 2-5-4z',
  play: 'M6 3v18l15-9z',
  pause: 'M5 3h5v18H5zm9 0h5v18h-5z',
  edit: 'm3 16 13-13 5 5L8 21H3zm14-15 2-1 5 5-2 2z',
  add: 'M11 3h2v8h8v2h-8v8h-2v-8H3v-2h8z',
  close: 'm5 3 7 7 7-7 2 2-7 7 7 7-2 2-7-7-7 7-2-2 7-7-7-7z',
  download: 'M10 2h4v11l4-4 2 2-8 8-8-8 2-2 4 4zM3 20h18v3H3z',
  water: 'M5 2h14l-2 20H7zm2 4 1 13h8l1-13z',
  'water-full': 'M5 2h14l-2 20H7zm2 3 .3 3h9.4l.3-3z',
  mountain: 'M0 21 9 3l5 8 3-5 7 15zM9 8l-3 6h6z',
  trend: 'm2 17 7-7 5 4 6-8h-5V3h9v9h-3V8l-7 10-5-4-5 5z',
  info: 'M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20m-1 4h2v2h-2zm0 4h2v8h-2z',
};
Object.entries(paths).forEach(([name, path]) => registerIconFromText(name, `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><path fill="currentColor" fill-rule="evenodd" d="${path}"/></svg>`, 'wellness'));

export function Icon({ name, className = '', slot }: { name: string; className?: string; slot?: string }) {
  return <IgrIcon name={name} collection="wellness" className={className} slot={slot} aria-hidden="true" />;
}
export function IconButton({ icon, label, onClick, active = false }: { icon: string; label: string; onClick: () => void; active?: boolean }) {
  return <IgrIconButton name={icon === 'heart' && !active ? 'heart-outline' : icon} collection="wellness" aria-label={label} title={label} onClick={onClick} className={active ? 'is-active' : ''} variant="flat" />;
}
export function Metric({ title, value, unit, icon, tone = 'green', children }: { title: string; value: ReactNode; unit?: string; icon: string; tone?: string; children?: ReactNode }) {
  return <article className={`metric tone-${tone}`}><div className="metric-title"><Icon name={icon} />{title}</div><div className="metric-value">{value}{unit && <span>{unit}</span>}</div>{children && <div className="metric-foot">{children}</div>}</article>;
}
export function Select({ label, value, options, onChange }: { label: string; value: string; options: string[]; onChange: (value: string) => void }) {
  return <IgrSelect label={label} value={value} outlined onChange={event => onChange(event.detail.value)}>{options.map(option => <IgrSelectItem key={option} value={option}>{option}</IgrSelectItem>)}</IgrSelect>;
}
export function SectionTitle({ title, children }: { title: string; children?: ReactNode }) {
  return <div className="section-title"><h2>{title}</h2>{children}</div>;
}
export function Modal({ title, onClose, children, footer }: { title: string; onClose: () => void; children: ReactNode; footer?: ReactNode }) {
  return <IgrDialog open title={title} hideDefaultAction onClosed={onClose} closeOnOutsideClick><div className="dialog-content">{children}</div><div slot="footer" className="dialog-actions"><IgrButton variant="flat" onClick={onClose}>Close</IgrButton>{footer}</div></IgrDialog>;
}

const chartBase: ApexOptions = {
  chart: { toolbar: { show: false }, fontFamily: 'Titillium Web, sans-serif', foreColor: '#616161', animations: { enabled: false }, parentHeightOffset: 0 },
  colors: ['#9DE772', '#8B5BB1', '#6DB1FF'],
  grid: { borderColor: '#e5e5e5', padding: { left: 12, right: 24 } },
  legend: { position: 'bottom', fontSize: '14px' },
  dataLabels: { enabled: false },
  stroke: { width: 2 },
};
export function ActivityChart({ period }: { period: string }) {
  const weekly = period === 'This week';
  const data = weekly ? [2030, 2900, 5760, 9300, 10890, 6930, 3200] : [6100, 7200, 8400, 9300];
  return <div className="chart-panel"><Chart type="bar" height={432} series={[{ name: 'Steps', data }]} options={{
    ...chartBase,
    plotOptions: { bar: { columnWidth: '78%', borderRadius: 0, dataLabels: { position: 'top' } } },
    dataLabels: { enabled: true, offsetY: -20, formatter: value => Number(value).toLocaleString(), style: { colors: ['#616161'], fontSize: '12px', fontWeight: 400 } },
    xaxis: { categories: weekly ? ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'] : ['Week 1', 'Week 2', 'Week 3', 'Week 4'] },
    yaxis: { max: 12000, title: { text: 'Steps' }, labels: { formatter: value => value.toLocaleString() } },
    tooltip: { y: { formatter: value => value === null ? 'No data' : `${value.toLocaleString()} steps` } },
  }} /></div>;
}
export function MacroChart({ period = 'Today' }: { period?: string }) {
  const labels = ['Carbs', 'Protein', 'Fat'];
  return <div className="macro-chart"><Chart type="pie" height={250} series={period === 'Today' ? [55, 25, 20] : [50, 30, 20]} options={{
    ...chartBase,
    labels,
    stroke: { width: 0 },
    plotOptions: { pie: { customScale: 0.9, dataLabels: { offset: 28 } } },
    dataLabels: { enabled: true, formatter: (value, options) => `${options ? `${labels[options.seriesIndex]}: ` : ''}${Math.round(Number(value))}%`, style: { fontSize: '14px', fontWeight: 400, colors: ['#212121'] }, dropShadow: { enabled: false } },
    legend: { show: false },
    tooltip: { y: { formatter: value => `${value}%` } },
  }} /></div>;
}
export function AreaChart({ kind, multiplier = 1 }: { kind: 'sleep' | 'heart'; multiplier?: number }) {
  const sleep = kind === 'sleep';
  const series = sleep ? [
    { name: 'Sleep', data: [30, 42, 66, 70, 58, 40, 78, 12, 80, 40, 82].map(v => Math.round(v * multiplier)) },
    { name: 'Deep sleep', data: [18, 24, 28, 30, 45, 43, 48, 90, 132, 135, 95].map(v => Math.round(v * multiplier)) },
  ] : [{ name: 'Heart rate', data: [55, 80, 132, 138, 115, 75, 155].map(v => Math.round(v * multiplier)) }];
  return <div className={`chart-panel ${sleep ? 'sleep-chart' : 'heart-chart'}`}><Chart type="area" height={sleep ? 390 : 168} series={series} options={{ ...chartBase, colors: sleep ? ['#9DE772', '#8B5BB1'] : ['#9DE772'], markers: { size: 3, strokeColors: '#fff' }, legend: { position: 'top', horizontalAlign: 'left' }, stroke: { curve: sleep ? 'stepline' : 'straight', width: 2 }, fill: { type: 'solid', opacity: sleep ? 0.55 : 0.3 }, xaxis: { categories: sleep ? ['0:30', '1:00', '1:30', '2:00', '2:30', '3:00', '3:30', '4:00', '4:30', '5:00', '5:30'] : ['12:00', '12:05', '12:10', '12:15', '12:20', '12:25', '12:30'] }, yaxis: { min: 0, max: sleep ? 140 : 160, tickAmount: sleep ? 7 : 4, title: { text: sleep ? 'Score' : 'BPM' } } }} /></div>;
}
