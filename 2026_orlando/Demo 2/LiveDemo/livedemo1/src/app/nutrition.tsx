import { useState } from 'react';
import { IgrButton, IgrInput, IgrRating, IgrRatingSymbol } from 'igniteui-react';
import { asset, foodCalories, saveFood, useWellness } from './wellness-data';
import type { Food } from './wellness-data';
import { Icon, IconButton, MacroChart, Metric, Modal, SectionTitle, Select } from './ui';

function MealEditor({ food, onClose }: { food?: Food; onClose: () => void }) {
  const { setFoods } = useWellness();
  const [name, setName] = useState(food?.name ?? '');
  const [serving, setServing] = useState(food?.serving ?? '');
  const [calories, setCalories] = useState(food ? String(food.calories) : '');
  const [meal, setMeal] = useState(food?.meal ?? 'Breakfast');
  const [error, setError] = useState('');
  const save = () => {
    const number = Number(calories);
    if (!name.trim() || !serving.trim() || !calories.trim() || !Number.isInteger(number) || number <= 0 || number > 10000) {
      setError('Enter a food name, serving size, and whole-number calories between 1 and 10,000.');
      return;
    }
    setFoods(items => saveFood(items, { id: food?.id ?? crypto.randomUUID(), name: name.trim(), serving: serving.trim(), calories: number, meal, image: food?.image ?? '' }));
    onClose();
  };
  return <Modal title={food ? 'Edit meal' : 'Add meal'} onClose={onClose} footer={<IgrButton variant="contained" onClick={save}>Save meal</IgrButton>}><div className="form-fields"><IgrInput outlined label="Food name" value={name} onInput={e => setName(e.detail)} /><IgrInput outlined label="Serving size" placeholder="e.g. 1 bowl" value={serving} onInput={e => setServing(e.detail)} /><IgrInput outlined label="Calories (kcal)" type="number" min={1} max={10000} value={calories} onInput={e => setCalories(e.detail)} /><Select label="Meal" value={meal} options={['Breakfast', 'Lunch', 'Dinner', 'Snacks']} onChange={setMeal} />{error && <p role="alert" className="error">{error}</p>}</div></Modal>;
}

export default function Nutrition() {
  const { foods, water, setWater } = useWellness();
  const [search, setSearch] = useState('');
  const [editor, setEditor] = useState<Food | 'new' | null>(null);
  const [period, setPeriod] = useState('Today');
  const total = foodCalories(foods);
  const matches = foods.filter(food => food.name.toLowerCase().includes(search.toLowerCase()));
  return <div className="page nutrition-page">
    <h1 className="visually-hidden">Nutrition</h1>
    <SectionTitle title="Calories Remaining"><Select label="Period" value={period} options={['Today']} onChange={setPeriod} /></SectionTitle>
    <div className="calorie-equation"><Metric title="Goal" icon="food" value="1,500" unit="kcal" /><span className="operator">−</span><Metric title="Food" icon="food" value={total.toLocaleString()} unit="kcal" tone="pink" /><span className="operator">+</span><Metric title="Activity" icon="run" value="290" unit="kcal" /><span className="operator">=</span><Metric title="Remaining" icon="fire" value={(1500 - total + 290).toLocaleString()} unit="kcal" tone="blue" /></div>
    <div className="nutrition-columns"><section className="food-section"><h2 className="nutrition-section-heading">Food</h2><div className="food-toolbar"><IgrInput outlined label="Search food" value={search} onInput={e => setSearch(e.detail)}><span slot="prefix"><Icon name="search" /></span></IgrInput><IgrButton variant="outlined" onClick={() => setEditor('new')}><Icon name="add" />Add Meal</IgrButton></div>
      {['Breakfast', 'Lunch', 'Dinner', 'Snacks'].map(meal => {
        const group = matches.filter(food => food.meal === meal);
        return group.length > 0 && <section key={meal}><div className="meal-heading"><h2>{meal}</h2><span>{foodCalories(foods.filter(food => food.meal === meal))} <small>kcal</small></span></div>{group.map(food => <div className="food-row" key={food.id}>{food.image ? <img src={asset(food.image)} alt="" /> : <span className="food-placeholder"><Icon name="food" /></span>}<div className="row-description">{food.name}<small>{food.serving}</small></div><span className="calories">{food.calories}<small>kcal</small></span><IconButton icon="edit" label={`Edit ${food.name}`} onClick={() => setEditor(food)} /></div>)}</section>;
      })}
      {!matches.length && <p className="empty-state">No foods match “{search}”. Try another search or add a meal.</p>}
    </section>
      <aside className="nutrition-insights">
        <h2 className="nutrition-section-heading">Body Stats</h2>
        <section className="body-stats">
          <div><div className="body-stat-heading"><h3>Weight</h3></div><div className="stat-ring"><strong>68<small>kg</small></strong></div><p>Goal: 65 kg</p><span className="healthy-label">Normal</span></div>
          <div><div className="body-stat-heading"><h3>BMI</h3></div><div className="stat-ring bmi"><strong>23.5</strong></div><p>Healthy: 18.5–25</p><span className="healthy-label">Normal</span></div>
        </section>
        <section><SectionTitle title="Macros" /><MacroChart /></section>
        <section className="water-section">
          <SectionTitle title="Water Intake" />
          <div className="water-total"><strong>{water}</strong> / 10 cups</div>
          <IgrRating label="Water intake, each symbol is two cups" value={water / 2} max={5} step={0.5} valueFormat="{0} of {1} two-cup servings" onChange={e => setWater(e.detail * 2)}>
            {Array.from({ length: 5 }, (_, i) => <IgrRatingSymbol slot="symbol" key={i}><Icon name="water-full" /><Icon name="water" slot="empty" /></IgrRatingSymbol>)}
          </IgrRating>
          <p className="muted">Goal: 2.2 l <span>· {Math.round(water * 220)} ml today</span></p>
        </section>
      </aside>
    </div>
    {editor && <MealEditor food={editor === 'new' ? undefined : editor} onClose={() => setEditor(null)} />}
  </div>;
}
