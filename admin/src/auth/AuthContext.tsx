import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { api, ApiError, refreshSession, session, type AuthResponse, type UserSummary } from '../api/client';

/** Roles mirror the backend (RoleNames). The API enforces them; the UI only hides what a role cannot use. */
export const Roles = { SuperAdmin: 'SuperAdmin', Admin: 'Admin', ContentManager: 'ContentManager' } as const;
const panelRoles = [Roles.SuperAdmin, Roles.Admin, Roles.ContentManager];

export interface Permissions { content: boolean; admin: boolean; superAdmin: boolean }

export function permissionsFor(roles: string[]): Permissions {
  const admin = roles.includes(Roles.Admin) || roles.includes(Roles.SuperAdmin);
  return { content: admin || roles.includes(Roles.ContentManager), admin, superAdmin: roles.includes(Roles.SuperAdmin) };
}

interface AuthState {
  user: UserSummary | null;
  permissions: Permissions;
  restoring: boolean;
  login(email: string, password: string): Promise<void>;
  logout(): Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserSummary | null>(null);
  const [restoring, setRestoring] = useState(true);

  useEffect(() => {
    session.onEnded(() => setUser(null));
    // Restore a session after a page reload using the refresh token kept for this tab.
    (async () => {
      if (session.refreshToken && (await refreshSession())) {
        try {
          const me = await api.get<UserSummary & Record<string, unknown>>('/users/me');
          if (me.roles.some((r) => panelRoles.includes(r as never))) setUser(me);
          else session.clear();
        } catch { session.clear(); }
      }
      setRestoring(false);
    })();
    return () => session.onEnded(null);
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const auth = await api.post<AuthResponse>('/auth/login', { email: email.trim(), password, deviceName: 'Admin panel' });
    if (!auth.user.roles.some((r) => panelRoles.includes(r as never))) {
      // Revoke the session we just created: this account has no admin access.
      session.set(auth);
      await api.post('/auth/logout', { refreshToken: auth.refreshToken }).catch(() => undefined);
      session.clear();
      throw new ApiError(403, 'forbidden', 'This account does not have access to the admin panel.');
    }
    session.set(auth);
    setUser(auth.user);
  }, []);

  const logout = useCallback(async () => {
    const token = session.refreshToken;
    if (token) await api.post('/auth/logout', { refreshToken: token }).catch(() => undefined);
    session.clear();
    setUser(null);
  }, []);

  const value = useMemo<AuthState>(() => ({ user, permissions: permissionsFor(user?.roles ?? []), restoring, login, logout }), [user, restoring, login, logout]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthState {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
