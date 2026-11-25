import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from '../auth/AuthContext';
import { NotifyProvider } from '../components/Common';
import { AppRoutes } from '../App';

const json = (status: number, body: unknown) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
const ok = (data: unknown) => json(200, { success: true, data, error: null });
const authFor = (roles: string[]) => ({
  accessToken: 'a', accessTokenExpiresAt: '', refreshToken: 'r', refreshTokenExpiresAt: '',
  user: { id: 'u1', email: 'staff@example.com', firstName: 'Sam', lastName: 'Staff', roles, tier: 'Free', emailConfirmed: true },
});
const dashboard = {
  totalUsers: 120, activeUsers7d: 40, newUsers7d: 9, premiumUsers: 12, totalQuestions: 236, publishedQuestions: 230, draftQuestions: 6,
  totalChallenges: 23, quizAttempts7d: 51, submissions7d: 77, mockInterviews7d: 5, aiCalls7d: 19, aiCost7d: 1.25, openReports: 2,
  signupsTrend: [], activityTrend: [],
};

/** Routes fetch calls by path so the UI can be exercised end-to-end against a fake API. */
function fakeApi(roles: string[]) {
  return vi.fn(async (url: string, init?: RequestInit) => {
    const path = new URL(url).pathname;
    if (path.endsWith('/auth/login')) return ok(authFor(roles));
    if (path.endsWith('/auth/logout')) return ok({ message: 'ok' });
    if (path.endsWith('/admin/dashboard')) return ok(dashboard);
    if (path.endsWith('/admin/reports/content')) return ok({ technologies: [], hardestQuestions: [], challenges: [] });
    return json(404, { success: false, data: null, error: { code: 'not_found', message: `Unhandled ${init?.method ?? 'GET'} ${path}` } });
  });
}

function renderApp() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <NotifyProvider>
        <AuthProvider><MemoryRouter initialEntries={['/']}><AppRoutes /></MemoryRouter></AuthProvider>
      </NotifyProvider>
    </QueryClientProvider>,
  );
}

async function signIn() {
  const user = userEvent.setup();
  await user.type(await screen.findByLabelText(/email/i), 'staff@example.com');
  await user.type(screen.getByLabelText(/password/i), 'Secret123');
  await user.click(screen.getByRole('button', { name: /sign in/i }));
}

afterEach(() => vi.unstubAllGlobals());

describe('admin app', () => {
  it('redirects to sign-in and shows the live dashboard after login', async () => {
    vi.stubGlobal('fetch', fakeApi(['Admin', 'User']));
    renderApp();
    await signIn();
    expect(await screen.findByText('236')).toBeInTheDocument();
    expect(screen.getByText(/230 published · 6 draft/)).toBeInTheDocument();
    // Admins see operations pages (jsdom renders the mobile layout, so open the drawer first).
    await userEvent.setup().click(screen.getByRole('button', { name: /open navigation/i }));
    expect(await screen.findByRole('link', { name: /^users$/i })).toBeInTheDocument();
  });

  it('content managers do not see admin-only sections', async () => {
    vi.stubGlobal('fetch', fakeApi(['ContentManager', 'User']));
    renderApp();
    await signIn();
    await userEvent.setup().click(await screen.findByRole('button', { name: /open navigation/i }));
    expect(await screen.findByRole('link', { name: /^questions$/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /^users$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /settings/i })).not.toBeInTheDocument();
  });

  it('rejects accounts without an admin role and revokes their session', async () => {
    const fetchMock = fakeApi(['User']);
    vi.stubGlobal('fetch', fetchMock);
    renderApp();
    await signIn();
    expect(await screen.findByRole('alert')).toHaveTextContent('does not have access');
    await waitFor(() => expect(fetchMock.mock.calls.some(([u]) => String(u).endsWith('/auth/logout'))).toBe(true));
  });

  it('validates the login form before calling the API', async () => {
    const fetchMock = fakeApi(['Admin']);
    vi.stubGlobal('fetch', fetchMock);
    renderApp();
    await userEvent.setup().click(await screen.findByRole('button', { name: /sign in/i }));
    expect(screen.getByRole('alert')).toHaveTextContent('Enter your email and password.');
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
