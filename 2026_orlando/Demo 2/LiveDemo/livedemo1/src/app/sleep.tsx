import { useEffect, useRef, useState } from 'react';
import { IgrList, IgrListItem } from 'igniteui-react';
import { asset } from './wellness-data';
import { AreaChart, IconButton, Metric, SectionTitle, Select } from './ui';

const tracks = [
  { title: 'Track 1', subtitle: 'Still Waters', image: 'track-water.png', frequency: 174 },
  { title: 'Track 2', subtitle: 'Forest Whispers', image: 'track-leaves.png', frequency: 196 },
  { title: 'Track 3', subtitle: 'Northern Lights', image: 'track-aurora.png', frequency: 220 },
  { title: 'Track 4', subtitle: 'Evening Rain', image: 'track-rain.png', frequency: 146.83 },
];

export default function Sleep() {
  const [period, setPeriod] = useState('Today');
  const [playing, setPlaying] = useState<number | null>(null);
  const [error, setError] = useState('');
  const [elapsed, setElapsed] = useState(0);
  const audio = useRef<AudioContext | null>(null);
  const request = useRef(0);
  useEffect(() => () => {
    request.current++;
    if (audio.current) void audio.current.close().catch(error => console.error('Unable to close relaxation audio:', error));
  }, []);
  useEffect(() => {
    if (playing === null) return;
    const timer = window.setInterval(() => setElapsed(value => value + 1), 1000);
    return () => window.clearInterval(timer);
  }, [playing]);
  const toggle = async (index: number) => {
    const currentRequest = ++request.current;
    const previous = playing;
    setPlaying(null);
    setError('');
    try {
      if (audio.current) {
        const old = audio.current;
        audio.current = null;
        await old.close();
      }
      if (previous === index || currentRequest !== request.current) return;
      const context = new AudioContext();
      audio.current = context;
      const master = context.createGain();
      master.gain.value = 0.035;
      master.connect(context.destination);
      [1, 1.5, 2].forEach((ratio, harmonic) => {
        const oscillator = context.createOscillator();
        const gain = context.createGain();
        oscillator.type = 'sine';
        oscillator.frequency.value = tracks[index].frequency * ratio;
        gain.gain.value = 1 / (harmonic + 1);
        oscillator.connect(gain); gain.connect(master); oscillator.start();
      });
      await context.resume();
      if (currentRequest !== request.current) {
        if (context.state !== 'closed') await context.close();
        return;
      }
      setElapsed(0);
      setPlaying(index);
    } catch (error) {
      console.error('Relaxation audio playback failed:', error);
      setError('Audio could not start. Check your browser’s audio permissions and try again.');
      if (audio.current && audio.current.state !== 'closed') {
        void audio.current.close().catch(closeError => console.error('Unable to close failed audio:', closeError));
      }
      audio.current = null;
    }
  };
  const weekly = period === 'This week';
  return <div className="page sleep-page">
    <h1 className="visually-hidden">Sleep</h1>
    <SectionTitle title="Sleep Analysis"><Select label="Period" value={period} options={['Today', 'This week']} onChange={setPeriod} /></SectionTitle>
    <div className="metrics four"><Metric title="Sleep" icon="moon" value={weekly ? '6' : '5'} unit={weekly ? 'hr 40 min' : 'hr 30 min'} tone="blue"><span>8 hr Goal</span><span>6 hr 40 min Avg</span></Metric><Metric title="Deep Sleep" icon="moon" value={weekly ? '2' : '2'} unit={weekly ? 'hr 15 min' : 'hr 48 min'} tone="purple"><span>3 hr Goal</span><span>2 hr 15 min Avg</span></Metric><Metric title="Sleep Latency" icon="clock" value={weekly ? '18' : '15'} unit="min" tone="blue"><span>10 min Min · 25 min Max</span><span>18 min Avg</span></Metric><Metric title="Sleep Score" icon="stats" value={weekly ? '88' : '90'}><span>62 Min · 93 Max</span><span>88 Avg</span></Metric></div>
    <h2 className="chart-heading">Sleep</h2>
    <AreaChart kind="sleep" multiplier={weekly ? 0.85 : 1} />
    <section id="playlist"><SectionTitle title="Relaxing Sleep Melody" /><IgrList className="track-list">{tracks.map((track, index) => <IgrListItem key={track.title}><img className="track-cover" slot="start" src={asset(track.image)} alt="" /><span slot="title">{track.title}</span><span slot="subtitle">{track.subtitle} · Ambient studio</span><div slot="end" className="track-actions">{playing === index && <span className="playback-time" role="status">{Math.floor(elapsed / 60)}:{String(elapsed % 60).padStart(2, '0')}</span>}<IconButton icon={playing === index ? 'pause' : 'play'} label={`${playing === index ? 'Pause' : 'Play'} ${track.title}`} onClick={() => void toggle(index)} active={playing === index} /></div></IgrListItem>)}</IgrList>{error && <p className="error" role="alert">{error}</p>}</section>
  </div>;
}
