import type { ApexOptions } from 'apexcharts';
import ReactApexChart from 'react-apexcharts';
// ApexCharts follows the Ignite UI (Fluent) tokens on the page through its Ignite UI add-on.
// Imported here (not in main.tsx) so ApexCharts loads only with the chart routes.
import 'apexcharts/features/ignite-ui';
import { chartColors } from '../lib/chartColors';

/**
 * Thin wrapper over react-apexcharts. Fonts, text and grid colors come from the page's Ignite UI
 * (Fluent) tokens via `apexcharts/features/ignite-ui`; we only add console-wide defaults here.
 */
export function Chart({
  type, series, options, height = '100%', label,
}: {
  type: 'bar' | 'line' | 'area' | 'rangeBar' | 'scatter';
  series: ApexOptions['series'];
  options: ApexOptions;
  height?: number | string;
  label: string;
}) {
  const c = chartColors();
  const merged: ApexOptions = {
    ...options,
    chart: {
      toolbar: { show: false },
      zoom: { enabled: false },
      animations: { enabled: false },
      parentHeightOffset: 0,
      ...options.chart,
    },
    grid: { borderColor: c.grid, strokeDashArray: 3, ...options.grid },
    dataLabels: { enabled: false, ...options.dataLabels },
    legend: { position: 'top', horizontalAlign: 'left', fontSize: '12px', ...options.legend },
    tooltip: { theme: 'light', ...options.tooltip },
    theme: { designSystem: true, ...options.theme },
  };
  return (
    <div className="chart" role="img" aria-label={label} style={{ height }}>
      <ReactApexChart type={type} series={series} options={merged} height="100%" width="100%" />
    </div>
  );
}
