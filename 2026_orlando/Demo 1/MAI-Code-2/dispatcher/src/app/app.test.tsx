import { beforeAll, expect, test } from 'vitest';
import { render } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import App from './app';
import { routes } from './app-routes';
import 'element-internals-polyfill';
import { setupTestMocks } from '../setupTests';

beforeAll(() => setupTestMocks());

test('renders the dispatcher shell', () => {
  const router = createMemoryRouter([{ element: <App />, children: routes }], { initialEntries: ['/ledger'] });
  const wrapper = render(<RouterProvider router={router} />);
  expect(wrapper.getByText('Infrastructure dispatch')).toBeTruthy();
});
