// Thin fetch wrapper for the DispatchApi. Errors are RFC 7807 problem details;
// a 409 carries validation issues in `data`.
export const API_BASE: string = import.meta.env.VITE_API_BASE ?? 'http://localhost:5207';

export interface ProblemDetails {
  status: number;
  title?: string;
  detail?: string;
  data?: { issues?: AssignmentIssue[] } & Record<string, unknown>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;
  constructor(problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `HTTP ${problem.status}`);
    this.status = problem.status;
    this.problem = problem;
  }
}

export interface AssignmentIssue {
  code: string;
  severity: 'Blocking' | 'Warning' | string;
  message: string;
}

export async function apiGet<T>(path: string, params?: Record<string, string | number | undefined | null>, signal?: AbortSignal): Promise<T> {
  const url = new URL(path, API_BASE);
  for (const [k, v] of Object.entries(params ?? {})) {
    if (v !== undefined && v !== null && v !== '') url.searchParams.set(k, String(v));
  }
  const res = await fetch(url, { signal, headers: { Accept: 'application/json' } });
  return parse<T>(res);
}

export async function apiSend<T>(method: 'POST' | 'PATCH' | 'PUT' | 'DELETE', path: string, body?: unknown): Promise<T> {
  const res = await fetch(new URL(path, API_BASE), {
    method,
    headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  return parse<T>(res);
}

async function parse<T>(res: Response): Promise<T> {
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  const json = text ? JSON.parse(text) : undefined;
  if (!res.ok) {
    throw new ApiError({ status: res.status, ...(json ?? {}) });
  }
  return json as T;
}
