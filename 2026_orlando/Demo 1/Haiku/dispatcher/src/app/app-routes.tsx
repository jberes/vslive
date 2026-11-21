import { lazy, Suspense } from 'react';
import type { RouteObject } from 'react-router-dom';

// Route-level code splitting: each journey loads its own chunk (ApexCharts only on SLA).
const OperationsLedger = lazy(() => import('../pages/OperationsLedger'));
const Schedule = lazy(() => import('../pages/Schedule'));
const SlaRisk = lazy(() => import('../pages/SlaRisk'));
const UrgentFault = lazy(() => import('../pages/UrgentFault'));
const Handover = lazy(() => import('../pages/Handover'));

const wrap = (el: React.ReactNode) => <Suspense fallback={<p className="muted" role="status">読み込み中…</p>}>{el}</Suspense>;

export const routes: RouteObject[] = [
  { path: '/', element: wrap(<OperationsLedger />) },
  { path: '/jobs/:jobId', element: wrap(<OperationsLedger />) },
  { path: '/schedule', element: wrap(<Schedule />) },
  { path: '/sla', element: wrap(<SlaRisk />) },
  { path: '/urgent', element: wrap(<UrgentFault />) },
  { path: '/handover', element: wrap(<Handover />) },
];
