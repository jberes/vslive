import { useState } from 'react';
import { Calendar, ClipboardCheck, Coffee, Columns3, Download, Gauge, Mail, MapPin, Plus, ShieldCheck, TrendingUp, Users, CircleDollarSign } from 'lucide-react';
import { IgrAvatar } from 'igniteui-react';
import { IgrGridLite, IgrGridLiteColumn, type IgrCellContext } from 'igniteui-react/grid-lite';
import { CustomerCharts, CustomerSparkline } from './charts';
import { exportCsv, type Customer } from './data';
import { Action, Chip, Filters, Metric, Pagination, Panel, SearchBox, usePaging } from './ui';

const categories = ['All Customers', 'Key Accounts', 'Recently Active', 'High Volume'].map((label) => ({ label }));

export function Customers({ customers, globalQuery, add, notify }: {
  customers: Customer[]; globalQuery: string; add: () => void; notify: (message: string) => void;
}) {
  const [query, setQuery] = useState('');
  const [filter, setFilter] = useState('All Customers');
  const [range, setRange] = useState('30');
  const [sort, setSort] = useState('revenue');
  const [showFocus, setShowFocus] = useState(true);
  const [selected, select] = useState('CUST-00102');
  const rows = customers.filter((customer) => {
    const text = `${customer.company} ${customer.contact} ${customer.location} ${customer.id}`.toLowerCase();
    return [query, globalQuery].every((part) => text.includes(part.trim().toLowerCase())) &&
      (filter !== 'Key Accounts' || customer.revenue >= 125000) &&
      (filter !== 'High Volume' || customer.revenue >= 100000) &&
      (filter !== 'Recently Active' || customer.recent);
  }).sort((a, b) => sort === 'revenue' ? b.revenue - a.revenue : a.company.localeCompare(b.company));
  const paging = usePaging(rows, `${query}|${globalQuery}|${filter}|${sort}`);
  const current = customers.find((customer) => customer.id === selected);
  const download = () => exportCsv('aura-customers.csv', rows, ['id', 'company', 'contact', 'location', 'focus', 'status', 'revenue']);

  return <div className="screen customers">
    <div className="page-heading">
      <div>
        <div className="title-line"><h1>Customer Intelligence &amp; Accounts</h1><Chip tone="blue">2,480 Active Accounts</Chip></div>
        <div className="live"><i /> Live sync active</div>
        <p>Enterprise distribution telemetry, relationship lifecycles, and predictive fulfillment metrics.</p>
      </div>
      <div className="page-actions">
        <Action tone="white" onClick={download}><Download size={15} />Export CSV</Action>
        <Action tone="white" onClick={() => notify(`Demo report: ${rows.length} matching accounts.`)}><TrendingUp size={15} />Run Report</Action>
        <Action tone="primary" onClick={add}><Plus size={16} />Add Customer</Action>
      </div>
    </div>
    <Panel className="toolbar customer-toolbar">
      <Filters options={categories} value={filter} change={setFilter} label="Customer segments" />
      <div className="toolbar-controls">
        <SearchBox value={query} change={setQuery} label="Filter company, lead, country…" />
        <label className="select-control"><Calendar size={14} /><select aria-label="Reporting period" value={range} onChange={(event) => setRange(event.target.value)}><option value="30">Last 30 Days</option><option value="90">Last 90 Days</option></select></label>
        <button className="square-button" aria-label="Toggle focus column" aria-pressed={showFocus} onClick={() => setShowFocus(!showFocus)}><Columns3 size={16} /></button>
      </div>
    </Panel>
    <section className="metrics" aria-label="Customer metrics">
      <Metric title="Total customer value" icon={<CircleDollarSign />} tone="pink" value="$1.42M" caption={<><b className="positive">↗ +12.4%</b> &nbsp;vs last month</>}><CustomerSparkline /></Metric>
      <Metric title="Active accounts" icon={<Users />} tone="blue" value="1,842" caption={<b className="positive">✧ 94.2% retention</b>} detail="Target: 92%" meter={{ value: 94.2, tone: 'slate' }} />
      <Metric title="Top product focus" icon={<Coffee />} tone="lime" value="Confections & Bev." caption="Gross Revenue Share" detail={<b>36.99%</b>} meter={{ value: 37, tone: 'lime' }} />
      <Metric title="Avg order fulfillment" icon={<Gauge />} tone="blue" value="2.4" unit="days" caption={<><b className="positive">↓ -0.6d improvement</b> &nbsp;MoM</>} />
    </section>
    <div className="customer-body">
      <div className="directory-column">
        <Panel className="directory">
          <div className="table-heading"><h2>Client Directory</h2><Chip>{paging.visible.length} displayed of {paging.total}</Chip>
            <label>Sort by: <select aria-label="Sort customers" value={sort} onChange={(event) => setSort(event.target.value)}><option value="revenue">Revenue Volume</option><option value="company">Company Name</option></select></label>
          </div>
          {paging.total ? <div className="table-scroll"><IgrGridLite className="crm-grid customer-grid" data={paging.visible} adoptRootStyles aria-label="Client directory" style={{ height: `${44 + paging.visible.length * 72}px` }}>
            <IgrGridLiteColumn field="company" header="Customer / Company" width="minmax(190px, 2.25fr)" cellTemplate={({ row }: IgrCellContext<Customer>) => {
              const customer = row.data!;
              return <div className="cell-profile"><button className={`check ${selected === customer.id ? 'checked' : ''}`} role="checkbox" aria-checked={selected === customer.id} aria-label={`Focus ${customer.company}`} onClick={() => select(selected === customer.id ? '' : customer.id)} />
                <IgrAvatar shape="rounded" initials={customer.initials} className={selected === customer.id ? 'avatar-active' : ''} />
                <div><strong>{customer.company}</strong><small>ID: #{customer.id}</small></div></div>;
            }} />
            <IgrGridLiteColumn field="contact" header="Primary Contact" cellTemplate={({ row }: IgrCellContext<Customer>) => <div><strong>{row.data!.contact}</strong><small>{row.data!.role}</small></div>} />
            <IgrGridLiteColumn field="location" header="Location" width="minmax(76px, .75fr)" cellTemplate={({ value }: IgrCellContext<Customer>) => <span className="location"><MapPin size={13} />{value}</span>} />
            <IgrGridLiteColumn field="focus" header="Focus" width="minmax(88px, .8fr)" hidden={!showFocus} cellTemplate={({ value }: IgrCellContext<Customer>) => <Chip tone={value === 'Beverages' ? 'blue' : value === 'Condiments' ? 'pink' : 'lime'}>{value}</Chip>} />
            <IgrGridLiteColumn field="status" header="Status" width="minmax(69px, .62fr)" cellTemplate={({ value }: IgrCellContext<Customer>) => <Chip tone={value === 'Active' ? 'green' : 'blue'}><i className="dot" />{value}</Chip>} />
          </IgrGridLite></div> : <p className="empty">No customers match your filters.</p>}
          <div className="table-footer"><span>Showing <b>{paging.first} - {paging.last}</b> of {paging.total} accounts</span><Pagination {...paging} /></div>
        </Panel>
        <section className="quick-stats" aria-label="Account signals">
          <Panel><span className="icon-tile pink"><ClipboardCheck /></span><div><h2>EDI Auto-Reorder</h2><p>418 accounts configured</p></div></Panel>
          <Panel><span className="icon-tile blue"><Mail /></span><div><h2>Pending Quotes</h2><p>23 open dispatch sheets</p></div></Panel>
          <Panel><span className="icon-tile lime"><ShieldCheck /></span><div><h2>Credit Risk Index</h2><p>Low across 98.4% tiers</p></div></Panel>
        </section>
      </div>
      <CustomerCharts range={range} company={current?.company} />
    </div>
  </div>;
}
