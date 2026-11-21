// Approval requests raised when a dispatcher hits a blocking validation issue they are not authorized to
// override. The API has no approvals endpoint, so each request is recorded as a job note (audit trail) and
// kept here so the shift handover can list it as a pending approval.
export interface ApprovalRequest {
  id: string;
  jobId: number;
  jobNumber: string;
  summary: string;
  action: string;
  reason: string;
  requestedBy: string;
  requestedAt: string;
  status: 'Pending' | 'Approved' | 'Declined';
}

const KEY = 'railops.approvals';

export function listApprovals(): ApprovalRequest[] {
  try {
    return JSON.parse(localStorage.getItem(KEY) ?? '[]') as ApprovalRequest[];
  } catch {
    return [];
  }
}
export function addApproval(a: Omit<ApprovalRequest, 'id' | 'status'>) {
  const all = listApprovals();
  all.unshift({ ...a, id: `${a.jobId}-${Date.now()}`, status: 'Pending' });
  try { localStorage.setItem(KEY, JSON.stringify(all.slice(0, 100))); } catch { /* ignore */ }
}
export function setApprovalStatus(id: string, status: ApprovalRequest['status']) {
  const all = listApprovals().map(a => (a.id === id ? { ...a, status } : a));
  try { localStorage.setItem(KEY, JSON.stringify(all)); } catch { /* ignore */ }
}
