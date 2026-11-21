import { lazy, Suspense } from 'react';
import type { ApexOptions } from 'apexcharts';
import { ChartNoAxesColumn, BadgeCheck } from 'lucide-react';
import { Meter, Panel } from './ui';

const Chart = lazy(() => import('react-apexcharts'));
const colors = { pink: '#e51670', slate: '#515d69', lime: '#95e361', guide: '#b8c5d0' };

function Area({ freight = false, spark = false, range = '30' }: { freight?: boolean; spark?: boolean; range?: string }) {
  const series = spark ? [{ name: 'Customer value', data: [25, 30, 40, 50, 44, 32, 34, 55] }]
    : freight ? [{ name: 'Federal Shipping', data: [26, 34, 28, 42, 38, 52, 56] }, { name: 'United Package', data: [20, 24, 32, 30, 34, 40, 44] }]
      : [{ name: 'Revenue', data: range === '90' ? [250, 294, 358, 395, 460] : [280, 310, 360, 368, 405] }, { name: 'Target', type: 'line', data: [288, 320, 365, 378, 392] }];
  const categories = spark ? undefined : freight ? ['May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov'] : ['Q1', 'Q2', 'Q3', 'Q3 close', 'Q4'];
  const options: ApexOptions = {
    chart: { toolbar: { show: false }, sparkline: { enabled: true }, animations: { enabled: false }, parentHeightOffset: 0 },
    colors: [colors.pink, freight ? colors.slate : colors.guide],
    stroke: { curve: freight ? 'straight' : 'smooth', width: spark ? 2 : [3, 2], dashArray: freight || spark ? 0 : [0, 4] },
    fill: { type: 'gradient', gradient: { opacityFrom: 0.4, opacityTo: 0.03, stops: [0, 100] } },
    xaxis: { categories, crosshairs: { show: false } },
    yaxis: { min: freight ? 14 : spark ? 15 : 260, max: freight ? 61 : spark ? 58 : range === '90' ? 470 : 415 },
    dataLabels: { enabled: false }, legend: { show: false }, tooltip: { enabled: !spark },
    annotations: freight ? { yaxis: [{ y: 50, borderColor: colors.guide, strokeDashArray: 4 }] } : undefined,
  };
  const height = spark ? 32 : freight ? 154 : 112;
  return <div className={spark ? 'sparkline' : 'area-chart'} role="img" aria-label={spark ? 'Customer value rising' : freight ? 'Freight costs by carrier with target line' : 'Revenue rising across four quarters compared to target'}>
    <Suspense fallback={<div style={{ height }} />}><Chart type="area" height={height} width="100%" options={options} series={series} /></Suspense>
  </div>;
}

function Donut({ freight = false }: { freight?: boolean }) {
  const labels = freight ? ['Federal Shipping', 'United Package', 'Speedy Express'] : ['Beverages', 'Condiments', 'Confections'];
  const shares = freight ? [66.27, 22.89, 10.84] : [56.2, 37, 6.8];
  const palette = [colors.pink, colors.slate, colors.lime];
  const options: ApexOptions = {
    chart: { animations: { enabled: false }, sparkline: { enabled: true } },
    labels, colors: palette, stroke: { width: 0 }, dataLabels: { enabled: false }, legend: { show: false },
    plotOptions: { pie: { expandOnClick: false, donut: { size: '76%' } } },
    tooltip: { y: { formatter: (value) => `${value}%` } },
  };
  return <div className="donut-layout">
    <div className="donut-plot">
      <Suspense fallback={null}><Chart type="donut" series={shares} options={options} width="100%" height="100%" /></Suspense>
      <div className="donut-total"><strong>{freight ? '1,248' : '2.4k'}</strong><span>{freight ? 'Parcels' : 'Orders'}</span></div>
    </div>
    <ul className="chart-legend">{labels.map((label, index) => <li key={label}>
      <i className={['pink', 'slate', 'lime'][index]} /><span>{label}</span><b>{shares[index].toFixed(freight ? 2 : 1)}%</b>
    </li>)}</ul>
  </div>;
}

