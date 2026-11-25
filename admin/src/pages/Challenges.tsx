import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Divider, Paper, Stack, Tab, Tabs, TextField, Typography } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { api, ApiError, errorMessage, type Paged } from '../api/client';
import {
  ChallengeCategories, ChallengeDifficulties, ContentStatuses, ExecutionLanguages, humanize,
  type AdminChallenge, type AdminChallengeListItem, type ChallengeUpsert, type TestCase,
} from '../api/types';
import { DataTable, ErrorAlert, Loading, PageHeader, StatusChip, formatDate, useNotify } from '../components/Common';
import { ConfirmDialog, FormFields, ListEditor, type Field } from '../components/Form';

export function ChallengesPage() {
  const navigate = useNavigate();
  const notify = useNotify();
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState({ page: 1, pageSize: 20 });
  const [deleting, setDeleting] = useState<AdminChallengeListItem | null>(null);
  const list = useQuery({
    queryKey: ['admin', 'challenges', search, page],
    queryFn: () => api.get<Paged<AdminChallengeListItem>>('/admin/challenges', { search, ...page }),
    placeholderData: keepPreviousData,
  });
  return (
    <>
      <PageHeader title="Coding challenges" subtitle="Problems, test cases and starter code. Submissions run in the sandbox."
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate('/challenges/new')}>New challenge</Button>} />
      <TextField size="small" label="Search" value={search} onChange={(e) => { setSearch(e.target.value); setPage({ ...page, page: 1 }); }} sx={{ mb: 2 }} />
      <DataTable rows={list.data?.items} loading={list.isLoading} error={list.error} onRetry={() => list.refetch()} rowKey={(c) => c.id}
        page={list.data} onPage={(p, s) => setPage({ page: p, pageSize: s })} onRowClick={(c) => navigate(`/challenges/${c.id}`)}
        columns={[
          { header: 'Title', render: (c) => c.title },
          { header: 'Category', render: (c) => humanize(c.category) },
          { header: 'Difficulty', render: (c) => c.difficulty },
          { header: 'Tests', render: (c) => c.testCaseCount, align: 'right' },
          { header: 'Submissions', render: (c) => c.submissionCount, align: 'right' },
          { header: 'Status', render: (c) => <Stack direction="row" spacing={0.5}><StatusChip status={c.status} />{c.isPremium && <StatusChip status="Premium" />}</Stack> },
          { header: '', align: 'right', render: (c) => <Button size="small" color="error" onClick={(e) => { e.stopPropagation(); setDeleting(c); }}>Delete</Button> },
        ]} />
      <ConfirmDialog open={!!deleting} title={`Delete “${deleting?.title}”?`} message="Existing submissions are kept." onClose={() => setDeleting(null)}
        onConfirm={async () => { await api.del(`/admin/challenges/${deleting!.id}`); notify('Challenge deleted'); await qc.invalidateQueries({ queryKey: ['admin', 'challenges'] }); }} />
    </>
  );
}

const emptyChallenge = (): ChallengeUpsert => ({
  title: '', slug: null, category: 'Arrays', difficulty: 'Easy', problemStatement: '', inputDescription: '', outputDescription: '', constraints: '',
  expectedTimeComplexity: null, expectedSpaceComplexity: null, hints: [], solutionExplanation: '', isPremium: false, status: 'Draft',
  timeLimitMs: 2000, memoryLimitMb: 256, allowedLanguages: ['python', 'csharp', 'javascript', 'java'], starterCode: {},
  testCases: [{ id: null, input: '', expectedOutput: '', isSample: true, explanation: null }],
});

const fields: Field<ChallengeUpsert>[] = [
  { name: 'title', label: 'Title', required: true, width: '60%' },
  { name: 'slug', label: 'Slug', nullable: true, width: '35%' },
  { name: 'category', label: 'Category', type: 'select', options: ChallengeCategories, width: '23%' },
  { name: 'difficulty', label: 'Difficulty', type: 'select', options: ChallengeDifficulties, width: '23%' },
  { name: 'status', label: 'Status', type: 'select', options: ContentStatuses, width: '23%' },
  { name: 'isPremium', label: 'Premium only', type: 'switch' },
  { name: 'problemStatement', label: 'Problem statement (Markdown)', type: 'textarea', rows: 6, required: true },
  { name: 'inputDescription', label: 'Input format', type: 'textarea', rows: 2, required: true, width: '48%' },
  { name: 'outputDescription', label: 'Output format', type: 'textarea', rows: 2, required: true, width: '48%' },
  { name: 'constraints', label: 'Constraints', type: 'textarea', rows: 2, required: true },
  { name: 'expectedTimeComplexity', label: 'Expected time complexity', nullable: true, width: '48%' },
  { name: 'expectedSpaceComplexity', label: 'Expected space complexity', nullable: true, width: '48%' },
  { name: 'timeLimitMs', label: 'Time limit (ms)', type: 'number', width: '23%' },
  { name: 'memoryLimitMb', label: 'Memory limit (MB)', type: 'number', width: '23%' },
  { name: 'allowedLanguages', label: 'Allowed languages', type: 'multiselect', options: ExecutionLanguages, width: '48%' },
  { name: 'hints', label: 'Hints (revealed one at a time)', type: 'tags', helper: 'Type a hint and press Enter' },
  { name: 'solutionExplanation', label: 'Solution explanation (Markdown)', type: 'textarea', rows: 5, required: true },
];

