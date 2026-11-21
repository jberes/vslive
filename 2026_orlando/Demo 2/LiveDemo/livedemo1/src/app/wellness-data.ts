import { createContext, useContext } from 'react';

export const asset = (name: string) => `${import.meta.env.BASE_URL}images/${name}`;
export const navigation = [
  { path: '/', label: 'Home', icon: 'home' },
  { path: '/activity', label: 'Activity', icon: 'activity' },
  { path: '/nutrition', label: 'Nutrition', icon: 'food' },
  { path: '/sleep', label: 'Sleep', icon: 'moon' },
  { path: '/mental-wellness', label: 'Mental Wellness', icon: 'leaf' },
];

export interface Food {
  id: string;
  name: string;
  serving: string;
  calories: number;
  meal: string;
  image: string;
}

export const initialFoods: Food[] = [
  { id: 'eggs', name: 'Eggs Organic', serving: '2 large', calories: 140, meal: 'Breakfast', image: 'eggs.png' },
  { id: 'sausage', name: 'Breakfast Sausage', serving: '100 g', calories: 150, meal: 'Breakfast', image: 'sausage.png' },
  { id: 'coffee', name: 'Coffee w/ Creamer', serving: '12 oz', calories: 60, meal: 'Breakfast', image: 'coffee.png' },
  { id: 'chicken', name: 'Chicken Fillet', serving: '180 g', calories: 300, meal: 'Lunch', image: 'chicken.png' },
  { id: 'potatoes', name: 'Red Potatoes Baked', serving: '85 g', calories: 59, meal: 'Lunch', image: 'potatoes.png' },
  { id: 'salad', name: 'Green Salad Mix', serving: '53 g', calories: 70, meal: 'Lunch', image: 'salad.png' },
  { id: 'yogurt', name: 'Plum and Greek Yogurt Snack', serving: '180 g', calories: 136, meal: 'Lunch', image: 'yogurt.png' },
];

export function foodCalories(foods: Food[]) {
  return foods.reduce((sum, food) => sum + food.calories, 0);
}

export function saveFood(foods: Food[], food: Food) {
  return foods.some(item => item.id === food.id)
    ? foods.map(item => item.id === food.id ? food : item)
    : [...foods, food];
}

export interface WellnessState {
  foods: Food[];
  setFoods: React.Dispatch<React.SetStateAction<Food[]>>;
  water: number;
  setWater: (value: number) => void;
  favorites: number[];
  setFavorites: React.Dispatch<React.SetStateAction<number[]>>;
}

export const WellnessContext = createContext<WellnessState | null>(null);
export function useWellness() {
  const context = useContext(WellnessContext);
  if (!context) throw new Error('Wellness pages require WellnessContext.');
  return context;
}

export const activities = [
  { id: 1, date: '22 May', name: 'Running', time: '12:00–12:30', location: '1.5 km', calories: 90, group: false, icon: 'run', steps: 2239, distance: 1.5, duration: 30, heart: 113 },
  { id: 2, date: '22 May', name: 'Pilates', time: '17:00–18:00', location: 'Yoga Studio', calories: 300, group: true, icon: 'yoga', steps: 420, distance: 0.3, duration: 60, heart: 92 },
  { id: 3, date: '24 May', name: 'Cycling', time: '12:00–13:30', location: '3.4 km', calories: 310, group: false, icon: 'bike', steps: 890, distance: 3.4, duration: 90, heart: 124 },
  { id: 4, date: '24 May', name: 'Running', time: '18:00–19:00', location: '2 km', calories: 120, group: false, icon: 'run', steps: 3100, distance: 2, duration: 60, heart: 118 },
  { id: 5, date: '24 May', name: 'Fitness', time: '21:00–21:30', location: 'Sports Center', calories: 380, group: true, icon: 'fitness', steps: 1250, distance: 0.8, duration: 30, heart: 130 },
  { id: 6, date: 'Today', name: 'Running', time: '9:00–10:30', location: '2.1 km', calories: 126, group: false, icon: 'run', steps: 3300, distance: 2.1, duration: 90, heart: 115 },
  { id: 7, date: 'Today', name: 'Yoga', time: '12:00–13:00', location: 'Yoga Studio', calories: 274, group: true, icon: 'yoga', steps: 340, distance: 0.2, duration: 60, heart: 86 },
  { id: 8, date: 'Today', name: 'Pilates', time: '17:00–18:00', location: 'Yoga Studio', calories: 300, group: true, icon: 'yoga', steps: 460, distance: 0.3, duration: 60, heart: 94 },
];

export const practices = [
  { id: 1, name: 'Morning Meditation', level: 'Beginner', duration: 60, image: 'morning-meditation.png', teacher: 'teacher-morning.png', description: 'Start your day with a clear mind and a positive outlook.' },
  { id: 2, name: 'Guided Meditation', level: 'Beginner', duration: 30, image: 'guided-meditation.png', teacher: 'teacher-guided.png', description: 'Find your calm with a gentle, guided journey inward.' },
  { id: 3, name: 'Stress Meditation', level: 'Advanced', duration: 60, image: 'stress-meditation.png', teacher: 'teacher-stress.png', description: 'Let go of tension and create space for a peaceful mind.' },
  { id: 4, name: 'Breathing Exercises', level: 'Intermediate', duration: 15, image: 'breathing.png', teacher: 'teacher-guided.png', description: 'Reconnect with your breath and restore your natural balance.' },
  { id: 5, name: 'Mindfulness', level: 'Beginner', duration: 60, image: 'mindfulness.png', teacher: 'teacher-mindfulness.png', description: 'Slow down, be present, and appreciate the little things.' },
  { id: 6, name: 'Emotional Intelligence', level: 'Advanced', duration: 60, image: 'emotional-intelligence.png', teacher: 'teacher-emotional.png', description: 'Build a deeper understanding of yourself and your emotions.' },
];
