import { IgrButton, IgrLinearProgress, IgrNavbar, IgrSelect, IgrSelectGroup, IgrSelectHeader, IgrSelectItem } from 'igniteui-react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Icon, type IconName } from '../components/Icon';
import { useConnection } from '../state/connection';
import { OutcomeTray } from '../components/OutcomeTray';
import { SessionProvider, useSession } from '../state/session';
import { ledgerHref } from '../state/ledgerState';
import { useNow } from '../state/usePolling';
import { AUTHORITY_NOTE, ROLE_LABEL } from '../lib/permissions';
import { hm, longDate } from '../lib/time';
import type { DispatcherRole } from '../api/types';

export default function App() {
  return (
    <SessionProvider>
      <Shell />
    </SessionProvider>
  );
}

const NAV: { to: string; label: string; icon: IconName }[] = [
  { to: '/ledger', label: 'Operations ledger', icon: 'list' },
  { to: '/sla', label: 'SLA risk', icon: 'chart' },
  { to: '/planner', label: 'Shift planner', icon: 'timeline' },
  { to: '/corridor', label: 'Corridor', icon: 'route' },
  { to: '/handover', label: 'Handover', icon: 'handover' },
];

function Shell() {
  const s = useSession();
  const conn = useConnection();
  const navigate = useNavigate();
  const location = useLocation();
  useNow(15_000); // keep the ops clock display ticking

  if (s.bootError && !s.ready) {
    return (
      <div className="boot">
        <Icon name="cloud_off" className="state-icon" />
        <h1>Cannot reach the dispatch service</h1>
        <p className="muted">{s.bootError}. The console needs the Dispatch API (port 5207) to start.</p>
        <IgrButton onClick={s.retryBoot}><Icon name="refresh" /> Try again</IgrButton>
      </div>
    );
  }
  if (!s.ready) {
    return (
      <div className="boot" role="status">
        <IgrLinearProgress indeterminate aria-label="Starting console" />
        <p className="muted">Starting dispatch console…</p>
      </div>
    );
  }

  const roles: DispatcherRole[] = ['Supervisor', 'SeniorDispatcher', 'Dispatcher'];
  const now = s.opsNow();

  return (
    <div className="shell">
      <a className="skip-link" href="#main">Skip to main content</a>
      <IgrNavbar className="topbar">
        <div slot="start" className="brand">
          <Icon name="train" />
          <span>Rail Ops Dispatch</span>
        </div>
        <nav className="mainnav" aria-label="Primary">
          {NAV.map(n => (
            <NavLink
              key={n.to}
              to={n.to === '/ledger' ? ledgerHref() : n.to}
              className={({ isActive }) => `navlink ${isActive || (n.to === '/ledger' && location.pathname.startsWith('/jobs')) ? 'active' : ''}`}
            >
              <Icon name={n.icon} />
              {n.label}
            </NavLink>
          ))}
          <NavLink to="/incident/new" className={({ isActive }) => `navlink navlink-urgent ${isActive ? 'active' : ''}`}>
            <Icon name="bolt" />
            Report urgent fault
          </NavLink>
        </nav>
        <div slot="end" className="topbar-end">
          <div className="ctx" title="Location context applies to every view">
            <Icon name="place" />
            <IgrSelect
              className="ctx-select"
              aria-label="Region (location context)"
              value={String(s.regionId ?? 0)}
              onChange={e => s.setRegionId(Number(e.detail.value) || null)}
            >
              <IgrSelectItem value="0">All regions</IgrSelectItem>
              {s.lookups!.regions.map(r => (
                <IgrSelectItem key={r.id} value={String(r.id)}>{r.name}</IgrSelectItem>
              ))}
            </IgrSelect>
          </div>
          <div className="clock" aria-label={`Operations time ${hm(now)} EST, ${longDate(s.today)}`}>
            <strong>{hm(now)}</strong>
            <span>EST · {longDate(s.today)}</span>
          </div>
          <span
            className={`conn ${conn.online ? 'conn-ok' : 'conn-down'}`}
            role="status"
            title={conn.online ? 'Connected to dispatch service' : conn.lastError ?? 'Disconnected'}
          >
            <Icon name={conn.online ? 'dot' : 'cloud_off'} />
            {conn.online ? 'Live' : 'Disconnected'}
          </span>
          <div className="user" title={AUTHORITY_NOTE[s.user!.role]}>
            <Icon name="person" />
            <IgrSelect
              className="user-select"
              aria-label="Signed in as"
              value={String(s.user!.id)}
              onChange={e => s.setUserId(Number(e.detail.value))}
            >
              {roles.map(r => (
                <IgrSelectGroup key={r}>
                  <IgrSelectHeader slot="label">{ROLE_LABEL[r]}</IgrSelectHeader>
                  {s.dispatchers.filter(d => d.role === r).map(d => (
                    <IgrSelectItem key={d.id} value={String(d.id)}>
                      {d.fullName} · {ROLE_LABEL[d.role]}
                    </IgrSelectItem>
                  ))}
                </IgrSelectGroup>
              ))}
            </IgrSelect>
          </div>
        </div>
      </IgrNavbar>

      {!conn.online ? (
        <div className="conn-banner" role="alert">
          <Icon name="cloud_off" />
          <span>
            <strong>Disconnected.</strong> Showing the last data received; it may be out of date and changes cannot be
            saved. Retrying automatically every 30 s.
          </span>
          <IgrButton variant="outlined" onClick={() => navigate(0)}>Reload now</IgrButton>
        </div>
      ) : null}

      <main id="main" className="main" tabIndex={-1}>
        <Outlet />
      </main>
      <OutcomeTray />
    </div>
  );
}
