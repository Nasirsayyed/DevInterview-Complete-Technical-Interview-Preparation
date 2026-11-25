import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Typography } from '@mui/material';
import { api } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { AppSetting } from '../api/types';
import { DataTable, PageHeader, useNotify } from '../components/Common';
import { FormDialog } from '../components/Form';

export default function SettingsPage() {
  const { permissions } = useAuth();
  const notify = useNotify();
  const qc = useQueryClient();
  const settings = useQuery({ queryKey: ['admin', 'settings'], queryFn: () => api.get<AppSetting[]>('/admin/settings') });
  const [editing, setEditing] = useState<AppSetting | null>(null);
  return (
    <>
      <PageHeader title="Settings" subtitle="Runtime settings stored in the database. Secrets are never stored here — they come from server environment variables." />
      {!permissions.superAdmin && <Alert severity="info" sx={{ mb: 2 }}>Only a Super Admin can change settings.</Alert>}
      <DataTable rows={settings.data} loading={settings.isLoading} error={settings.error} onRetry={() => settings.refetch()} rowKey={(s) => s.key}
        columns={[
          { header: 'Key', render: (s) => <Typography fontFamily="monospace" variant="body2">{s.key}</Typography> },
          { header: 'Value', render: (s) => s.value },
          { header: 'Description', render: (s) => s.description ?? '—' },
          { header: '', align: 'right', render: (s) => permissions.superAdmin && <Button size="small" onClick={() => setEditing(s)}>Edit</Button> },
        ]} />
      <FormDialog<AppSetting> open={!!editing} title={`Edit ${editing?.key ?? ''}`} initial={editing ?? { key: '', value: '', description: null }}
        fields={[{ name: 'value', label: 'Value', required: true }, { name: 'description', label: 'Description', nullable: true }]}
        onClose={() => setEditing(null)}
        onSubmit={async (v) => { await api.put('/admin/settings', { ...v, key: editing!.key }); notify('Setting saved'); await qc.invalidateQueries({ queryKey: ['admin', 'settings'] }); }} />
    </>
  );
}
