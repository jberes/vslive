import { afterEach, beforeAll, expect, test } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import App from './app';
import Nutrition from './nutrition';
import Activity from './activity';
import { foodCalories, initialFoods, saveFood } from './wellness-data';
import 'element-internals-polyfill';
import { setupTestMocks } from '../setupTests';
import 'igniteui-webcomponents/themes/light/material.css';
import '../../styles.css';

beforeAll(setupTestMocks);
afterEach(cleanup);

function renderPage(page: 'nutrition' | 'activity') {
  return render(<MemoryRouter initialEntries={[`/${page}`]}><Routes><Route element={<App />}><Route path="nutrition" element={<Nutrition />} /><Route path="activity" element={<Activity />} /></Route></Routes></MemoryRouter>);
}

test('adding and editing foods updates totals without duplicating entries', () => {
  const added = saveFood(initialFoods, { id: 'soup', name: 'Soup', serving: '1 bowl', calories: 200, meal: 'Dinner', image: '' });
  expect(foodCalories(added)).toBe(1115);
  const edited = saveFood(added, { ...added[added.length - 1], calories: 300 });
  expect(foodCalories(edited)).toBe(1215);
  expect(edited).toHaveLength(initialFoods.length + 1);
  expect(foodCalories(initialFoods)).toBe(915);
});

test('meal dialog validates input and updates food and remaining calories', async () => {
  const { container } = renderPage('nutrition');
  fireEvent.click(screen.getByText('Add Meal'));
  await waitFor(() => expect(container.querySelector('igc-dialog')).not.toBeNull());
  fireEvent.click(screen.getByText('Save meal'));
  expect((await screen.findByRole('alert')).textContent).toContain('Enter a food name');
  for (const [label, value] of [['Food name', 'Tomato soup'], ['Serving size', '1 bowl'], ['Calories (kcal)', '200']]) {
    const input = [...container.querySelectorAll('igc-input')].find(input => input.label === label);
    if (!input) throw new Error(`Missing input ${label}`);
    fireEvent(input, new CustomEvent('igcInput', { detail: value, bubbles: true }));
  }
  fireEvent.click(screen.getByText('Save meal'));
  await screen.findByText('Tomato soup');
  const metrics = container.querySelectorAll('.metric-value');
  expect(metrics[1].textContent).toBe('1,115kcal');
  expect(metrics[3].textContent).toBe('675kcal');
});

test('activity filtering changes the visible activities and selected details', async () => {
  const { container } = renderPage('activity');
  const tabs = container.querySelector('igc-tabs');
  const groupTab = [...container.querySelectorAll('igc-tab')].find(tab => tab.label === 'In a group');
  if (!tabs || !groupTab) throw new Error('Missing activity tabs');
  fireEvent(tabs, new CustomEvent('igcChange', { detail: groupTab, bubbles: true }));
  await waitFor(() => expect(container.querySelectorAll('.activity-row')).toHaveLength(4));
  expect(container.querySelector('.activity-detail')?.getAttribute('aria-label')).toBe('Pilates details');
  expect(container.querySelector('.route-stats')?.textContent).toContain('300 kcal');
});
