import { lazy, Suspense, type ReactNode } from 'react';
import { Navigate, type RouteObject } from 'react-router-dom';
import { LoadingBlock } from '../components/States';
import NotFound from '../routes/NotFound';

// Route-level code splitting: chart-heavy views (ApexCharts) load only when visited.
const LedgerPage = lazy(() => import('../routes/ledger/LedgerPage'));
const JobPage = lazy(() => import('../routes/jobs/JobPage'));
const SlaPage = lazy(() => import('../routes/sla/SlaPage'));
const PlannerPage = lazy(() => import('../routes/planner/PlannerPage'));
const CorridorPage = lazy(() => import('../routes/corridor/CorridorPage'));
const IncidentPage = lazy(() => import('../routes/incident/IncidentPage'));
const HandoverPage = lazy(() => import('../routes/handover/HandoverPage'));

const page = (el: ReactNode) => <Suspense fallback={<LoadingBlock label="Loading view…" />}>{el}</Suspense>;

export const routes: RouteObject[] = [
  { path: '/', element: <Navigate to="/ledger" replace /> },
  { path: '/ledger', element: page(<LedgerPage />) },
  { path: '/jobs/:id', element: page(<JobPage />) },
  { path: '/sla', element: page(<SlaPage />) },
  { path: '/planner', element: page(<PlannerPage />) },
  { path: '/corridor', element: page(<CorridorPage />) },
  { path: '/incident/new', element: page(<IncidentPage />) },
  { path: '/handover', element: page(<HandoverPage />) },
  { path: '*', element: <NotFound /> },
];
