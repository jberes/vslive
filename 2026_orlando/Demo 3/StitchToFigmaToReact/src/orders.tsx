import { useState } from 'react';
import { Archive, BadgeCheck, Banknote, CircleDot, Download, Navigation, Plus, Printer, SlidersHorizontal, Truck, Zap } from 'lucide-react';
import { IgrAvatar } from 'igniteui-react';
import { IgrGridLite, IgrGridLiteColumn, type IgrCellContext } from 'igniteui-react/grid-lite';
import { OrderCharts } from './charts';
import { exportCsv, money, type Order } from './data';
import { Action, Chip, Filters, Metric, Pagination, Panel, SearchBox, usePaging } from './ui';

const tabs = [
  { label: 'All Orders', count: '1,248' }, { label: 'Processing', count: '42' },
  { label: 'In Transit', count: '18' }, { label: 'Delivered', count: '1,180' },
];

export function Orders({ orders, globalQuery, add, dispatch, notify }: {
  orders: Order[]; globalQuery: string; add: () => void; dispatch: (id: string) => void; notify: (message: string) => void;
}) {
  const [query, setQuery] = useState('');
  const [tab, setTab] = useState('All Orders');
  const [segment, setSegment] = useState(false);
  const [carrier, setCarrier] = useState('');
  const [selected, select] = useState('10248');
  const rows = orders.filter((order) => {
    const text = `#${order.id} ${order.company} ${order.carrier} ${order.destination}`.toLowerCase();
    return [query, globalQuery].every((term) => text.includes(term.trim().toLowerCase())) &&
      (tab === 'All Orders' || order.status === (tab === 'Processing' ? 'Scheduled' : tab)) &&
      (!carrier || carrier === order.carrier);
  });
  const paging = usePaging(rows, `${query}|${globalQuery}|${tab}|${carrier}`);
  const current = paging.visible.find((order) => order.id === selected);
  const selectOrder = (id: string) => select(id === selected ? '' : id);
  const download = () => exportCsv('aura-orders.csv', rows, ['id', 'status', 'company', 'carrier', 'ordered', 'required', 'shipped', 'freight', 'destination']);
  const dateCell = ({ value }: IgrCellContext<Order>) => <span className="mono date">{value}</span>;

  return <div className="screen orders">
    <div className="page-heading">
      <div><div className="pipeline"><b>Fulfillment pipeline</b><i />Real-Time Sync 2m ago</div>
        <h1>Orders &amp; Fulfillment Hub</h1><p>Monitor pipeline orders, carrier logistics, and fulfillment benchmarks across regional nodes.</p>
      </div>
      <div className="page-actions">
        <Action onClick={() => window.print()}><Printer size={15} />Print Manifests</Action>
        <Action onClick={() => {
          if (!current) notify('Select a scheduled order to dispatch.');
          else if (current.status !== 'Scheduled') notify(`Order #${current.id} is already ${current.status.toLowerCase()}.`);
          else dispatch(current.id);
        }}><Truck size={16} />Dispatch Orders</Action>
        <Action tone="primary" onClick={add}><Plus size={16} />Create Order</Action>
      </div>
    </div>
    <section className="metrics" aria-label="Fulfillment metrics">
      <Metric title="Active in-transit" icon={<Navigation />} tone="pink" value="18" unit="orders routed" caption="Freight Value" detail={<b className="accent">$142,850.00</b>} meter={{ value: 72, tone: 'pink' }} />
      <Metric title="On-time delivery" icon={<BadgeCheck />} tone="lime" value="98.4%" unit={<b className="positive">↑+1.2%</b>} caption="Carrier SLA Target" detail={<b>97.50%</b>} meter={{ value: 98.4, tone: 'lime' }} />
      <Metric title="Avg freight cost" icon={<Banknote />} tone="blue" value="$34.20" unit="/ shipment" caption="Efficiency Index" detail={<b className="positive">-4.8% vs Q3</b>} meter={{ value: 54, tone: 'slate' }} />
      <Metric title="Expedited volume" icon={<Zap />} tone="pink" value="22.9%" unit="Federal & United" caption="Direct Priority" detail={<b>286 Parcels</b>} meter={{ value: 22.9, tone: 'pink' }} />
    </section>
    <Panel className="order-directory">
      <div className="toolbar order-toolbar">
        <Filters options={tabs} value={tab} change={setTab} label="Order status" />
        <div className="toolbar-controls">
          <SearchBox value={query} change={setQuery} label="Search order ID, customer, carrier…" />
          <Action onClick={() => setSegment(!segment)}><SlidersHorizontal size={15} />Filter &amp; Segment</Action>
          <Action onClick={download}><Download size={15} />Export CSV</Action>
        </div>
      </div>
      {segment && <div className="segment"><label>Logistics carrier <select value={carrier} onChange={(event) => setCarrier(event.target.value)}><option value="">All carriers</option>{['Federal Shipping', 'Speedy Express', 'United Package'].map((name) => <option key={name}>{name}</option>)}</select></label></div>}
      {paging.total ? <div className="table-scroll"><IgrGridLite className="crm-grid order-grid" data={paging.visible} adoptRootStyles aria-label="Orders and fulfillment" style={{ height: `${50 + paging.visible.length * 101}px` }}>
        <IgrGridLiteColumn field="id" header="Order / Status" width="minmax(140px, 1.65fr)" cellTemplate={({ row }: IgrCellContext<Order>) => {
          const order = row.data!;
          return <div className="order-status"><button className={`check ${selected === order.id ? 'checked' : ''}`} role="checkbox" aria-checked={selected === order.id} aria-label={`Select order ${order.id}`} onClick={() => selectOrder(order.id)} />
            <strong className={`mono ${selected === order.id ? 'accent' : ''}`}>#{order.id}</strong>
            <Chip tone={order.status === 'Delivered' ? 'lime' : order.status === 'In Transit' ? 'blue' : 'neutral'}>{order.status}</Chip>
          </div>;
        }} />
        <IgrGridLiteColumn field="company" header="Customer / Account" width="minmax(150px, 1.72fr)" cellTemplate={({ row }: IgrCellContext<Order>) => {
          const order = row.data!;
          return <div className="cell-profile"><IgrAvatar initials={order.initials} shape="circle" className={`order-avatar ${Number(order.id) % 3 === 0 ? 'pink' : Number(order.id) % 3 === 1 ? 'blue' : 'lime'}`} /><div><strong>{order.company}</strong><small>{order.rep} · ID: {order.account}</small></div></div>;
        }} />
        <IgrGridLiteColumn field="carrier" header="Logistics Carrier" width="minmax(92px, 1.1fr)" cellTemplate={({ value }: IgrCellContext<Order>) => <Chip tone="carrier">{value === 'Federal Shipping' ? <Truck size={13} /> : value === 'Speedy Express' ? <Zap size={13} /> : <Archive size={13} />}{value}</Chip>} />
        <IgrGridLiteColumn field="ordered" header="Order Date" width="minmax(64px, .72fr)" cellTemplate={dateCell} />
        <IgrGridLiteColumn field="required" header="Req. Date" width="minmax(64px, .72fr)" cellTemplate={dateCell} />
        <IgrGridLiteColumn field="shipped" header="Shipped Date" width="minmax(64px, .72fr)" cellTemplate={dateCell} />
        <IgrGridLiteColumn field="freight" header="Freight" width="minmax(66px, .78fr)" cellTemplate={({ value }: IgrCellContext<Order, 'freight'>) => <strong className="mono">{money.format(value)}</strong>} />
        <IgrGridLiteColumn field="destination" header="Destination Node" cellTemplate={({ row }: IgrCellContext<Order>) => <div><strong>{row.data!.destination}</strong><small>{row.data!.address}</small></div>} />
        <IgrGridLiteColumn field="account" header="Actions" width="minmax(58px, .62fr)" cellTemplate={({ row }: IgrCellContext<Order>) => <button className="icon-button" aria-label={`Toggle details for order ${row.data!.id}`} aria-pressed={selected === row.data!.id} onClick={() => selectOrder(row.data!.id)}><CircleDot size={17} /></button>} />
      </IgrGridLite></div> : <p className="empty">No orders match your filters.</p>}
      <div className="table-footer order-footer">
        <div className="order-detail">{current && <><strong><CircleDot size={15} />Order #{current.id} details expanded</strong><span>Carrier: {current.carrier} | Tracking: {current.status === 'Scheduled' ? 'Not assigned' : `FS-${current.id === '10248' ? '8829104' : current.id}-EU`} | Weight: {current.id === '10248' ? '14.8' : '12.5'} kg</span></>}</div>
        <span>Showing {paging.visible.length} of {paging.total} records</span><Pagination {...paging} />
      </div>
    </Panel>
    <OrderCharts />
  </div>;
}
