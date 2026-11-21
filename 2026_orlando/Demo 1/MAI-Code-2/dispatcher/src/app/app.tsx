import { NavLink, Outlet } from 'react-router-dom';
import { IgrButton } from 'igniteui-react';
import { DispatchProvider } from './dispatch-provider';
import { formatTime, useDispatch } from './dispatch-data';

const navItems = [
  { to: '/ledger', label: 'Operations Ledger' },
  { to: '/corridor', label: 'Corridor Control' },
  { to: '/planner', label: 'Shift Planner' },
  { to: '/handover', label: 'Handover' },
];

function Shell() {
  const data = useDispatch();
  return (
    <div className="console-shell">
      <title>Atlantic Corridor Rail · Dispatch Console</title>
      <aside className="primary-nav" aria-label="Primary navigation">
        <div className="brand-mark" aria-label="Atlantic Corridor Rail">
          <span>ACR</span>
          <small>Operations</small>
        </div>
        <nav>
          {navItems.map((item, index) => (
            <NavLink key={item.to} to={item.to} className={({ isActive }) => isActive ? 'nav-item active' : 'nav-item'}>
              <span className="nav-index">0{index + 1}</span>
              <span>{item.label}</span>
            </NavLink>
          ))}
        </nav>
        <div className="nav-spacer" />
        <div className="dispatcher-card">
          <div className="avatar">AM</div>
          <div><strong>Alex Morgan</strong><span>Shift dispatcher · S2</span></div>
          <span className="online-dot" title="Connected" />
        </div>
      </aside>

      <div className="workspace">
        <header className="topbar">
          <div className="location-context">
            <span className="eyebrow">ATLANTIC CORRIDOR RAIL</span>
            <h1>Infrastructure dispatch</h1>
          </div>
          <div className="global-filters">
            <label>
              <span>Region</span>
              <select value={data.regionId ?? ''} onChange={(event) => data.setRegionId(event.target.value ? Number(event.target.value) : null)}>
                <option value="">All regions</option>
                {data.regions.map((region) => <option key={region.id} value={region.id}>{region.name}</option>)}
              </select>
            </label>
            <div className="clock" aria-label="Operations local time">
              <span className="pulse-dot" />
              <div><strong>{formatTime(data.board?.now)}</strong><small>Ops time · EST</small></div>
            </div>
            <IgrButton variant="flat" disabled={data.refreshing} onClick={() => void data.refresh()}>
              {data.refreshing ? 'Refreshing…' : 'Refresh'}
            </IgrButton>
          </div>
        </header>
        {data.error && <div className="system-banner error" role="alert"><strong>Connection issue:</strong> {data.error}</div>}
        {data.message && <div className="system-banner success" role="status">{data.message}</div>}
        <main className="page-content"><Outlet /></main>
      </div>
    </div>
  );
}

export default function App() {
  return <DispatchProvider><Shell /></DispatchProvider>;
}
