import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createBrowserRouter, RouterProvider } from 'react-router-dom';
import App from './app/app';
import { routes } from './app/app-routes';
import { registerIcons } from './components/Icon';

// Theme order: Fluent base theme → MCP-generated palette → app layout styles.
import 'igniteui-webcomponents/themes/light/fluent.css';
import './styles/theme.css';
import './styles/app.css';


registerIcons();

const basename = import.meta.env.VITE_BASENAME || '/';

const router = createBrowserRouter([{ element: <App />, children: [...routes] }], { basename });

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <RouterProvider router={router} />
  </StrictMode>,
);