export function CustomerSparkline() { return <Area spark />; }

export function CustomerCharts({ range, company }: { range: string; company?: string }) {
  return <aside className="customer-charts" aria-label="Customer analytics">
    <Panel className="chart-panel"><h2>Product Category Affinity</h2><p>Legacy Northwind Product Portfolio Breakdown</p><Donut /></Panel>
    <Panel className="chart-panel">
      <div className="chart-heading"><div><h2>Quarterly Revenue Velocity</h2><p>Target trajectory vs realized delivery</p></div><b className="delta">+18.6% YTD</b></div>
      <Area range={range} />
      <div className="quarter-labels"><span>Q1 (${range === '90' ? '250' : '280'}k)</span><span>Q2 ($340k)</span><b>Q3 ($395k)</b><b>Q4 (Proj: ${range === '90' ? '460' : '405'}k)</b></div>
    </Panel>
    <Panel className="chart-panel">
      <div className="chart-heading"><div><h2>Order Volume by Category</h2><p>Monthly units fulfilled (Gross vs Direct)</p></div><ChartNoAxesColumn size={17} /></div>
      <div className="bar-list">{[
        ['Exotic Beverages', '1,348', 72, 88, 'pink'],
        ['Artisan Condiments', '888', 54, 72, 'slate'],
        ['Confections & Sweets', '164', 24, 32, 'lime'],
      ].map(([label, amount, value, end, tone]) => <div key={label}><div><span>{label}</span><b>{amount} units</b></div><Meter label={String(label)} value={Number(value)} end={Number(end)} tone={String(tone)} /></div>)}</div>
      <div className="chart-foot"><strong><BadgeCheck size={15} />{company ? `${company} Focused` : 'All accounts'}</strong><span>99.8% SLA delivery</span></div>
    </Panel>
  </aside>;
}

export function OrderCharts() {
  return <section className="order-charts" aria-label="Fulfillment analytics">
    <Panel className="chart-panel"><div className="chart-heading"><span className="eyebrow">Cost analysis</span><span className="delta blue">Monthly</span></div>
      <h2>Freight Cost Trends vs Target</h2><p>Carrier cost variations over the last 6 months compared to baseline SLA spend.</p>
      <Area freight />
      <div className="chart-foot freight-legend"><span><i className="pink" />Federal Shipping</span><span><i className="slate" />United Package</span><span><i className="guide" />Target SLA</span></div>
    </Panel>
    <Panel className="chart-panel"><div className="chart-heading"><span className="eyebrow">Volume split</span></div>
      <h2>Carrier Share Distribution</h2><p>Allocation of order dispatches across primary logistics partnerships.</p><Donut freight />
      <div className="chart-foot"><span>Highest On-Time Rate:</span><strong>Speedy Express (99.1%)</strong></div>
    </Panel>
    <Panel className="chart-panel"><div className="chart-heading"><span className="eyebrow">Throughput</span><strong className="accent eyebrow">Q1–Q4 Target Exceeded</strong></div>
      <h2>Quarterly Fulfillment Volumes</h2><p>Dispatched units compared against processing backlog milestones.</p>
      <div className="bar-list quarters">{[
        ['Q1 Milestone', 298, 310, 272], ['Q2 Milestone', 342, 350, 321], ['Q3 Milestone (Peak) ☆', 418, 420, 405], ['Q4 Progress (In Flight)', 190, 250, 167],
      ].map(([label, amount, target, delivered]) => <div key={label}><div><strong>{label}</strong><span className="mono">{amount} / {target} orders</span></div>
        <Meter label={`${label} delivered`} value={Math.round(Number(delivered) / Number(target) * 100)} end={Math.round(Number(amount) / Number(target) * 100)} tone="quarter" /></div>)}</div>
      <div className="throughput-key"><span><i className="pink" />Delivered</span><span><i className="slate" />Backlog / Hold</span></div>
    </Panel>
  </section>;
}
