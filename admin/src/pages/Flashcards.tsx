import { useMemo, useState } from 'react';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { Button, MenuItem, Stack, TextField } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { api, type Paged } from '../api/client';
import { toOptions, useCategories, useTechnologies } from '../api/hooks';
import { Difficulties, type AdminFlashcard, type FlashcardUpsert } from '../api/types';
import { DataTable, PageHeader, StatusChip, useNotify } from '../components/Common';
import { ConfirmDialog, FormDialog, type Field } from '../components/Form';

export default function FlashcardsPage() {
  const [technologyId, setTechnologyId] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState({ page: 1, pageSize: 20 });
  const [editing, setEditing] = useState<AdminFlashcard | 'new' | null>(null);
  const [deleting, setDeleting] = useState<AdminFlashcard | null>(null);
  const techs = useTechnologies();
  const notify = useNotify();
  const qc = useQueryClient();
  const list = useQuery({
    queryKey: ['admin', 'flashcards', technologyId, search, page],
    queryFn: () => api.get<Paged<AdminFlashcard>>('/admin/flashcards', { technologyId: technologyId || undefined, search, ...page }),
    placeholderData: keepPreviousData,
  });
  const initial = useMemo<FlashcardUpsert>(() => editing && editing !== 'new'
    ? { technologyId: editing.technologyId, categoryId: editing.categoryId, front: editing.front, back: editing.back, difficulty: editing.difficulty, isPublished: editing.isPublished }
    : { technologyId: technologyId || null, categoryId: null, front: '', back: '', difficulty: 'Beginner', isPublished: true }, [editing, technologyId]);
  const [formTech, setFormTech] = useState<string | null>(null);
  const cats = useCategories(formTech);
  const fields: Field<FlashcardUpsert>[] = [
    { name: 'technologyId', label: 'Technology', type: 'select', options: toOptions(techs.data), nullable: true, width: '48%' },
    { name: 'categoryId', label: 'Category', type: 'select', options: toOptions(cats.data?.filter((c) => !formTech || c.technologyId === formTech)), nullable: true, width: '48%' },
    { name: 'front', label: 'Front (prompt)', type: 'textarea', required: true, rows: 2 },
    { name: 'back', label: 'Back (answer, Markdown)', type: 'textarea', required: true, rows: 4 },
    { name: 'difficulty', label: 'Difficulty', type: 'select', options: Difficulties, width: '48%' },
    { name: 'isPublished', label: 'Published', type: 'switch' },
  ];
  return (
    <>
      <PageHeader title="Flashcards" subtitle="Spaced-repetition cards; learners' schedules are computed by the server."
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={() => { setFormTech(technologyId || null); setEditing('new'); }}>New flashcard</Button>} />
      <Stack direction="row" spacing={2} sx={{ mb: 2 }}>
        <TextField size="small" label="Search" value={search} onChange={(e) => { setSearch(e.target.value); setPage({ ...page, page: 1 }); }} />
        <TextField select size="small" label="Technology" value={technologyId} onChange={(e) => { setTechnologyId(e.target.value); setPage({ ...page, page: 1 }); }} sx={{ minWidth: 200 }}>
          <MenuItem value="">All</MenuItem>{techs.data?.map((t) => <MenuItem key={t.id} value={t.id}>{t.name}</MenuItem>)}
        </TextField>
      </Stack>
      <DataTable rows={list.data?.items} loading={list.isLoading} error={list.error} onRetry={() => list.refetch()} rowKey={(f) => f.id}
        page={list.data} onPage={(p, s) => setPage({ page: p, pageSize: s })} onRowClick={(f) => { setFormTech(f.technologyId); setEditing(f); }}
        columns={[
          { header: 'Front', render: (f) => f.front },
          { header: 'Technology', render: (f) => f.technologyName ?? '—' },
          { header: 'Difficulty', render: (f) => f.difficulty },
          { header: 'Status', render: (f) => <StatusChip status={f.isPublished ? 'Published' : 'Draft'} /> },
          { header: '', align: 'right', render: (f) => <Button size="small" color="error" onClick={(e) => { e.stopPropagation(); setDeleting(f); }}>Delete</Button> },
        ]} />
      <FormDialog open={editing !== null} title={editing === 'new' ? 'New flashcard' : 'Edit flashcard'} initial={initial} fields={fields} onClose={() => setEditing(null)}
        onSubmit={async (v) => {
          if (editing === 'new') await api.post('/admin/flashcards', v); else await api.put(`/admin/flashcards/${(editing as AdminFlashcard).id}`, v);
          notify('Flashcard saved'); await qc.invalidateQueries({ queryKey: ['admin', 'flashcards'] });
        }}
        onValueChange={(v) => setFormTech(v.technologyId)} />
      <ConfirmDialog open={!!deleting} title="Delete flashcard?" message={deleting?.front} onClose={() => setDeleting(null)}
        onConfirm={async () => { await api.del(`/admin/flashcards/${deleting!.id}`); notify('Deleted'); await qc.invalidateQueries({ queryKey: ['admin', 'flashcards'] }); }} />
    </>
  );
}
