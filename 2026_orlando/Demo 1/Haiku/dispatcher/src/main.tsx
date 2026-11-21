import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
// Theme first: Fluent base, then the Operations Ledger palette (primary #155dfc).
import 'igniteui-webcomponents/themes/light/fluent.css';
import './styles/theme.css';
import '../styles.css';
import { ConsoleProvider } from './context/console';
import { routes } from './app/app-routes';
import AppShell from './components/AppShell';

const router = createBrowserRouter([
  {
    element: <AppShell />,
    children: routes,
  },
], { basename: import.meta.env.VITE_BASENAME || '/' });

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ConsoleProvider>
      <RouterProvider router={router} />
    </ConsoleProvider>
  </StrictMode>,
);
