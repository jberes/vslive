import { StrictMode, useEffect, useRef, useState, type FormEvent } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Navigate, NavLink, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { Archive, Bell, ChartNoAxesCombined, CircleHelp, LayoutDashboard, Megaphone, PanelLeft, Plus, SlidersHorizontal, Ticket, Truck, UserRound, Users } from 'lucide-react';
import { IgrDialog, IgrInput, IgrNavbar, configureTheme } from 'igniteui-react';
import 'igniteui-webcomponents/themes/light/material.css';
import './styles.css';
import { Customers } from './customers';
import { Orders } from './orders';
import { initialCustomers, initialOrders } from './data';
import { Action, Meter, SearchBox } from './ui';

configureTheme('material', 'light');

const mainNav = [
  { title: 'Overview', icon: LayoutDashboard, path: '/' },
  { title: 'Customers', icon: Users, path: '/customers' },
  { title: 'Orders & Fulfillment', icon: Truck, path: '/orders' },
  { title: 'Products & Catalog', icon: Archive },
  { title: 'Analytics & Reports', icon: ChartNoAxesCombined },
];
const workspaceNav = [
  { title: 'Marketing', icon: Megaphone }, { title: 'Service & Tickets', icon: Ticket },
  { title: 'Settings', icon: SlidersHorizontal }, { title: 'Resource Center', icon: CircleHelp },
];

function App() {
  const [customers, setCustomers] = useState(initialCustomers);
  const [orders, setOrders] = useState(initialOrders);
  const [query, setQuery] = useState('');
  const [collapsed, setCollapsed] = useState(false);
  const [message, notify] = useState('');
  const dialog = useRef<IgrDialog>(null);
  const form = useRef<HTMLFormElement>(null);
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const isOrders = pathname === '/orders';

  useEffect(() => {
    document.title = `${isOrders ? 'Orders & Fulfillment' : 'Customer Intelligence'} | Aura CRM`;
  }, [isOrders]);
  useEffect(() => {
    if (!message) return;
    const timer = setTimeout(() => notify(''), 4500);
    return () => clearTimeout(timer);
  }, [message]);

  function addRecord(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const name = String(data.get('name') ?? '').trim();
    const person = String(data.get('contact') ?? '').trim();
    const location = String(data.get('location') ?? '').trim();
    if (!name || !person || !location) {
      notify('Please fill out all fields.');
      return;
    }
    const initials = name.split(/\s+/).slice(0, 2).map((token) => token[0].toUpperCase()).join('');
    if (isOrders) {
      const date = new Date().toISOString().slice(0, 10);
      setOrders((existing) => [{
        id: String(Math.max(...existing.map((order) => Number(order.id))) + 1), status: 'Scheduled',
        company: name, initials, rep: person, account: 'C-DEMO', carrier: 'Federal Shipping', ordered: date,
        required: date, shipped: '—', freight: 0, destination: location, address: '',
      }, ...existing]);
    } else {
      setCustomers((existing) => [{
        id: `CUST-${String(102 + existing.length).padStart(5, '0')}`, company: name, initials,
        contact: person, role: 'Primary Contact', location, focus: 'Beverages', status: 'Active',
        revenue: 0, recent: true,
      }, ...existing]);
    }
    setQuery(name);
    form.current?.reset();
    void dialog.current?.hide();
    notify(isOrders ? 'Demo order created.' : 'Demo customer added.');
  }

  function openForm() {
    form.current?.reset();
    void dialog.current?.show();
  }

  function dispatch(id: string) {
    setOrders((existing) => existing.map((order) => order.id === id
      ? { ...order, status: 'In Transit', shipped: new Date().toISOString().slice(0, 10) } : order));
    notify(`Demo order #${id} dispatched.`);
  }

  return <div className={`app-shell${collapsed ? ' collapsed' : ''}`}>
    {/* A static rail keeps real router links and the usage panel in one landmark. */}
    <aside className="sidebar">
      <div className="brand"><NavLink to="/customers" onClick={() => setQuery('')} aria-label="Aura CRM home"><span className="brand-mark">A</span><span className="brand-copy"><b>Aura CRM</b><small>Enterprise SaaS</small></span></NavLink>
        <button className="icon-button collapse-button" aria-label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'} onClick={() => setCollapsed(!collapsed)}><PanelLeft size={17} /></button>
      </div>
      <nav className="sidebar-nav" aria-label="Main navigation">
        <p>Main menu</p>
        {mainNav.map(({ title, path, icon: Icon }) => path
          ? <NavLink key={title} to={path} end aria-label={title} onClick={() => setQuery('')}><Icon size={19} /><span>{title}</span>{path === '/customers' && <b className="nav-count">1,240</b>}</NavLink>
          : <span className="nav-placeholder" key={title} title="Not part of this demo"><Icon size={19} /><span>{title}</span></span>)}
        <p>Workspace</p>
        {workspaceNav.map(({ title, icon: Icon }) => <span className="nav-placeholder" key={title} title="Not part of this demo"><Icon size={19} /><span>{title}</span>{title === 'Service & Tickets' && <i />}</span>)}
      </nav>
      <div className="usage"><div><span className="eyebrow">API usage</span><span>84%</span></div><Meter label="API usage" value={84} /></div>
    </aside>
    <div className="workspace">
      <IgrNavbar className="app-bar">
        <SearchBox global value={query} change={setQuery} label="Search leads, accounts, orders…" />
        <div className="top-actions" slot="end">
          <Action tone="primary" onClick={openForm}><Plus size={16} />New Record</Action>
          <button className="icon-button notification" aria-label="Notifications" onClick={() => notify('No new demo notifications.')}><Bell size={18} /></button>
          <button className="icon-button" aria-label="Help" onClick={() => notify('Aura CRM demo: use the sidebar to switch between Customers and Orders.')}><CircleHelp size={18} /></button>
          <div className="user"><span><UserRound size={17} /></span><div><b>Elena Vance</b><small>Sales Director</small></div></div>
        </div>
      </IgrNavbar>
      <main>
        <Routes>
          <Route path="/" element={<Navigate to="/customers" replace />} />
          <Route path="/customers" element={<Customers customers={customers} globalQuery={query} add={openForm} notify={notify} />} />
          <Route path="/orders" element={<Orders orders={orders} globalQuery={query} add={openForm} dispatch={dispatch} notify={notify} />} />
          <Route path="*" element={<div className="screen"><h1>Page not available</h1><p>Only Customers and Orders are included in this demo.</p><Action onClick={() => navigate('/customers')}>Back to Customers</Action></div>} />
        </Routes>
      </main>
    </div>
    <IgrDialog ref={dialog} title={isOrders ? 'Create Order' : 'Add Customer'} closeOnOutsideClick>
      <form className="record-form" ref={form} onSubmit={addRecord}>
        <p>This record is for the current demo session only.</p>
        <IgrInput name="name" label="Company name" required />
        <IgrInput name="contact" label={isOrders ? 'Account representative' : 'Primary contact'} required />
        <IgrInput name="location" label={isOrders ? 'Destination' : 'City and country'} required />
        <div><Action onClick={() => void dialog.current?.hide()}>Cancel</Action><Action type="submit" tone="primary">Save Record</Action></div>
      </form>
    </IgrDialog>
    <div className={message ? 'toast visible' : 'toast'} role="status">{message}</div>
  </div>;
}

createRoot(document.getElementById('root')!).render(<StrictMode><BrowserRouter><App /></BrowserRouter></StrictMode>);
