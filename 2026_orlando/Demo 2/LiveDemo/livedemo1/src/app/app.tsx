import { useState } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { IgrAvatar, IgrButton, IgrInput, IgrNavDrawer, IgrNavDrawerItem } from 'igniteui-react';
import { asset, initialFoods, navigation, WellnessContext } from './wellness-data';
import { Icon, IconButton, Modal } from './ui';

export default function App() {
  const [foods, setFoods] = useState(initialFoods);
  const [water, setWater] = useState(9);
  const [favorites, setFavorites] = useState([1, 2]);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [dialog, setDialog] = useState('');
  const [search, setSearch] = useState('');
  const [signedOut, setSignedOut] = useState(false);
  const navigate = useNavigate();
  return <WellnessContext value={{ foods, setFoods, water, setWater, favorites, setFavorites }}>
    <a className="skip-link" href="#main-content">Skip to content</a>
    <header className="topbar">
      <div className="mobile-menu"><IconButton icon="menu" label="Toggle navigation" onClick={() => setMobileOpen(!mobileOpen)} /></div>
      <NavLink to="/" className="brand" aria-label="Healthy home"><img src={asset('healthy-logo.svg')} alt="HEALTHY" /></NavLink>
      <div className="header-actions"><IconButton icon="search" label="Search pages" onClick={() => setDialog('Search')} /><IconButton icon="bell" label="Notifications" onClick={() => setDialog('Notifications')} /></div>
    </header>
    <div className={`app-layout ${mobileOpen ? 'menu-open' : ''}`}>
      {mobileOpen && <button className="nav-scrim" aria-label="Close navigation" onClick={() => setMobileOpen(false)} />}
      <aside className="sidebar"><nav aria-label="Main navigation"><IgrNavDrawer open position="relative">
        {navigation.map(item => <NavLink key={item.path} to={item.path} end={item.path === '/'} onClick={() => setMobileOpen(false)}>{({ isActive }) => <IgrNavDrawerItem active={isActive}><span slot="icon"><Icon name={item.icon} /></span><span slot="content">{item.label}</span></IgrNavDrawerItem>}</NavLink>)}
        {[['Statistics', 'stats'], ['Profile', 'profile'], ['Settings', 'settings'], ['Log out', 'lock']].map(([label, icon]) => <button className="nav-action" key={label} onClick={() => { setDialog(label); setMobileOpen(false); }}><IgrNavDrawerItem><span slot="icon"><Icon name={icon} /></span><span slot="content">{label}</span></IgrNavDrawerItem></button>)}
      </IgrNavDrawer></nav></aside>
      <main id="main-content" className="main-content"><Outlet /></main>
    </div>
    {dialog && <Modal title={dialog} onClose={() => { setDialog(''); setSignedOut(false); }}>
      {dialog === 'Search' && <><IgrInput outlined label="Find a page" value={search} onInput={e => setSearch(e.detail)} /><div className="search-results">{navigation.filter(item => item.label.toLowerCase().includes(search.toLowerCase())).map(item => <IgrButton key={item.path} variant="flat" onClick={() => { navigate(item.path); setDialog(''); }}>{item.label}</IgrButton>)}{!navigation.some(item => item.label.toLowerCase().includes(search.toLowerCase())) && <p>No pages found.</p>}</div></>}
      {dialog === 'Notifications' && <><p>Your running time increased by 20% this week.</p><p>You’re one cup away from your water goal. Keep going!</p><p className="muted">Demo notifications · today</p></>}
      {dialog === 'Profile' && <div className="profile-dialog"><IgrAvatar src={asset('alice.png')} alt="Alice" shape="circle" /><h2>Alice Morgan</h2><p>Your wellness journey, one day at a time.</p><p className="muted">Demo profile · no personal data is collected.</p></div>}
      {dialog === 'Statistics' && <><p>This week at a glance</p><div className="summary-stat"><strong>41,010</strong><span>steps</span><strong>3 hr 48 min</strong><span>running</span><strong>3</strong><span>mindfulness sessions</span></div><p className="muted">Explore each page for detailed demo insights.</p></>}
      {dialog === 'Settings' && <><p>This is an interactive demo using mock wellness data.</p><p>Changes are kept while you explore and reset when you refresh the browser. No account or device is connected.</p></>}
      {dialog === 'Log out' && <><p>{signedOut ? 'You’re signed out of the demo. No real account was connected.' : 'You’re exploring a demo profile. End this demo session?'}</p><IgrButton variant="outlined" onClick={() => { setSignedOut(true); setFoods(initialFoods); setWater(9); setFavorites([1, 2]); navigate('/'); }}>{signedOut ? 'Reset demo again' : 'End demo session'}</IgrButton></>}
    </Modal>}
  </WellnessContext>;
}
