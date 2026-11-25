import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, Checkbox, Divider, MenuItem, Paper, Stack, TextField, Typography,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { api, ApiError, errorMessage, type Paged } from '../api/client';
import { toOptions, useCategories, useTechnologies } from '../api/hooks';
import {
  ContentStatuses, Difficulties, ObjectiveTypes, QuestionTypes, humanize,
  type AdminQuestion, type AdminQuestionListItem, type OptionUpsert, type QuestionUpsert,
} from '../api/types';
import { DataTable, ErrorAlert, Loading, PageHeader, StatusChip, formatDate, useNotify } from '../components/Common';
import { ConfirmDialog, FormFields, ListEditor, type Field } from '../components/Form';

interface Filters { technologyId: string; categoryId: string; status: string; difficulty: string; type: string; search: string; page: number; pageSize: number }

export function QuestionsPage() {
  const navigate = useNavigate();
  const notify = useNotify();
  const qc = useQueryClient();
  const techs = useTechnologies();
  const [f, setF] = useState<Filters>({ technologyId: '', categoryId: '', status: '', difficulty: '', type: '', search: '', page: 1, pageSize: 20 });
  const cats = useCategories(f.technologyId || null);
  const [selected, setSelected] = useState<string[]>([]);
  const [deleting, setDeleting] = useState<AdminQuestionListItem | null>(null);
  const [search, setSearch] = useState('');
  useEffect(() => { const t = setTimeout(() => setF((s) => ({ ...s, search, page: 1 })), 350); return () => clearTimeout(t); }, [search]);

  const list = useQuery({
    queryKey: ['admin', 'questions', f],
    queryFn: () => api.get<Paged<AdminQuestionListItem>>('/admin/questions', { ...f }),
    placeholderData: keepPreviousData,
  });
  const set = (patch: Partial<Filters>) => { setF((s) => ({ ...s, ...patch, page: patch.page ?? 1 })); setSelected([]); };
  const bulk = async (status: string) => {
    try {
      const r = await api.post<{ updated: number }>('/admin/questions/bulk-status', { ids: selected, status });
      notify(`${r.updated} question(s) set to ${status}`); setSelected([]);
      await qc.invalidateQueries({ queryKey: ['admin', 'questions'] });
    } catch (e) { notify(errorMessage(e), 'error'); }
  };
  const rows = list.data?.items ?? [];
  const allSelected = rows.length > 0 && rows.every((r) => selected.includes(r.id));

  return (
    <>
      <PageHeader title="Questions" subtitle="Create, review and publish interview questions."
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate('/questions/new')}>New question</Button>} />
      <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
        <Stack direction="row" gap={2} flexWrap="wrap">
          <TextField size="small" label="Search" value={search} onChange={(e) => setSearch(e.target.value)} sx={{ minWidth: 240 }} />
          <TextField select size="small" label="Technology" value={f.technologyId} onChange={(e) => set({ technologyId: e.target.value, categoryId: '' })} sx={{ minWidth: 160 }}>
            <MenuItem value="">All</MenuItem>{techs.data?.map((t) => <MenuItem key={t.id} value={t.id}>{t.name}</MenuItem>)}
          </TextField>
          <TextField select size="small" label="Category" value={f.categoryId} onChange={(e) => set({ categoryId: e.target.value })} sx={{ minWidth: 160 }} disabled={!f.technologyId}>
            <MenuItem value="">All</MenuItem>{cats.data?.map((c) => <MenuItem key={c.id} value={c.id}>{c.name}</MenuItem>)}
          </TextField>
          {([['status', ContentStatuses], ['difficulty', Difficulties], ['type', QuestionTypes]] as const).map(([key, opts]) => (
            <TextField key={key} select size="small" label={humanize(key[0].toUpperCase() + key.slice(1))} value={f[key]} onChange={(e) => set({ [key]: e.target.value })} sx={{ minWidth: 140 }}>
              <MenuItem value="">All</MenuItem>{opts.map((o) => <MenuItem key={o} value={o}>{humanize(o)}</MenuItem>)}
            </TextField>
          ))}
        </Stack>
      </Paper>
      {selected.length > 0 && (
        <Alert severity="info" sx={{ mb: 2 }} action={
          <Stack direction="row" spacing={1}>
            {ContentStatuses.map((s) => <Button key={s} size="small" onClick={() => bulk(s)}>Set {s}</Button>)}
          </Stack>
        }>{selected.length} selected</Alert>
      )}
      <DataTable rows={list.data?.items} loading={list.isLoading} error={list.error} onRetry={() => list.refetch()} rowKey={(q) => q.id}
        empty="No questions match these filters."
        page={list.data ? { page: list.data.page, pageSize: list.data.pageSize, totalCount: list.data.totalCount } : undefined}
        onPage={(page, pageSize) => setF((s) => ({ ...s, page, pageSize }))}
        columns={[
          {
            header: '', width: 40, render: (q) => <Checkbox size="small" checked={selected.includes(q.id)} inputProps={{ 'aria-label': `Select ${q.title}` }}
              onClick={(e) => e.stopPropagation()} onChange={(e) => setSelected((s) => e.target.checked ? [...s, q.id] : s.filter((x) => x !== q.id))} />,
          },
          { header: 'Title', render: (q) => <Typography variant="body2" fontWeight={500}>{q.title}</Typography> },
          { header: 'Technology', render: (q) => `${q.technologyName} › ${q.categoryName}` },
          { header: 'Type', render: (q) => humanize(q.type) },
          { header: 'Difficulty', render: (q) => q.difficulty },
          { header: 'Status', render: (q) => <Stack direction="row" spacing={0.5}><StatusChip status={q.status} />{q.isPremium && <StatusChip status="Premium" />}</Stack> },
          { header: 'Updated', render: (q) => formatDate(q.updatedAt) },
          { header: '', align: 'right', render: (q) => <Button size="small" color="error" onClick={(e) => { e.stopPropagation(); setDeleting(q); }}>Delete</Button> },
        ]}
        onRowClick={(q) => navigate(`/questions/${q.id}`)} />
      {rows.length > 0 && (
        <Button size="small" sx={{ mt: 1 }} onClick={() => setSelected(allSelected ? [] : rows.map((r) => r.id))}>{allSelected ? 'Clear selection' : 'Select page'}</Button>
      )}
      <ConfirmDialog open={!!deleting} title="Delete question?" message={<>“{deleting?.title}” will be removed from the app. Learner history is kept.</>}
        onClose={() => setDeleting(null)}
        onConfirm={async () => { await api.del(`/admin/questions/${deleting!.id}`); notify('Question deleted'); await qc.invalidateQueries({ queryKey: ['admin', 'questions'] }); }} />
    </>
  );
}

