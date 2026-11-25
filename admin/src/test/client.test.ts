import { afterEach, describe, expect, it, vi } from 'vitest';
import { api, ApiError, session } from '../api/client';

const json = (status: number, body: unknown) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
const ok = (data: unknown) => json(200, { success: true, data, error: null });
const auth = (access: string, refresh: string) => ({
  accessToken: access, accessTokenExpiresAt: '', refreshToken: refresh, refreshTokenExpiresAt: '',
  user: { id: 'u', email: 'a@x.io', firstName: 'A', lastName: 'B', roles: ['Admin'], tier: 'Free', emailConfirmed: true },
});

afterEach(() => { vi.unstubAllGlobals(); session.clear(); });

describe('api client', () => {
  it('unwraps the ApiResponse envelope', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(ok([{ id: '1' }])));
    await expect(api.get('/admin/technologies')).resolves.toEqual([{ id: '1' }]);
  });

  it('throws ApiError with validation details', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(json(400, { success: false, data: null, error: { code: 'validation_error', message: 'Invalid', details: { Title: ['Title is required.'] } } })));
    const err = (await api.post('/admin/questions', {}).catch((e: unknown) => e)) as ApiError;
    expect(err).toBeInstanceOf(ApiError);
    expect(err.code).toBe('validation_error');
    expect(err.field('title')).toBe('Title is required.');
  });

  it('sends the bearer token and query parameters, skipping empty values', async () => {
    const fetchMock = vi.fn().mockResolvedValue(ok([]));
    vi.stubGlobal('fetch', fetchMock);
    session.set(auth('access-1', 'refresh-1'));
    await api.get('/admin/questions', { page: 2, search: '', status: 'Draft', technologyId: undefined });
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://localhost:5080/api/v1/admin/questions?page=2&status=Draft');
    expect(init.headers.Authorization).toBe('Bearer access-1');
  });

  it('refreshes once on 401 and retries with the new token', async () => {
    session.set(auth('expired', 'refresh-1'));
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(json(401, { success: false, data: null, error: { code: 'unauthorized', message: 'Expired' } }))
      .mockResolvedValueOnce(ok(auth('access-2', 'refresh-2')))
      .mockResolvedValueOnce(ok({ totalUsers: 3 }));
    vi.stubGlobal('fetch', fetchMock);
    await expect(api.get('/admin/dashboard')).resolves.toEqual({ totalUsers: 3 });
    expect(fetchMock.mock.calls[1][0]).toContain('/auth/refresh');
    expect(JSON.parse(fetchMock.mock.calls[1][1].body)).toEqual({ refreshToken: 'refresh-1' });
    expect(fetchMock.mock.calls[2][1].headers.Authorization).toBe('Bearer access-2');
    expect(session.refreshToken).toBe('refresh-2');
  });

  it('ends the session when the refresh token is rejected', async () => {
    session.set(auth('expired', 'stolen'));
    const ended = vi.fn();
    session.onEnded(ended);
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce(json(401, { success: false, data: null, error: { code: 'unauthorized', message: 'Expired' } }))
      .mockResolvedValueOnce(json(401, { success: false, data: null, error: { code: 'invalid_token', message: 'Reused' } })));
    await expect(api.get('/admin/dashboard')).rejects.toBeInstanceOf(ApiError);
    expect(ended).toHaveBeenCalled();
    expect(session.refreshToken).toBeNull();
    session.onEnded(null);
  });

  it('reports network failures clearly', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));
    const err = (await api.get('/admin/dashboard').catch((e: unknown) => e)) as ApiError;
    expect(err.code).toBe('network_error');
  });
});
