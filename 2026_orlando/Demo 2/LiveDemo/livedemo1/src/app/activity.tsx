import { useState } from 'react';
import { IgrButton, IgrTab, IgrTabs } from 'igniteui-react';
import { activities, asset } from './wellness-data';
import { AreaChart, Icon, Metric, SectionTitle, Select } from './ui';

export default function Activity() {
  const [banner, setBanner] = useState(true);
  const [period, setPeriod] = useState('This week');
  const [selected, setSelected] = useState(activities[0]);
  const [filter, setFilter] = useState('All');
  const filtered = activities.filter(item => filter === 'All' || (filter === 'In a group' ? item.group : !item.group));
  const exportActivities = () => {
    const csv = ['Date,Activity,Time,Location,Calories', ...filtered.map(item => [item.date, item.name, item.time, item.location, item.calories].join(','))].join('\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url; link.download = 'healthy-activities.csv'; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  };
  const changeFilter = (value: string) => {
    setFilter(value);
    const first = activities.find(item => value === 'All' || (value === 'In a group' ? item.group : !item.group));
    if (first) setSelected(first);
  };
  const monthly = period === 'This month';
  return <div className="page">
    <h1 className="visually-hidden">Activity</h1>
    {banner && <div className="notice info"><Icon name="info" /><span>Your running time increased by 20% this week!</span><IgrButton variant="flat" onClick={() => setBanner(false)}>Close</IgrButton></div>}
    <SectionTitle title="Overview"><Select label="Period" value={period} options={['This week', 'This month']} onChange={setPeriod} /></SectionTitle>
    <div className="metrics four activity-metrics">
      <Metric title="Steps" icon="run" value={monthly ? '164,040' : '41,010'}><span>8,000 Daily goal</span><span>5,858 Avg</span></Metric>
      <Metric title="Running" icon="run" value={monthly ? '15' : '3'} unit={monthly ? 'hr 12 min' : 'hr 48 min'}><span>2 hr 15 min Avg</span></Metric>
      <Metric title="Fitness" icon="fitness" value={monthly ? '17' : '4'} unit={monthly ? 'hr' : 'hr 15 min'}><span>1 hr Avg</span></Metric>
      <Metric title="Yoga / Pilates" icon="yoga" value={monthly ? '12' : '3'} unit="hr"><span>1 hr 10 min Avg</span></Metric>
      <Metric title="Cycling" icon="bike" value={monthly ? '8' : '2'} unit={monthly ? 'hr 40 min' : 'hr 10 min'}><span>40 min Avg</span></Metric>
      <Metric title="Heart Rate" icon="heart" value="72" unit="BPM" tone="red"><span>62 Min · 124 Max</span><span>75 Avg</span></Metric>
      <Metric title="Elevation" icon="mountain" value={monthly ? '1,120' : '280'} unit="m" tone="purple"><span>300 m Avg</span></Metric>
      <Metric title="Distance" icon="stats" value={monthly ? '130.24' : '32.56'} unit="km" tone="blue"><span>4.7 km Avg</span></Metric>
    </div>
    <SectionTitle title="Your Activities"><IgrButton variant="flat" onClick={exportActivities}><Icon name="download" />Export</IgrButton></SectionTitle>
    <div className="activity-columns">
      <section className="activity-list"><IgrTabs onChange={event => changeFilter(event.detail.label)}>{['All', 'Alone', 'In a group'].map(label => <IgrTab key={label} label={label} />)}</IgrTabs>
        {['22 May', '24 May', 'Today'].map(day => <div key={day}>{filtered.some(item => item.date === day) && <h3 className="date-heading">{day}</h3>}{filtered.filter(item => item.date === day).map(item => <button key={item.id} className={`activity-row ${selected.id === item.id ? 'selected' : ''}`} onClick={() => setSelected(item)} aria-pressed={selected.id === item.id}><span className="activity-avatar"><Icon name={item.icon} /></span><span className="row-description">{item.name}<small>{item.time} | {item.location}</small></span><span className="calories">{item.calories}<small>kcal</small></span></button>)}</div>)}
      </section>
      <section className="activity-detail" aria-label={`${selected.name} details`}>
        <h2>Details</h2>
        <img className="route-map" src={asset('activity-route.png')} alt="Illustrated running route through a city park" />
        <div className="route-stats">{[
          ['Steps', selected.steps.toLocaleString(), 'run'],
          ['Distance', `${selected.distance} km`, 'stats'],
          ['Duration', `${selected.duration} min`, 'clock'],
          ['Pace', `${(selected.distance / (selected.duration / 60)).toFixed(1)} km/h`, 'trend'],
          ['Calories', `${selected.calories} kcal`, 'fire'],
          ['Avg Heart Rate', `${selected.heart} BPM`, 'heart'],
        ].map(([name, value, icon]) => <div key={name}><Icon name={icon} /><span>{name}<strong>{value}</strong></span></div>)}</div>
        <h3>Heart Rate</h3>
        <AreaChart kind="heart" multiplier={selected.heart / 113} />
      </section>
    </div>
  </div>;
}
