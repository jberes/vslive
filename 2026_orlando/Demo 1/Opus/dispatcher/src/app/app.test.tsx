import { describe, expect, test } from 'vitest';
import type { JobListItem } from '../api/types';
import { permissionsFor } from '../lib/permissions';
import { impactCompare, nextAction, riskCause } from '../lib/status';

const job = (over: Omit<Partial<JobListItem>, 'sla'> & { sla?: Partial<JobListItem['sla']> } = {}): JobListItem => ({
  id: 1, jobNumber: 'WO-1', status: 'Assigned', priority: 'Medium', summary: 'Fault', customerId: 1, customerName: 'Line',
  customerTier: 'Gold', siteId: 1, siteName: 'Station', siteCity: 'City', siteLatitude: 0, siteLongitude: 0, regionId: 1,
  regionName: 'R', jobTypeId: 1, jobTypeName: 'T', jobCategory: 'Repair', requiredSkillId: 1, requiredSkillName: 'Signals',
  windowStart: '2026-11-17T08:00:00', windowEnd: '2026-11-17T10:00:00', scheduledStart: '2026-11-17T09:00:00',
  scheduledEnd: '2026-11-17T10:00:00', estimatedDurationMinutes: 60, responseDueAt: '2026-11-17T10:00:00',
  resolutionDueAt: '2026-11-18T10:00:00', assignedTechnicianId: 2, assignedTechnicianName: 'Crew', actualStart: null,
  actualEnd: null, rescheduleCount: 0, createdAt: '2026-11-17T06:00:00', updatedAt: '2026-11-17T06:00:00',
  ...over,
  sla: { state: 'OnTrack', stage: 'Response', dueAt: '2026-11-17T10:00:00', minutesRemaining: 120, projectedAt: null, reason: null, responseMet: null, resolutionMet: null, ...over.sla },
});

describe('impact ordering', () => {
  test('critical before high, breached before on-track, less time first', () => {
    const a = job({ id: 1, priority: 'High', sla: { state: 'Breached', minutesRemaining: -10 } });
    const b = job({ id: 2, priority: 'Critical', sla: { state: 'OnTrack', minutesRemaining: 300 } });
    const c = job({ id: 3, priority: 'Critical', sla: { state: 'Breached', minutesRemaining: -5 } });
    const d = job({ id: 4, priority: 'Critical', sla: { state: 'Breached', minutesRemaining: -50 } });
    expect([a, b, c, d].sort(impactCompare).map(j => j.id)).toEqual([4, 3, 2, 1]);
  });
});

describe('risk explanation', () => {
  test('combines SLA reason, ownership and remaining work', () => {
    const j = job({ status: 'Unassigned', assignedTechnicianName: null, scheduledStart: null,
      sla: { state: 'AtRisk', stage: 'Resolution', minutesRemaining: 30, reason: 'Projected late' }, estimatedDurationMinutes: 90 });
    expect(riskCause(j)).toBe('Projected late · no crew assigned · work needs 90 min, 30 min left');
    expect(nextAction(j)).toBe('Assign a qualified crew');
  });
});

describe('role permissions', () => {
  test('dispatchers cannot set or clear Critical; supervisors can override', () => {
    expect(permissionsFor('Dispatcher').canSetPriority('High', 'Critical')).toBe(false);
    expect(permissionsFor('Dispatcher').canSetPriority('Critical', 'High')).toBe(false);
    expect(permissionsFor('Dispatcher').canSetPriority('Medium', 'High')).toBe(true);
    expect(permissionsFor('SeniorDispatcher').canSetPriority('High', 'Critical')).toBe(true);
    expect(permissionsFor('Dispatcher').canOverride).toBe(false);
    expect(permissionsFor('Supervisor').canOverride).toBe(true);
  });
});
