import { useState } from 'react';
import { Alert, Button, Paper, Stack } from '@mui/material';
import { api, ApiError, errorMessage } from '../api/client';
import { NotificationCategories } from '../api/types';
import { PageHeader, useNotify } from '../components/Common';
import { ConfirmDialog, FormFields, type Field } from '../components/Form';

type Broadcast = { title: string; body: string; category: string; deepLink: string | null; audience: string; sendPush: boolean };

const fields: Field<Broadcast>[] = [
  { name: 'title', label: 'Title', required: true },
  { name: 'body', label: 'Message', type: 'textarea', rows: 3, required: true },
  { name: 'category', label: 'Category', type: 'select', options: NotificationCategories, width: '31%' },
  { name: 'audience', label: 'Audience', type: 'select', options: ['All', 'Premium', 'Free'], width: '31%' },
  { name: 'deepLink', label: 'Deep link (optional)', nullable: true, width: '31%', helper: 'e.g. devinterview://quiz/daily or devinterview://learn' },
  { name: 'sendPush', label: 'Also send a push notification', type: 'switch' },
];

const blank: Broadcast = { title: '', body: '', category: 'System', deepLink: null, audience: 'All', sendPush: true };

export default function NotificationsPage() {
  const notify = useNotify();
  const [form, setForm] = useState<Broadcast>(blank);
  const [confirm, setConfirm] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const valid = form.title.trim() && form.body.trim() && (!form.deepLink || form.deepLink.startsWith('devinterview://'));
  return (
    <>
      <PageHeader title="Broadcast notification" subtitle="Send an in-app notification (and optionally a push) to a group of learners." />
      <Paper variant="outlined" sx={{ p: 3, maxWidth: 820 }}>
        {error != null && <Alert severity="error" sx={{ mb: 2 }}>{errorMessage(error)}</Alert>}
        <FormFields fields={fields} value={form} onChange={setForm} errors={error instanceof ApiError ? error : null} />
        {form.deepLink && !form.deepLink.startsWith('devinterview://') && <Alert severity="warning" sx={{ mt: 2 }}>Deep links must start with devinterview://</Alert>}
        <Stack direction="row" justifyContent="flex-end" sx={{ mt: 2 }}>
          <Button variant="contained" disabled={!valid} onClick={() => setConfirm(true)}>Send…</Button>
        </Stack>
      </Paper>
      <ConfirmDialog open={confirm} title="Send this notification?" confirmLabel="Send" onClose={() => setConfirm(false)}
        message={`“${form.title}” will be delivered to ${form.audience === 'All' ? 'all active users' : `${form.audience.toLowerCase()} users`}.`}
        onConfirm={async () => {
          setError(null);
          try {
            const r = await api.post<{ recipients: number; pushQueued: number }>('/admin/notifications/broadcast', form);
            notify(`Sent to ${r.recipients} users (${r.pushQueued} push)`); setForm(blank);
          } catch (e) { setError(e); throw e; }
        }} />
    </>
  );
}
