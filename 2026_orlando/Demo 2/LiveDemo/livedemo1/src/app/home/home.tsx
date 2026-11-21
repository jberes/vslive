import { useState } from 'react';
import { IgrCalendar, IgrList, IgrListItem } from 'igniteui-react';
import { asset, foodCalories, useWellness } from '../wellness-data';
import { ActivityChart, Icon, MacroChart, Metric, SectionTitle, Select } from '../ui';

const schedule = [
  ['run', 'Morning Run', '7:00–7:40 | 1.3 km'],
  ['bike', 'Cycling', '12:00–12:30 | 3.2 km'],
  ['yoga', 'Pilates', '17:00–18:00 | Yoga Studio'],
  ['fitness', 'Fitness', '20:00–21:15 | Gama Sport Center'],
];
export default function Home() {
  const [date, setDate] = useState(new Date(2025, 4, 10));
  const [period, setPeriod] = useState('This week');
  const [macroPeriod, setMacroPeriod] = useState('Today');
  const { foods } = useWellness();
  const isDemoDate = date.getDate() === 10 && date.getMonth() === 4 && date.getFullYear() === 2025;
  const dailyActivities = isDemoDate ? schedule : schedule.filter((_, index) => (date.getDate() + index) % 2 === 0);
  return <div className="page home-page">
    <h1 className="visually-hidden">Home dashboard</h1>
    <section className="home-overview">
      <div className="greeting"><img src={asset('alice.png')} alt="Alice" /><div><p className="hello">Hello, Alice!</p><p>Ready for today’s challenges?</p></div></div>
      <div className="metrics four"><Metric title="Steps" icon="run" value="3,200" /><Metric title="Calories burned" icon="fire" value="700" unit="kcal" tone="pink" /><Metric title="Heart Rate" icon="heart" value="72" unit="BPM" tone="red" /><Metric title="Sleep" icon="moon" value="5" unit="hr 30 min" tone="blue" /></div>
    </section>
    <div className="home-columns">
      <section className="calendar-column"><SectionTitle title="Your Active Days" /><IgrCalendar value={date} activeDate={date} headerOrientation="horizontal" showWeekNumbers onChange={event => { if (event.detail instanceof Date) setDate(event.detail); }} />
        <h3 className="schedule-heading">{isDemoDate ? 'Today' : date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })} - Activities</h3>
        <IgrList>{dailyActivities.map(([icon, name, detail]) => <IgrListItem key={name}><span slot="start" className="activity-avatar"><Icon name={icon} /></span><span slot="title">{name}</span><span slot="subtitle">{detail}</span></IgrListItem>)}</IgrList>
      </section>
      <div className="home-charts"><section><SectionTitle title="Activity"><Select label="Period" value={period} options={['This week', 'This month']} onChange={setPeriod} /></SectionTitle><ActivityChart period={period} /></section>
        <section><SectionTitle title="Macros"><Select label="Period" value={macroPeriod} options={['Today', 'This week']} onChange={setMacroPeriod} /></SectionTitle><div className="macro-overview"><MacroChart period={macroPeriod} /><div className="macro-details">{[['Protein', '120 g', '120.55 g', 'purple'], ['Carbs', '220 g', '230.87 g', 'green'], ['Fat', '80 g', '90.30 g', 'blue'], ['Calories', '1500 kcal', `${foodCalories(foods)} kcal`, 'pink']].map(([name, goal, average, tone]) => <div className="macro-row" key={name}><span className={`round-icon tone-${tone}`}><Icon name={name === 'Calories' ? 'food' : 'leaf'} /></span><div>{name}<small>{goal} Goal</small></div><div className="macro-average"><strong>{average}</strong><small>{macroPeriod === 'Today' ? 'Today’s intake' : 'Avg intake'}</small></div><Icon name="trend" className="trend" /></div>)}</div></div></section>
      </div>
    </div>
  </div>;
}