const emptyQuestion = (technologyId = '', categoryId = ''): QuestionUpsert => ({
  technologyId, categoryId, topicId: null, title: '', body: '', codeSnippet: null, codeLanguage: null, type: 'SingleChoice', difficulty: 'Beginner',
  estimatedSeconds: 60, isPremium: false, status: 'Draft', tags: [],
  options: [{ text: '', isCorrect: true, explanation: null }, { text: '', isCorrect: false, explanation: null }],
  shortAnswer: '', detailedExplanation: '', referenceAnswer: null,
});

/** Client-side checks mirroring the backend validator (AdminContentService), so authors see problems before saving. */
export function validateQuestion(q: QuestionUpsert): string[] {
  const problems: string[] = [];
  if (!q.technologyId || !q.categoryId) problems.push('Choose a technology and category.');
  if (!q.title.trim()) problems.push('A title is required.');
  if (!q.body.trim()) problems.push('A body is required (repeat the question if there is nothing to add).');
  if (q.estimatedSeconds < 10 || q.estimatedSeconds > 3600) problems.push('Estimated time must be between 10 and 3600 seconds.');
  const options = ObjectiveTypes.includes(q.type) ? q.options.filter((o) => o.text.trim()) : [];
  const correct = options.filter((o) => o.isCorrect).length;
  if (options.length > 8) problems.push('At most 8 options.');
  if (q.type === 'MultipleChoice' || q.type === 'SingleChoice') {
    if (options.length < 2) problems.push('Choice questions need at least 2 options.');
    if (correct !== 1) problems.push('Choice questions need exactly one correct option.');
  } else if (q.type === 'MultipleSelect') {
    if (options.length < 3) problems.push('Multiple-select questions need at least 3 options.');
    if (correct < 1) problems.push('Mark at least one correct option.');
  } else if (q.type === 'TrueFalse' && (options.length !== 2 || correct !== 1)) {
    problems.push('True/False questions need exactly 2 options with one correct.');
  }
  if (new Set(options.map((o) => o.text.trim().toLowerCase())).size !== options.length) problems.push('Options must be unique.');
  if (q.status === 'Published' && !q.shortAnswer.trim()) problems.push('A short answer is required before publishing.');
  return problems;
}