const testFields: Field<TestCase>[] = [
  { name: 'input', label: 'stdin', type: 'code', rows: 2, width: '48%' },
  { name: 'expectedOutput', label: 'Expected stdout', type: 'code', rows: 2, width: '48%' },
  { name: 'isSample', label: 'Sample (visible to learners)', type: 'checkbox' },
  { name: 'explanation', label: 'Explanation (samples only)', nullable: true, hidden: (t) => !t.isSample },
];

export function ChallengeEditorPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const notify = useNotify();
  const qc = useQueryClient();
  const existing = useQuery({ queryKey: ['admin', 'challenge', id], queryFn: () => api.get<AdminChallenge>(`/admin/challenges/${id}`), enabled: !!id });
  const [c, setC] = useState<ChallengeUpsert | null>(id ? null : emptyChallenge());
  const [lang, setLang] = useState(0);
  const [error, setError] = useState<unknown>(null);
  const [saving, setSaving] = useState(false);
  useEffect(() => { if (existing.data) setC(existing.data); }, [existing.data]);
  if (id && existing.isLoading) return <Loading />;
  if (existing.error) return <ErrorAlert error={existing.error} onRetry={() => existing.refetch()} />;
  if (!c) return <Loading />;

  const languages = c.allowedLanguages;
  const current = languages[Math.min(lang, Math.max(0, languages.length - 1))];
  const save = async () => {
    setError(null);
    if (!c.testCases.some((t) => t.isSample)) { setError(new Error('Add at least one sample test case.')); return; }
    if (c.testCases.length < 2) { setError(new Error('Add at least one hidden test case so submissions are graded on more than the samples.')); return; }
    setSaving(true);
    try {
      const starterCode = Object.fromEntries(Object.entries(c.starterCode).filter(([k, v]) => languages.includes(k) && v.trim()));
      const saved = id ? await api.put<AdminChallenge>(`/admin/challenges/${id}`, { ...c, starterCode }) : await api.post<AdminChallenge>('/admin/challenges', { ...c, starterCode });
      notify('Challenge saved');
      await qc.invalidateQueries({ queryKey: ['admin'] });
      if (!id) navigate(`/challenges/${saved.id}`, { replace: true });
    } catch (e) { setError(e); } finally { setSaving(false); }
  };

  return (
    <>
      <PageHeader title={id ? 'Edit challenge' : 'New challenge'} subtitle={existing.data ? `Last updated ${formatDate(existing.data.updatedAt)}` : undefined}
        actions={<><Button onClick={() => navigate('/challenges')}>Back</Button><Button variant="contained" onClick={save} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button></>} />
      {error != null && <Alert severity="error" sx={{ mb: 2 }}>{errorMessage(error)}</Alert>}
      <Paper variant="outlined" sx={{ p: 3 }}>
        <FormFields fields={fields} value={c} onChange={setC} errors={error instanceof ApiError ? error : null} />
        <Divider sx={{ my: 3 }} />
        <Typography variant="subtitle1" fontWeight={600}>Starter code</Typography>
        {languages.length === 0 ? <Typography color="text.secondary">Select allowed languages first.</Typography> : (
          <>
            <Tabs value={languages.indexOf(current)} onChange={(_, v) => setLang(v)} variant="scrollable">
              {languages.map((l) => <Tab key={l} label={l} />)}
            </Tabs>
            <TextField fullWidth multiline minRows={8} value={c.starterCode[current] ?? ''} placeholder={`Starter code for ${current} (reads stdin, writes stdout)`}
              onChange={(e) => setC({ ...c, starterCode: { ...c.starterCode, [current]: e.target.value } })}
              slotProps={{ htmlInput: { style: { fontFamily: 'ui-monospace, Menlo, Consolas, monospace', fontSize: 13 }, spellCheck: false, 'aria-label': `Starter code ${current}` } }} />
          </>
        )}
        <Divider sx={{ my: 3 }} />
        <ListEditor title="Test cases" items={c.testCases} fields={testFields} minItems={1} addLabel="Add test case"
          create={() => ({ id: null, input: '', expectedOutput: '', isSample: false, explanation: null })} onChange={(testCases) => setC({ ...c, testCases })} />
        <Typography variant="caption" color="text.secondary">Hidden test cases are never sent to the app; only pass/fail is shown for them.</Typography>
      </Paper>
    </>
  );
}
