import { useEffect, useRef } from 'react';
import { Outlet, useLocation, useNavigate } from 'react-router-dom';
import { IgrTab, IgrTabs, IgrSelect, IgrSelectItem, type IgrTabComponentEventArgs } from 'igniteui-react';
import { useConsole } from '../context/console';
import { ja } from '../i18n/ja';
import { fmtTime } from '../lib/format';
import { useQuery } from '../hooks/useQuery';
import { apiGet } from '../api/client';
import type { Region, Clock } from '../api/types';

const NAV = [
  { path: '/', label: ja.nav.ledger },
  { path: '/schedule', label: ja.nav.schedule },
  { path: '/sla', label: ja.nav.sla },
  { path: '/urgent', label: ja.nav.urgent },
  { path: '/handover', label: ja.nav.handover },
];

const roleLabel = (r: string) => (ja.role as Record<string, string>)[r] ?? r;

export default function AppShell() {
  const { pathname, search } = useLocation();
  const navigate = useNavigate();
  const c = useConsole();
  const regions = useQuery<Region[]>('regions', (s) => apiGet<{ items?: Region[] } | Region[]>('/api/regions', undefined, s).then((r) => (Array.isArray(r) ? r : (r.items ?? []))), 0);
  const clockNow = c.clock.data?.now;
  const clockAt = useRef<number>(Date.now());
  useEffect(() => { clockAt.current = Date.now(); }, [clockNow]);

  const selectedTab = NAV.find((n) => n.path === '/' ? pathname === '/' || pathname.startsWith('/jobs') : pathname.startsWith(n.path))?.path ?? '/';
  const disconnected = c.clock.disconnected;

  return (
    <div className="shell">
      <header className="shell-header">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">◈</span>
          <span className="brand-name">{ja.appName}</span>
        </div>
        <div className="context-bar" role="group" aria-label={ja.header.region}>
          <IgrSelect
            className="ctx-select"
            label={ja.header.region}
            value={c.regionId === null ? 'all' : String(c.regionId)}
            onChange={(e: CustomEvent<any>) => {
              const v = e.detail.value;
              c.setRegionId(v === 'all' ? null : Number(v));
            }}
          >
            <IgrSelectItem value="all">{ja.header.allRegions}</IgrSelectItem>
            {(regions.data ?? []).map((r) => (
              <IgrSelectItem key={r.id} value={String(r.id)}>{r.name}</IgrSelectItem>
            ))}
          </IgrSelect>
          <IgrSelect
            className="ctx-select"
            label={ja.header.dispatcher}
            value={c.dispatcher ? String(c.dispatcher.id) : ''}
            onChange={(e: CustomEvent<any>) => c.setDispatcherId(Number(e.detail.value))}
          >
            {c.dispatchers.map((d) => (
              <IgrSelectItem key={d.id} value={String(d.id)}>{`${d.fullName}（${roleLabel(d.role)}）`}</IgrSelectItem>
            ))}
          </IgrSelect>
        </div>
        <div className="clock" aria-live="off">
          <span className="clock-label">{ja.header.clock}</span>
          <time className="clock-value" dateTime={clockNow}>{clockNow ? fmtTime(clockNow) : '--:--'}</time>
          {disconnected
            ? <span className="pill pill-error">✖ {ja.header.disconnected}</span>
            : <span className="pill pill-ok">● {ja.header.live}</span>}
        </div>
      </header>
      <nav aria-label="Main">
        <IgrTabs
          className="nav-tabs"
          onChange={(e: IgrTabComponentEventArgs) => {
            const target = NAV.find((n) => n.label === e.detail.label);
            if (target && target.path !== selectedTab) navigate(target.path + (target.path === '/' ? '' : ''));
          }}
        >
          {NAV.map((n) => (
            <IgrTab key={n.path} label={n.label} selected={n.path === selectedTab} />
          ))}
        </IgrTabs>
      </nav>
      <main className="shell-main" id="main" data-search={search}>
        <Outlet />
      </main>
    </div>
  );
}

export type { Clock };
