import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Divider, Paper, Stack, Typography } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { api, ApiError, errorMessage } from '../api/client';
import { toOptions, useTechnologies } from '../api/hooks';
import {
  CollectionKinds, ContentSources, Difficulties,
  type AdminCompany, type CollectionUpsert, type CompanyTopicUpsert, type CompanyUpsert, type RoundUpsert,
} from '../api/types';
import { DataTable, ErrorAlert, Loading, PageHeader, StatusChip, useNotify } from '../components/Common';
import { ConfirmDialog, FormFields, ListEditor, type Field } from '../components/Form';
import { ChallengePicker, QuestionPicker } from '../components/Pickers';

const sourceHelp = 'Verified needs a public source reference. Use Illustrative for original practice material — never present it as real interview questions.';

/** Mirrors the backend rule: Verified and community content must cite where it came from. */
export function sourceProblems(c: CompanyUpsert): string[] {
  const problems: string[] = [];
  const needsRef = (source: string, ref: string | null) => source === 'Verified' && !ref?.trim();
  if (c.rounds.some((r) => needsRef(r.source, r.sourceReference))) problems.push('Verified interview rounds need a source reference.');
  if (c.collections.some((x) => needsRef(x.source, x.sourceReference))) problems.push('Verified collections need a source reference.');
  return problems;
}

export function CompaniesPage() {
  const navigate = useNavigate();
  const notify = useNotify();
  const qc = useQueryClient();
  const [deleting, setDeleting] = useState<AdminCompany | null>(null);
  const list = useQuery({ queryKey: ['admin', 'companies'], queryFn: () => api.get<AdminCompany[]>('/admin/companies') });
  return (
    <>
      <PageHeader title="Companies" subtitle="Company preparation profiles. Every item carries a content source."
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate('/companies/new')}>New company</Button>} />
      <DataTable rows={list.data} loading={list.isLoading} error={list.error} onRetry={() => list.refetch()} rowKey={(c) => c.id}
        onRowClick={(c) => navigate(`/companies/${c.id}`)} columns={[
          { header: 'Name', render: (c) => c.name },
          { header: 'Industry', render: (c) => c.industry ?? '—' },
          { header: 'Overview source', render: (c) => <StatusChip status={c.overviewSource} /> },
          { header: 'Rounds', render: (c) => c.rounds.length, align: 'right' },
          { header: 'Collections', render: (c) => c.collections.length, align: 'right' },
          { header: 'Status', render: (c) => <StatusChip status={c.isActive ? 'Active' : 'Archived'} /> },
          { header: '', align: 'right', render: (c) => <Button size="small" color="error" onClick={(e) => { e.stopPropagation(); setDeleting(c); }}>Delete</Button> },
        ]} />
      <ConfirmDialog open={!!deleting} title={`Delete ${deleting?.name}?`} message="The profile and its collections are removed from the app." onClose={() => setDeleting(null)}
        onConfirm={async () => { await api.del(`/admin/companies/${deleting!.id}`); notify('Company deleted'); await qc.invalidateQueries({ queryKey: ['admin', 'companies'] }); }} />
    </>
  );
}

