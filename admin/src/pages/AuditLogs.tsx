import { useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Dialog, DialogContent, DialogTitle, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { api, type Paged } from '../api/client';
import type { AuditLog } from '../api/types';
import { DataTable, PageHeader, formatDate } from '../components/Common';

const entityTypes = ['Question', 'Technology', 'Category', 'Topic', 'Quiz', 'CodingChallenge', 'LearningPath', 'Flashcard', 'Company', 'AppUser',
  'UserSubscription', 'SubscriptionPlan', 'AiPromptTemplate', 'Notification', 'AppSetting', 'QuestionReport'];

const pretty = (details: string | null) => {
  if (!details) return '—';
  try { return JSON.stringify(JSON.parse(details), null, 2); } catch { return details; }
};

export default function AuditLogsPage() {
  const [q, setQ] = useState({ entityType: '', search: '', page: 1, pageSize: 50 });
  const [open, setOpen] = useState<AuditLog | null>(null);
  const list = useQuery({ queryKey: ['admin', 'audit', q], queryFn: () => api.get<Paged<AuditLog>>('/admin/audit-logs', q), placeholderData: keepPreviousData });
  return (
    <>
      <PageHeader title="Audit logs" subtitle="Administrative changes. Sensitive values are masked before they are stored." />
      <Stack direction="row" spacing={2} sx={{ mb: 2 }}>
        <TextField select size="small" label="Entity" value={q.entityType} onChange={(e) => setQ({ ...q, entityType: e.target.value, page: 1 })} sx={{ minWidth: 200 }}>
          <MenuItem value="">All</MenuItem>{entityTypes.map((t) => <MenuItem key={t} value={t}>{t}</MenuItem>)}
        </TextField>
        <TextField size="small" label="Search action" value={q.search} onChange={(e) => setQ({ ...q, search: e.target.value, page: 1 })} />
      </Stack>
      <DataTable rows={list.data?.items} loading={list.isLoading} error={list.error} onRetry={() => list.refetch()} rowKey={(l) => l.id}
        page={list.data} onPage={(page, pageSize) => setQ({ ...q, page, pageSize })} onRowClick={setOpen} columns={[
          { header: 'When', render: (l) => formatDate(l.createdAt) },
          { header: 'Who', render: (l) => l.userEmail ?? 'System' },
          { header: 'Action', render: (l) => l.action },
          { header: 'Entity', render: (l) => `${l.entityType}${l.entityId ? ` · ${l.entityId.slice(0, 8)}` : ''}` },
          { header: 'IP', render: (l) => l.ipAddress ?? '—' },
        ]} />
      <Dialog open={!!open} onClose={() => setOpen(null)} maxWidth="md" fullWidth>
        <DialogTitle>{open?.action} · {open?.entityType}</DialogTitle>
        <DialogContent>
          <Typography variant="body2" gutterBottom>{formatDate(open?.createdAt)} by {open?.userEmail ?? 'System'}{open?.entityId && ` · ${open.entityId}`}</Typography>
          <Typography component="pre" sx={{ fontFamily: 'monospace', fontSize: 13, whiteSpace: 'pre-wrap', bgcolor: 'grey.100', p: 2, borderRadius: 1 }}>{pretty(open?.details ?? null)}</Typography>
        </DialogContent>
      </Dialog>
    </>
  );
}
