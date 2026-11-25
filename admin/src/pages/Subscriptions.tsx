import { useMemo, useState } from 'react';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { Box, Button, Tab, Tabs } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { api, type Paged } from '../api/client';
import type { AdminSubscription, SubscriptionPlan } from '../api/types';
import { DataTable, PageHeader, StatusChip, formatDate, useNotify } from '../components/Common';
import { ConfirmDialog, FormDialog, type Field } from '../components/Form';

type PlanForm = {
  code: string; name: string; description: string; tier: string; price: number; currency: string; billingPeriodMonths: number;
  googlePlayProductId: string | null; googlePlayBasePlanId: string | null; features: string[]; isActive: boolean; displayOrder: number;
};

const planFields: Field<PlanForm>[] = [
  { name: 'code', label: 'Code', required: true, width: '31%', helper: 'Stable identifier, e.g. premium-monthly' },
  { name: 'name', label: 'Name', required: true, width: '31%' },
  { name: 'tier', label: 'Tier', type: 'select', options: ['Free', 'Premium'], width: '31%' },
  { name: 'description', label: 'Description', type: 'textarea' },
  { name: 'price', label: 'Display price', type: 'number', width: '23%', helper: 'Google Play shows the localised store price' },
  { name: 'currency', label: 'Currency', width: '23%' },
  { name: 'billingPeriodMonths', label: 'Billing period (months)', type: 'number', width: '23%' },
  { name: 'displayOrder', label: 'Order', type: 'number', width: '23%' },
  { name: 'googlePlayProductId', label: 'Google Play product id', nullable: true, width: '48%' },
  { name: 'googlePlayBasePlanId', label: 'Google Play base plan id', nullable: true, width: '48%' },
  { name: 'features', label: 'Features (shown on the plan card)', type: 'tags' },
  { name: 'isActive', label: 'Active', type: 'switch' },
];

export const priceToMicros = (price: number) => Math.round(price * 1_000_000);

export default function SubscriptionsPage() {
  const [tab, setTab] = useState(0);
  return (
    <>
      <PageHeader title="Subscriptions & plans" subtitle="Entitlements are granted only after the server verifies purchases with Google Play." />
      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}><Tab label="Subscriptions" /><Tab label="Plans" /></Tabs>
      {tab === 0 ? <Subscriptions /> : <Plans />}
    </>
  );
}

function Subscriptions() {
  const [page, setPage] = useState({ page: 1, pageSize: 20, search: '' });
  const [revoking, setRevoking] = useState<AdminSubscription | null>(null);
  const notify = useNotify();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: ['admin', 'subscriptions', page], queryFn: () => api.get<Paged<AdminSubscription>>('/admin/subscriptions', page), placeholderData: keepPreviousData });
  return (
    <>
      <DataTable rows={list.data?.items} loading={list.isLoading} error={list.error} onRetry={() => list.refetch()} rowKey={(s) => s.id}
        page={list.data} onPage={(p, s) => setPage({ ...page, page: p, pageSize: s })} columns={[
          { header: 'User', render: (s) => s.userEmail },
          { header: 'Plan', render: (s) => s.planName },
          { header: 'Provider', render: (s) => s.provider },
          { header: 'Status', render: (s) => <StatusChip status={s.status} /> },
          { header: 'Period', render: (s) => `${formatDate(s.startsAt)} → ${formatDate(s.expiresAt)}` },
          { header: 'Renews', render: (s) => (s.autoRenewing ? 'Yes' : 'No') },
          { header: 'Order', render: (s) => s.orderId ?? '—' },
          { header: '', align: 'right', render: (s) => (s.status === 'Active' || s.status === 'GracePeriod') && <Button size="small" color="error" onClick={() => setRevoking(s)}>Revoke</Button> },
        ]} />
      <ConfirmDialog open={!!revoking} title="Revoke subscription?" confirmLabel="Revoke" onClose={() => setRevoking(null)}
        message={<>Premium access for {revoking?.userEmail} ends immediately. {revoking?.provider === 'GooglePlay' && 'This does not refund or cancel billing in Google Play — do that in the Play Console.'}</>}
        onConfirm={async () => { await api.post(`/admin/subscriptions/${revoking!.id}/revoke`); notify('Subscription revoked'); await qc.invalidateQueries({ queryKey: ['admin', 'subscriptions'] }); }} />
    </>
  );
}

function Plans() {
  const notify = useNotify();
  const qc = useQueryClient();
  const plans = useQuery({ queryKey: ['admin', 'plans'], queryFn: () => api.get<SubscriptionPlan[]>('/admin/plans') });
  const [editing, setEditing] = useState<SubscriptionPlan | 'new' | null>(null);
  const initial = useMemo<PlanForm>(() => editing && editing !== 'new'
    ? { ...editing, price: editing.priceMicros / 1_000_000, isActive: true, displayOrder: (plans.data?.indexOf(editing) ?? 0) + 1 }
    : { code: '', name: '', description: '', tier: 'Premium', price: 0, currency: 'USD', billingPeriodMonths: 1, googlePlayProductId: null, googlePlayBasePlanId: null, features: [], isActive: true, displayOrder: (plans.data?.length ?? 0) + 1 },
  [editing, plans.data]);
  return (
    <>
      <Box sx={{ mb: 2 }}><Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing('new')}>New plan</Button></Box>
      <DataTable rows={plans.data} loading={plans.isLoading} error={plans.error} onRetry={() => plans.refetch()} rowKey={(p) => p.id} onRowClick={(p) => setEditing(p)}
        empty="No active plans." columns={[
          { header: 'Name', render: (p) => p.name },
          { header: 'Code', render: (p) => p.code },
          { header: 'Tier', render: (p) => p.tier },
          { header: 'Price', render: (p) => `${(p.priceMicros / 1_000_000).toFixed(2)} ${p.currency} / ${p.billingPeriodMonths} mo` },
          { header: 'Google Play', render: (p) => (p.googlePlayProductId ? `${p.googlePlayProductId}${p.googlePlayBasePlanId ? ` · ${p.googlePlayBasePlanId}` : ''}` : '—') },
        ]} />
      <FormDialog open={editing !== null} title={editing === 'new' ? 'New plan' : 'Edit plan'} initial={initial} fields={planFields} onClose={() => setEditing(null)}
        onSubmit={async ({ price, ...v }) => {
          const body = { ...v, priceMicros: priceToMicros(price) };
          if (editing === 'new') await api.post('/admin/plans', body); else await api.put(`/admin/plans/${(editing as SubscriptionPlan).id}`, body);
          notify('Plan saved'); await qc.invalidateQueries({ queryKey: ['admin', 'plans'] });
        }} />
    </>
  );
}