export function CompanyEditorPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const notify = useNotify();
  const qc = useQueryClient();
  const techs = useTechnologies();
  const existing = useQuery({ queryKey: ['admin', 'company', id], queryFn: () => api.get<AdminCompany>(`/admin/companies/${id}`), enabled: !!id });
  const [c, setC] = useState<CompanyUpsert | null>(id ? null : {
    name: '', slug: null, overview: '', overviewSource: 'Illustrative', industry: null, logoUrl: null, websiteUrl: null, difficultyLevel: 'Intermediate',
    displayOrder: 1, isPremium: true, isActive: true, rounds: [], topics: [], collections: [],
  });
  const [error, setError] = useState<unknown>(null);
  const [saving, setSaving] = useState(false);
  useEffect(() => { if (existing.data) setC(existing.data); }, [existing.data]);
  if (id && existing.isLoading) return <Loading />;
  if (existing.error) return <ErrorAlert error={existing.error} onRetry={() => existing.refetch()} />;
  if (!c) return <Loading />;

  const fields: Field<CompanyUpsert>[] = [
    { name: 'name', label: 'Name', required: true, width: '48%' },
    { name: 'slug', label: 'Slug', nullable: true, width: '23%' },
    { name: 'industry', label: 'Industry', nullable: true, width: '23%' },
    { name: 'overview', label: 'Overview', type: 'textarea', rows: 4, required: true },
    { name: 'overviewSource', label: 'Overview source', type: 'select', options: ContentSources, width: '31%', helper: sourceHelp },
    { name: 'difficultyLevel', label: 'Difficulty', type: 'select', options: Difficulties, width: '31%' },
    { name: 'displayOrder', label: 'Order', type: 'number', width: '31%' },
    { name: 'websiteUrl', label: 'Careers website', nullable: true, width: '48%' },
    { name: 'logoUrl', label: 'Logo URL', nullable: true, width: '48%' },
    { name: 'isPremium', label: 'Premium only', type: 'switch' },
    { name: 'isActive', label: 'Active', type: 'switch' },
  ];
  const roundFields: Field<RoundUpsert>[] = [
    { name: 'name', label: 'Round', required: true, width: '30%' },
    { name: 'source', label: 'Source', type: 'select', options: ContentSources, width: '30%' },
    { name: 'sourceReference', label: 'Source reference (URL or citation)', nullable: true, width: '35%' },
    { name: 'description', label: 'Description', type: 'textarea', rows: 2, required: true },
  ];
  const topicFields: Field<CompanyTopicUpsert>[] = [
    { name: 'name', label: 'Topic', required: true, width: '30%' },
    { name: 'technologyId', label: 'Technology', type: 'select', options: toOptions(techs.data), nullable: true, width: '30%' },
    { name: 'description', label: 'Description', nullable: true, width: '35%' },
  ];
  const collectionFields: Field<CollectionUpsert>[] = [
    { name: 'title', label: 'Collection title', required: true, width: '48%' },
    { name: 'kind', label: 'Kind', type: 'select', options: CollectionKinds, width: '23%' },
    { name: 'source', label: 'Source', type: 'select', options: ContentSources, width: '23%' },
    { name: 'sourceReference', label: 'Source reference', nullable: true },
    { name: 'description', label: 'Description', nullable: true },
  ];
  const problems = sourceProblems(c);
  const save = async () => {
    setError(null);
    if (problems.length) { setError(new Error(problems.join(' '))); return; }
    setSaving(true);
    try {
      const saved = id ? await api.put<AdminCompany>(`/admin/companies/${id}`, c) : await api.post<AdminCompany>('/admin/companies', c);
      notify('Company saved');
      await qc.invalidateQueries({ queryKey: ['admin'] });
      if (!id) navigate(`/companies/${saved.id}`, { replace: true });
    } catch (e) { setError(e); } finally { setSaving(false); }
  };

  return (
    <>
      <PageHeader title={id ? `Edit ${c.name}` : 'New company'}
        actions={<><Button onClick={() => navigate('/companies')}>Back</Button><Button variant="contained" onClick={save} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button></>} />
      <Alert severity="info" sx={{ mb: 2 }}>
        Do not claim that questions were asked in real interviews unless you can cite a reliable public source. Original practice content must be marked Illustrative.
      </Alert>
      {error != null && <Alert severity="error" sx={{ mb: 2 }}>{errorMessage(error)}</Alert>}
      <Paper variant="outlined" sx={{ p: 3 }}>
        <FormFields fields={fields} value={c} onChange={setC} errors={error instanceof ApiError ? error : null} />
        <Divider sx={{ my: 3 }} />
        <ListEditor title="Interview rounds" items={c.rounds} fields={roundFields} addLabel="Add round"
          create={() => ({ name: '', description: '', source: 'Illustrative', sourceReference: null })} onChange={(rounds) => setC({ ...c, rounds })} />
        <Divider sx={{ my: 3 }} />
        <ListEditor title="Focus topics" items={c.topics} fields={topicFields} addLabel="Add topic"
          create={() => ({ name: '', description: null, technologyId: null })} onChange={(topics) => setC({ ...c, topics })} />
        <Divider sx={{ my: 3 }} />
        <ListEditor title="Practice collections" items={c.collections} addLabel="Add collection"
          create={() => ({ id: null, title: '', description: null, kind: 'Technical', source: 'Illustrative', sourceReference: null, questionIds: [], challengeIds: [] })}
          onChange={(collections) => setC({ ...c, collections })}
          render={(col, update) => (
            <Stack spacing={1.5}>
              <FormFields dense fields={collectionFields} value={col} onChange={update} />
              <QuestionPicker value={col.questionIds ?? []} onChange={(questionIds) => update({ ...col, questionIds })} />
              <ChallengePicker value={col.challengeIds ?? []} onChange={(challengeIds) => update({ ...col, challengeIds })} />
            </Stack>
          )} />
        {problems.length > 0 && <Typography color="warning.main" sx={{ mt: 2 }}>{problems.join(' ')}</Typography>}
      </Paper>
    </>
  );
}
