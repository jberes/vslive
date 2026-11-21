import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { IgrAvatar, IgrButton, IgrCard, IgrCardActions, IgrCardContent, IgrCardHeader, IgrCardMedia } from 'igniteui-react';
import { asset, practices, useWellness } from './wellness-data';
import { Icon, IconButton, Metric, Modal, SectionTitle, Select } from './ui';

function PracticeSession({ practice, onClose }: { practice: typeof practices[number]; onClose: () => void }) {
  const [running, setRunning] = useState(false);
  const [seconds, setSeconds] = useState(0);
  const [finished, setFinished] = useState(false);
  const duration = practice.duration * 60;
  useEffect(() => {
    if (!running || finished) return;
    const timer = window.setInterval(() => setSeconds(value => Math.min(value + 1, duration)), 1000);
    return () => window.clearInterval(timer);
  }, [running, finished, duration]);
  const complete = finished || seconds >= duration;
  return <Modal title={practice.name} onClose={onClose} footer={!complete && <IgrButton variant="flat" onClick={() => { setRunning(false); setFinished(true); }}>Finish session</IgrButton>}>
    <div className="session-panel"><img src={asset(practice.image)} alt={practice.name} /><p className="eyebrow">{practice.level} · {practice.duration} min</p><p>{complete ? 'Well done. You made time for yourself today.' : 'Find a comfortable seat. Breathe slowly, and let your shoulders soften.'}</p><div className="session-timer" role="timer">{Math.floor(seconds / 60)}:{String(seconds % 60).padStart(2, '0')}</div><progress value={seconds} max={duration} aria-label="Session progress" />{complete ? <p role="status">Demo session complete · {Math.floor(seconds / 60)} min {seconds % 60} sec practiced.</p> : <IgrButton variant="contained" onClick={() => setRunning(!running)}>{running ? 'Pause' : seconds ? 'Resume' : 'Start session'}</IgrButton>}</div>
  </Modal>;
}
export default function MentalWellness() {
  const [banner, setBanner] = useState(true);
  const [period, setPeriod] = useState('This week');
  const [level, setLevel] = useState('All');
  const [session, setSession] = useState<typeof practices[number] | null>(null);
  const { favorites, setFavorites } = useWellness();
  const navigate = useNavigate();
  const monthly = period === 'This month';
  return <div className="page mental-page">
    <h1 className="visually-hidden">Mental Wellness</h1>
    {banner && <div className="notice warning"><Icon name="info" /><span>Your device found elevated stress levels. Listening to calming music may help you relax.</span><div><IgrButton variant="flat" onClick={() => setBanner(false)}>Cancel</IgrButton><IgrButton variant="flat" onClick={() => navigate('/sleep#playlist')}>Open Playlist</IgrButton></div></div>}
    <SectionTitle title="Overview"><Select label="Period" value={period} options={['This week', 'This month']} onChange={setPeriod} /></SectionTitle>
    <div className="metrics four"><Metric title="Total Time" icon="clock" value={monthly ? '14' : '3'} unit={monthly ? 'hr' : 'hr 30 min'} tone="blue"><span>45 min Min · 4 hr Max</span><span>3 hr Avg</span></Metric><Metric title="Sessions" icon="yoga" value={monthly ? '12' : '3'} tone="purple"><span>5 Goal</span><span>3 Avg</span></Metric><Metric title="Breathing" icon="leaf" value="18" unit="BPM"><span>18 Goal</span><span>19 Avg</span></Metric><Metric title="Stress Level" icon="heart" value="7" unit="/ 10" tone="pink"><span>5 Goal</span><span>8 Avg</span></Metric></div>
    <section><SectionTitle title="Practice"><Select label="Level" value={level} options={['All', 'Beginner', 'Intermediate', 'Advanced']} onChange={setLevel} /></SectionTitle><div className="practice-grid">{practices.filter(practice => level === 'All' || practice.level === level).map(practice => <IgrCard key={practice.id} className="practice-card"><IgrCardMedia><img src={asset(practice.image)} alt={practice.name} /></IgrCardMedia><IgrCardHeader><IgrAvatar slot="thumbnail" src={asset(practice.teacher)} alt={`${practice.name} instructor`} shape="circle" /><h3 slot="title">{practice.name}</h3><span slot="subtitle">{practice.level}</span></IgrCardHeader><IgrCardContent><p>{practice.description}</p><span className="practice-duration"><Icon name="clock" />{practice.duration === 60 ? '1 hr' : `${practice.duration} min`}</span></IgrCardContent><IgrCardActions><IgrButton slot="start" variant="flat" onClick={() => setSession(practice)}>Join</IgrButton><span slot="end"><IconButton icon="heart" label={`${favorites.includes(practice.id) ? 'Unfavorite' : 'Favorite'} ${practice.name}`} active={favorites.includes(practice.id)} onClick={() => setFavorites(items => items.includes(practice.id) ? items.filter(id => id !== practice.id) : [...items, practice.id])} /></span></IgrCardActions></IgrCard>)}</div></section>
    {session && <PracticeSession practice={session} onClose={() => setSession(null)} />}
  </div>;
}
