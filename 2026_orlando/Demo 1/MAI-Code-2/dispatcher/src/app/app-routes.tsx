import { Navigate } from 'react-router-dom';
import { Corridor, Handover, Ledger, Planner } from './pages';

export const routes = [
  { path: '/', element: <Navigate to="/ledger" replace /> },
  { path: '/ledger', element: <Ledger /> },
  { path: '/corridor', element: <Corridor /> },
  { path: '/planner', element: <Planner /> },
  { path: '/handover', element: <Handover /> },
  { path: '*', element: <Navigate to="/ledger" replace /> },
];
