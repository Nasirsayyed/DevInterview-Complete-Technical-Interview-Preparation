import { useEffect, useMemo, useState } from 'react';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, FormGroup, MenuItem, Stack, TextField, Typography,
} from '@mui/material';
import { api, errorMessage, type Paged } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { AssignableRoles, type AdminUser, type SubscriptionPlan } from '../api/types';
import { DataTable, PageHeader, StatusChip, formatDate, useNotify } from '../components/Common';
import { ConfirmDialog, FormDialog } from '../components/Form';

type Grant = { planId: string; months: number; notes: string | null };

export default function UsersPage() {
  const { user: me, permissions } = useAuth();
  const notify = useNotify();
  const qc = useQueryClient();
  const [input, setInput] = useState('');
  const [q, setQ] = useState({ search: '', role: '', isActive: '', page: 1, pageSize: 20 });
  useEffect(() => { const t = setTimeout(() => setQ((s) => ({ ...s, search: input, page: 1 })), 350); return () => clearTimeout(t); }, [input]);
  const list = useQuery({
    queryKey: ['admin', 'users', q],
    queryFn: () => api.get<Paged<AdminUser>>('/admin/users', { ...q, isActive: q.isActive === '' ? undefined : q.isActive }),
    placeholderData: keepPreviousData,
  });
  const plans = useQuery({ queryKey: ['admin', 'plans'], queryFn: () => api.get<SubscriptionPlan[]>('/admin/plans') });
  const [roles, setRoles] = useState<{ user: AdminUser; roles: string[] } | null>(null);
  const [toggling, setToggling] = useState<AdminUser | null>(null);
  const [granting, setGranting] = useState<AdminUser | null>(null);
  const [savingRoles, setSavingRoles] = useState(false);
  const refresh = () => qc.invalidateQueries({ queryKey: ['admin', 'users'] });
  const premiumPlans = (plans.data ?? []).filter((p) => p.tier === 'Premium');
  const grantInitial = useMemo<Grant>(() => ({ planId: premiumPlans[0]?.id ?? '', months: 1, notes: null }), [premiumPlans]);
  // Only SuperAdmins may hand out admin-level roles; the API enforces the same rule.
  const assignable = AssignableRoles.filter((r) => r !== 'User' && (permissions.superAdmin || (r !== 'SuperAdmin' && r !== 'Admin')));
  const canEditRoles = (u: AdminUser) => permissions.superAdmin || !u.roles.some((r) => r === 'Admin' || r === 'SuperAdmin');

  return (
    <>
      <PageHeader title="Users" subtitle="Accounts, roles and complimentary subscriptions." />
      <Stack direction="row" gap={2} flexWrap="wrap" sx={{ mb: 2 }}>
        <TextField size="small" label="Search name or email" value={input} onChange={(e) => setInput(e.target.value)} sx={{ minWidth: 260 }} />
        <TextField select size="small" label="Role" value={q.role} onChange={(e) => setQ({ ...q, role: e.target.value, page: 1 })} sx={{ minWidth: 160 }}>
          <MenuItem value="">Any</MenuItem>{AssignableRoles.map((r) => <MenuItem key={r} value={r}>{r}</MenuItem>)}
        </TextField>
        <TextField select size="small" label="Status" value={q.isActive} onChange={(e) => setQ({ ...q, isActive: e.target.value, page: 1 })} sx={{ minWidth: 140 }}>
          <MenuItem value="">Any</MenuItem><MenuItem value="true">Active</MenuItem><MenuItem value="false">Deactivated</MenuItem>
        </TextField>
      </Stack>
      <DataTable rows={list.data?.items} loading={list.isLoading} error={list.error} onRetry={() => list.refetch()} rowKey={(u) => u.id}
        page={list.data} onPage={(page, pageSize) => setQ({ ...q, page, pageSize })} columns={[
          { header: 'Name', render: (u) => <><Typography variant="body2" fontWeight={500}>{u.fullName}</Typography><Typography variant="caption">{u.email}{!u.emailConfirmed && ' · unverified'}</Typography></> },
          { header: 'Roles', render: (u) => u.roles.join(', ') || 'User' },
          { header: 'Plan', render: (u) => u.tier === 'Premium' ? `Premium until ${formatDate(u.premiumExpiresAt)}` : 'Free' },
          { header: 'Joined', render: (u) => formatDate(u.createdAt) },
          { header: 'Last sign-in', render: (u) => formatDate(u.lastLoginAt) },
          { header: 'Status', render: (u) => <StatusChip status={u.isActive ? 'Active' : 'Revoked'} /> },
          {
            header: '', align: 'right', render: (u) => u.id === me?.id ? <Typography variant="caption">You</Typography> : (
              <Stack direction="row" spacing={0.5} justifyContent="flex-end">
                <Button size="small" onClick={() => setRoles({ user: u, roles: u.roles })} disabled={!canEditRoles(u)}>Roles</Button>
                <Button size="small" onClick={() => setGranting(u)} disabled={!premiumPlans.length}>Grant Premium</Button>
                <Button size="small" color={u.isActive ? 'error' : 'success'} onClick={() => setToggling(u)}>{u.isActive ? 'Deactivate' : 'Reactivate'}</Button>
              </Stack>
            ),
          },
        ]} />
      <Dialog open={!!roles} onClose={() => setRoles(null)}>
        <DialogTitle>Roles for {roles?.user.email}</DialogTitle>
        <DialogContent>
          <FormGroup>
            {AssignableRoles.map((r) => (
              <FormControlLabel key={r} label={r} control={<Checkbox checked={r === 'User' || (roles?.roles.includes(r) ?? false)} disabled={!assignable.includes(r)}
                onChange={(e) => roles && setRoles({ ...roles, roles: e.target.checked ? [...roles.roles, r] : roles.roles.filter((x) => x !== r) })} />} />
            ))}
          </FormGroup>
          <Typography variant="caption" color="text.secondary">The user is signed out so the new roles take effect at their next sign-in.</Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setRoles(null)}>Cancel</Button>
          <Button variant="contained" disabled={savingRoles} onClick={async () => {
            setSavingRoles(true);
            try { await api.put(`/admin/users/${roles!.user.id}/roles`, { roles: roles!.roles }); notify('Roles updated'); setRoles(null); await refresh(); }
            catch (e) { notify(errorMessage(e), 'error'); } finally { setSavingRoles(false); }
          }}>Save</Button>
        </DialogActions>
      </Dialog>
      <ConfirmDialog open={!!toggling} title={toggling?.isActive ? 'Deactivate account?' : 'Reactivate account?'} confirmLabel={toggling?.isActive ? 'Deactivate' : 'Reactivate'}
        message={toggling?.isActive ? `${toggling.email} will be signed out everywhere and can't sign in until reactivated.` : `${toggling?.email} will be able to sign in again.`}
        onClose={() => setToggling(null)}
        onConfirm={async () => { await api.put(`/admin/users/${toggling!.id}/active`, { isActive: !toggling!.isActive }); notify('Account updated'); await refresh(); }} />
      <FormDialog<Grant> open={!!granting} title={`Grant Premium to ${granting?.email ?? ''}`} initial={grantInitial} submitLabel="Grant"
        fields={[
          { name: 'planId', label: 'Plan', type: 'select', options: premiumPlans.map((p) => ({ value: p.id, label: p.name })) },
          { name: 'months', label: 'Months', type: 'number' },
          { name: 'notes', label: 'Reason (stored in the audit log)', nullable: true },
        ]}
        onClose={() => setGranting(null)}
        onSubmit={async (v) => { await api.post(`/admin/users/${granting!.id}/subscriptions`, v); notify('Premium granted'); await refresh(); }} />
    </>
  );
}
