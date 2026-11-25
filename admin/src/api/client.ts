/**
 * Minimal API client for the DevInterview backend.
 * Every endpoint returns ApiResponse<T> = { success, data, error: { code, message, details } }.
 * The access token lives in memory; the refresh token in sessionStorage (cleared when the tab closes).
 */
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080').replace(/\/$/, '');

export interface ApiErrorBody { code: string; message: string; details?: Record<string, string[]> | null }
export interface Envelope<T> { success: boolean; data: T | null; error: ApiErrorBody | null; traceId?: string }
export interface Paged<T> { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number; hasNext: boolean }

export class ApiError extends Error {
  constructor(public readonly status: number, public readonly code: string, message: string, public readonly details: Record<string, string[]> = {}) {
    super(message);
  }
  /** First validation message for a field (camelCase name). */
  field(name: string): string | undefined {
    const key = Object.keys(this.details).find((k) => k.toLowerCase() === name.toLowerCase());
    return key ? this.details[key]?.[0] : undefined;
  }
}

export interface UserSummary { id: string; email: string; firstName: string; lastName: string; roles: string[]; tier: string; emailConfirmed: boolean }
export interface AuthResponse { accessToken: string; accessTokenExpiresAt: string; refreshToken: string; refreshTokenExpiresAt: string; user: UserSummary }

const REFRESH_KEY = 'di.refresh';
let accessToken: string | null = null;
let refreshing: Promise<boolean> | null = null;
let onSessionEnded: (() => void) | null = null;

export const session = {
  set(auth: AuthResponse) { accessToken = auth.accessToken; sessionStorage.setItem(REFRESH_KEY, auth.refreshToken); },
  clear() { accessToken = null; sessionStorage.removeItem(REFRESH_KEY); },
  get refreshToken() { return sessionStorage.getItem(REFRESH_KEY); },
  get hasAccessToken() { return accessToken !== null; },
  onEnded(handler: (() => void) | null) { onSessionEnded = handler; },
};

async function parse<T>(res: Response): Promise<T> {
  let body: Envelope<T> | null = null;
  try { body = (await res.json()) as Envelope<T>; } catch { /* non-JSON */ }
  if (!res.ok || !body?.success) {
    const e = body?.error;
    throw new ApiError(res.status, e?.code ?? 'http_error', e?.message || `Request failed (${res.status}).`, e?.details ?? {});
  }
  return body.data as T;
}

/** Rotates the refresh token once even if several requests fail with 401 at the same time. */
export async function refreshSession(): Promise<boolean> {
  const token = session.refreshToken;
  if (!token) return false;
  refreshing ??= (async () => {
    try {
      const res = await fetch(`${API_BASE_URL}/api/v1/auth/refresh`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken: token }),
      });
      if (!res.ok) { session.clear(); return false; }
      session.set(await parse<AuthResponse>(res));
      return true;
    } catch {
      return false;
    } finally {
      refreshing = null;
    }
  })();
  return refreshing;
}

type Query = Record<string, string | number | boolean | null | undefined>;

function url(path: string, query?: Query) {
  const u = new URL(`${API_BASE_URL}/api/v1${path}`);
  Object.entries(query ?? {}).forEach(([k, v]) => { if (v !== undefined && v !== null && v !== '') u.searchParams.set(k, String(v)); });
  return u.toString();
}

async function send(method: string, path: string, opts: { query?: Query; body?: unknown; form?: FormData } = {}, retry = true): Promise<Response> {
  const headers: Record<string, string> = {};
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
  let body: BodyInit | undefined;
  if (opts.form) body = opts.form;
  else if (opts.body !== undefined) { headers['Content-Type'] = 'application/json'; body = JSON.stringify(opts.body); }
  let res: Response;
  try {
    res = await fetch(url(path, opts.query), { method, headers, body });
  } catch {
    throw new ApiError(0, 'network_error', 'Cannot reach the DevInterview API. Check your connection and the API URL.');
  }
  if (res.status === 401 && retry && session.refreshToken) {
    if (await refreshSession()) return send(method, path, opts, false);
    onSessionEnded?.();
  }
  return res;
}

export const api = {
  get: <T>(path: string, query?: Query) => send('GET', path, { query }).then((r) => parse<T>(r)),
  post: <T>(path: string, body?: unknown, query?: Query) => send('POST', path, { body: body ?? {}, query }).then((r) => parse<T>(r)),
  put: <T>(path: string, body: unknown) => send('PUT', path, { body }).then((r) => parse<T>(r)),
  del: <T = unknown>(path: string) => send('DELETE', path).then((r) => parse<T>(r)),
  upload: <T>(path: string, file: File, query?: Query) => {
    const form = new FormData();
    form.append('file', file);
    return send('POST', path, { form, query }).then((r) => parse<T>(r));
  },
  /** Downloads a file endpoint (export/template) and saves it through the browser. */
  async download(path: string, query: Query, fallbackName: string) {
    const res = await send('GET', path, { query });
    if (!res.ok) await parse(res);
    const blob = await res.blob();
    const name = /filename="?([^";]+)"?/.exec(res.headers.get('Content-Disposition') ?? '')?.[1] ?? fallbackName;
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = name;
    link.click();
    URL.revokeObjectURL(link.href);
  },
};

export function errorMessage(e: unknown): string {
  if (e instanceof ApiError) return e.message;
  if (e instanceof Error) return e.message;
  return 'Something went wrong.';
}
