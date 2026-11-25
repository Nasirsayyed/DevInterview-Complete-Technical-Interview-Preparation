import { useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, Link, MenuItem, Stack, TextField } from '@mui/material';
import { api, type Paged } from '../api/client';
import { humanize, type QuestionReport } from '../api/types';
import { DataTable, PageHeader, StatusChip, formatDate, useNotify } from '../components/Common';
import { FormDialog } from '../components/Form';

type Resolve = { status: string; resolutionNote: string | null };

export default function ReportsPage() {
  const [status, setStatus] = useState('Open');
  const [page, setPage] = useState({ page: 1, pageSize: 20 });
  const [resolving, setResolving] = useState<QuestionReport | null>(null);
  const notify = useNotify();
  const qc = useQueryClient();
  const list = useQuery({
    queryKey: ['admin', 'reports', status, page],
    queryFn: () => api.get<Paged<QuestionReport>>('/admin/question-reports', { status, ...page }),
    placeholderData: keepPreviousData,
  });
  return (
    <>
      <PageHeader title="Question reports" subtitle="Problems learners reported on questions." />
      <Stack direction="row" sx={{ mb: 2 }}>
        <TextField select size="small" label="Status" value={status} onChange={(e) => { setStatus(e.target.value); setPage({ ...page, page: 1 }); }} sx={{ minWidth: 160 }}>
          {['Open', 'Resolved', 'Rejected'].map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
          <MenuItem value="">All</MenuItem>
        </TextField>
      </Stack>
      <DataTable rows={list.data?.items} loading={list.isLoading} error={list.error} onRetry={() => list.refetch()} rowKey={(r) => r.id}
        empty="No reports with this status." page={list.data} onPage={(p, s) => setPage({ page: p, pageSize: s })}
        columns={[
          { header: 'Question', render: (r) => <Link component={RouterLink} to={`/questions/${r.questionId}`}>{r.questionTitle}</Link> },
          { header: 'Reason', render: (r) => humanize(r.reason) },
          { header: 'Details', render: (r) => r.details ?? '—' },
          { header: 'Reporter', render: (r) => r.reporterEmail },
          { header: 'Reported', render: (r) => formatDate(r.createdAt) },
          { header: 'Status', render: (r) => <StatusChip status={r.status} /> },
          { header: '', align: 'right', render: (r) => <Button size="small" onClick={() => setResolving(r)}>{r.status === 'Open' ? 'Resolve' : 'Update'}</Button> },
        ]} />
      <FormDialog<Resolve> open={!!resolving} title="Resolve report" initial={{ status: 'Resolved', resolutionNote: resolving?.resolutionNote ?? null }}
        fields={[
          { name: 'status', label: 'Outcome', type: 'select', options: ['Resolved', 'Rejected', 'Open'] },
          { name: 'resolutionNote', label: 'Note', type: 'textarea', nullable: true, helper: 'What was changed, or why the report was rejected.' },
        ]}
        onClose={() => setResolving(null)}
        onSubmit={async (v) => { await api.put(`/admin/question-reports/${resolving!.id}`, v); notify('Report updated'); await qc.invalidateQueries({ queryKey: ['admin'] }); }} />
    </>
  );
}