export function QuestionEditorPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const notify = useNotify();
  const qc = useQueryClient();
  const existing = useQuery({ queryKey: ['admin', 'question', id], queryFn: () => api.get<AdminQuestion>(`/admin/questions/${id}`), enabled: !!id });
  const techs = useTechnologies();
  const [q, setQ] = useState<QuestionUpsert | null>(id ? null : emptyQuestion());
  const cats = useCategories(q?.technologyId || null);
  const [error, setError] = useState<unknown>(null);
  const [saving, setSaving] = useState(false);
  useEffect(() => { if (existing.data) setQ({ ...existing.data, options: existing.data.options.map(({ text, isCorrect, explanation }) => ({ text, isCorrect, explanation })) }); }, [existing.data]);
  useEffect(() => { if (q && !q.technologyId && techs.data?.length) setQ({ ...q, technologyId: techs.data[0].id }); }, [techs.data, q]);

  if (id && existing.isLoading) return <Loading />;
  if (existing.error) return <ErrorAlert error={existing.error} onRetry={() => existing.refetch()} />;
  if (!q) return <Loading />;

  const category = cats.data?.find((c) => c.id === q.categoryId);
  const objective = ObjectiveTypes.includes(q.type);
  const fields: Field<QuestionUpsert>[] = [
    { name: 'technologyId', label: 'Technology', type: 'select', options: toOptions(techs.data), required: true, width: '31%' },
    { name: 'categoryId', label: 'Category', type: 'select', options: toOptions(cats.data), required: true, width: '31%' },
    { name: 'topicId', label: 'Topic', type: 'select', options: toOptions(category?.topics), nullable: true, width: '31%' },
    { name: 'title', label: 'Question title', required: true },
    { name: 'body', label: 'Body (Markdown)', type: 'textarea', rows: 4, required: true, helper: 'The full question; supports Markdown.' },
    { name: 'codeSnippet', label: 'Code snippet', type: 'code', nullable: true, width: '70%' },
    { name: 'codeLanguage', label: 'Code language', nullable: true, width: '25%' },
    { name: 'type', label: 'Type', type: 'select', options: QuestionTypes, width: '23%' },
    { name: 'difficulty', label: 'Difficulty', type: 'select', options: Difficulties, width: '23%' },
    { name: 'status', label: 'Status', type: 'select', options: ContentStatuses, width: '23%' },
    { name: 'estimatedSeconds', label: 'Estimated seconds', type: 'number', width: '23%' },
    { name: 'tags', label: 'Tags', type: 'tags', width: '70%' },
    { name: 'isPremium', label: 'Premium only', type: 'switch' },
  ];
  const answerFields: Field<QuestionUpsert>[] = [
    { name: 'shortAnswer', label: 'Short answer', type: 'textarea', rows: 2, helper: 'Required before publishing.' },
    { name: 'detailedExplanation', label: 'Detailed explanation (Markdown)', type: 'textarea', rows: 6 },
    { name: 'referenceAnswer', label: 'Reference answer for AI evaluation (optional)', type: 'textarea', rows: 3, nullable: true,
      helper: 'Used by the AI evaluator as the expected answer for written questions.' },
  ];
  const optionFields: Field<OptionUpsert>[] = [
    { name: 'text', label: 'Option text', required: true, width: '70%' },
    { name: 'isCorrect', label: 'Correct', type: 'checkbox' },
    { name: 'explanation', label: 'Why (shown after answering)', nullable: true },
  ];
  const problems = validateQuestion(q);
  const save = async () => {
    setError(null);
    if (problems.length) { setError(new Error(problems.join(' '))); return; }
    setSaving(true);
    const body: QuestionUpsert = { ...q, options: objective ? q.options.filter((o) => o.text.trim()) : [] };
    try {
      const saved = id ? await api.put<AdminQuestion>(`/admin/questions/${id}`, body) : await api.post<AdminQuestion>('/admin/questions', body);
      notify('Question saved');
      await qc.invalidateQueries({ queryKey: ['admin'] });
      if (!id) navigate(`/questions/${saved.id}`, { replace: true });
    } catch (e) { setError(e); } finally { setSaving(false); }
  };

  return (
    <>
      <PageHeader title={id ? 'Edit question' : 'New question'} subtitle={existing.data ? `Last updated ${formatDate(existing.data.updatedAt)}` : undefined}
        actions={<>
          <Button onClick={() => navigate('/questions')}>Back</Button>
          <Button variant="contained" onClick={save} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button>
        </>} />
      {error != null && <Alert severity="error" sx={{ mb: 2 }}>{errorMessage(error)}</Alert>}
      <Paper variant="outlined" sx={{ p: 3 }}>
        <FormFields fields={fields} value={q} errors={error instanceof ApiError ? error : null}
          onChange={(v) => setQ(v.technologyId !== q.technologyId ? { ...v, categoryId: '', topicId: null }
            : v.type === 'TrueFalse' && q.type !== 'TrueFalse' ? { ...v, options: [{ text: 'True', isCorrect: true, explanation: null }, { text: 'False', isCorrect: false, explanation: null }] }
              : v)} />
        {objective && (
          <>
            <Divider sx={{ my: 3 }} />
            <ListEditor title="Options" items={q.options} fields={optionFields} minItems={2} addLabel="Add option"
              create={() => ({ text: '', isCorrect: false, explanation: null })} onChange={(options) => setQ({ ...q, options })} />
          </>
        )}
        <Divider sx={{ my: 3 }} />
        <FormFields fields={answerFields} value={q} onChange={setQ} errors={error instanceof ApiError ? error : null} />
        {problems.length > 0 && (
          <Box sx={{ mt: 2 }}>
            <Typography variant="subtitle2" color="warning.main">Fix before saving:</Typography>
            <ul style={{ margin: 0 }}>{problems.map((p) => <li key={p}><Typography variant="body2">{p}</Typography></li>)}</ul>
          </Box>
        )}
      </Paper>
    </>
  );
}
