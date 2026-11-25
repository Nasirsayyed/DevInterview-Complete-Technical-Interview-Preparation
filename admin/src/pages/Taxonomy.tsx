import { useMemo, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Box, Button, Chip, IconButton, MenuItem, Stack, Tab, Tabs, TextField, Tooltip } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import DeleteIcon from '@mui/icons-material/DeleteOutline';
import { api } from '../api/client';
import { toOptions, useCategories, useTechnologies } from '../api/hooks';
import type { AdminCategory, AdminTechnology, AdminTopic } from '../api/types';
import { DataTable, PageHeader, useNotify } from '../components/Common';
import { ConfirmDialog, FormDialog, type Field } from '../components/Form';

type TechForm = { name: string; slug: string | null; description: string | null; group: string; iconKey: string | null; displayOrder: number; isActive: boolean };
type CatForm = { technologyId: string; name: string; slug: string | null; description: string | null; displayOrder: number; isActive: boolean };
type TopicForm = { categoryId: string; name: string; slug: string | null; displayOrder: number; isActive: boolean };

const techFields: Field<TechForm>[] = [
  { name: 'name', label: 'Name', required: true, width: '48%' },
  { name: 'slug', label: 'Slug', helper: 'Generated from the name when empty', nullable: true, width: '48%' },
  { name: 'group', label: 'Group', required: true, helper: 'e.g. Backend, Frontend, Databases', width: '48%' },
  { name: 'iconKey', label: 'Icon key', nullable: true, width: '30%' },
  { name: 'displayOrder', label: 'Order', type: 'number', width: 100 },
  { name: 'description', label: 'Description', type: 'textarea', nullable: true },
  { name: 'isActive', label: 'Active', type: 'switch' },
];

export default function TaxonomyPage() {
  const [tab, setTab] = useState(0);
  return (
    <>
      <PageHeader title="Technologies & categories" subtitle="The taxonomy questions, flashcards and paths are organised by." />
      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2 }}>
        <Tab label="Technologies" />
        <Tab label="Categories & topics" />
      </Tabs>
      {tab === 0 ? <Technologies /> : <Categories />}
    </>
  );
}

function Technologies() {
  const techs = useTechnologies();
  const qc = useQueryClient();
  const notify = useNotify();
  const [editing, setEditing] = useState<AdminTechnology | 'new' | null>(null);
  const [deleting, setDeleting] = useState<AdminTechnology | null>(null);
  const initial = useMemo<TechForm>(() => editing && editing !== 'new'
    ? { name: editing.name, slug: editing.slug, description: editing.description, group: editing.group, iconKey: editing.iconKey, displayOrder: editing.displayOrder, isActive: editing.isActive }
    : { name: '', slug: null, description: null, group: 'General', iconKey: null, displayOrder: (techs.data?.length ?? 0) + 1, isActive: true }, [editing, techs.data]);
  const refresh = () => qc.invalidateQueries({ queryKey: ['admin'] });

  return (
    <>
      <Box sx={{ mb: 2 }}><Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing('new')}>New technology</Button></Box>
      <DataTable rows={techs.data} loading={techs.isLoading} error={techs.error} onRetry={() => techs.refetch()} rowKey={(t) => t.id} columns={[
        { header: 'Order', render: (t) => t.displayOrder, width: 70 },
        { header: 'Name', render: (t) => t.name },
        { header: 'Group', render: (t) => t.group },
        { header: 'Categories', render: (t) => t.categoryCount, align: 'right' },
        { header: 'Questions', render: (t) => t.questionCount, align: 'right' },
        { header: 'Status', render: (t) => <Chip size="small" label={t.isActive ? 'Active' : 'Hidden'} color={t.isActive ? 'success' : 'default'} variant="outlined" /> },
        {
          header: '', align: 'right', render: (t) => (
            <>
              <Tooltip title="Edit"><IconButton size="small" onClick={() => setEditing(t)}><EditIcon fontSize="small" /></IconButton></Tooltip>
              <Tooltip title="Delete"><IconButton size="small" onClick={() => setDeleting(t)}><DeleteIcon fontSize="small" /></IconButton></Tooltip>
            </>
          ),
        },
      ]} />
      <FormDialog open={editing !== null} title={editing === 'new' ? 'New technology' : 'Edit technology'} initial={initial} fields={techFields}
        onClose={() => setEditing(null)}
        onSubmit={async (v) => {
          if (editing === 'new') await api.post('/admin/technologies', v); else await api.put(`/admin/technologies/${(editing as AdminTechnology).id}`, v);
          notify('Technology saved'); await refresh();
        }} />
      <ConfirmDialog open={!!deleting} title={`Delete ${deleting?.name}?`} onClose={() => setDeleting(null)}
        message="Technologies that still have questions cannot be deleted; hide them instead."
        onConfirm={async () => { await api.del(`/admin/technologies/${deleting!.id}`); notify('Deleted'); await refresh(); }} />
    </>
  );
}

function Categories() {
  const techs = useTechnologies();
  const [techId, setTechId] = useState<string>('');
  const cats = useCategories(techId || null);
  const qc = useQueryClient();
  const notify = useNotify();
  const [editing, setEditing] = useState<AdminCategory | 'new' | null>(null);
  const [topic, setTopic] = useState<{ category: AdminCategory; topic: AdminTopic | null } | null>(null);
  const [deleting, setDeleting] = useState<{ kind: 'category' | 'topic'; id: string; name: string } | null>(null);
  const refresh = () => qc.invalidateQueries({ queryKey: ['admin'] });
  const catFields: Field<CatForm>[] = [
    { name: 'technologyId', label: 'Technology', type: 'select', options: toOptions(techs.data), required: true, width: '48%' },
    { name: 'name', label: 'Name', required: true, width: '48%' },
    { name: 'slug', label: 'Slug', nullable: true, width: '48%' },
    { name: 'displayOrder', label: 'Order', type: 'number', width: 100 },
    { name: 'description', label: 'Description', type: 'textarea', nullable: true },
    { name: 'isActive', label: 'Active', type: 'switch' },
  ];
  const topicFields: Field<TopicForm>[] = [
    { name: 'name', label: 'Name', required: true, width: '60%' },
    { name: 'slug', label: 'Slug', nullable: true, width: '35%' },
    { name: 'displayOrder', label: 'Order', type: 'number', width: 100 },
    { name: 'isActive', label: 'Active', type: 'switch' },
  ];
  const catInitial = useMemo<CatForm>(() => editing && editing !== 'new'
    ? { technologyId: editing.technologyId, name: editing.name, slug: editing.slug, description: editing.description, displayOrder: editing.displayOrder, isActive: editing.isActive }
    : { technologyId: techId || techs.data?.[0]?.id || '', name: '', slug: null, description: null, displayOrder: 1, isActive: true }, [editing, techId, techs.data]);
  const topicInitial = useMemo<TopicForm>(() => topic?.topic
    ? { categoryId: topic.category.id, name: topic.topic.name, slug: topic.topic.slug, displayOrder: topic.topic.displayOrder, isActive: topic.topic.isActive }
    : { categoryId: topic?.category.id ?? '', name: '', slug: null, displayOrder: (topic?.category.topics.length ?? 0) + 1, isActive: true }, [topic]);

  return (
    <>
      <Stack direction="row" spacing={2} sx={{ mb: 2 }}>
        <TextField select size="small" label="Technology" value={techId} onChange={(e) => setTechId(e.target.value)} sx={{ minWidth: 220 }}>
          <MenuItem value="">All technologies</MenuItem>
          {techs.data?.map((t) => <MenuItem key={t.id} value={t.id}>{t.name}</MenuItem>)}
        </TextField>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing('new')} disabled={!techs.data?.length}>New category</Button>
      </Stack>
      <DataTable rows={cats.data} loading={cats.isLoading} error={cats.error} onRetry={() => cats.refetch()} rowKey={(c) => c.id} columns={[
        { header: 'Technology', render: (c) => c.technologyName },
        { header: 'Category', render: (c) => c.name },
        {
          header: 'Topics', render: (c) => (
            <Stack direction="row" gap={0.5} flexWrap="wrap">
              {c.topics.map((t) => <Chip key={t.id} size="small" label={t.name} onClick={() => setTopic({ category: c, topic: t })}
                onDelete={() => setDeleting({ kind: 'topic', id: t.id, name: t.name })} variant={t.isActive ? 'filled' : 'outlined'} />)}
              <Chip size="small" icon={<AddIcon />} label="Topic" variant="outlined" onClick={() => setTopic({ category: c, topic: null })} />
            </Stack>
          ),
        },
        { header: 'Questions', render: (c) => c.questionCount, align: 'right' },
        {
          header: '', align: 'right', render: (c) => (
            <>
              <IconButton size="small" aria-label="Edit category" onClick={() => setEditing(c)}><EditIcon fontSize="small" /></IconButton>
              <IconButton size="small" aria-label="Delete category" onClick={() => setDeleting({ kind: 'category', id: c.id, name: c.name })}><DeleteIcon fontSize="small" /></IconButton>
            </>
          ),
        },
      ]} />
      <FormDialog open={editing !== null} title={editing === 'new' ? 'New category' : 'Edit category'} initial={catInitial} fields={catFields}
        onClose={() => setEditing(null)}
        onSubmit={async (v) => {
          if (editing === 'new') await api.post('/admin/categories', v); else await api.put(`/admin/categories/${(editing as AdminCategory).id}`, v);
          notify('Category saved'); await refresh();
        }} />
      <FormDialog open={topic !== null} title={topic?.topic ? 'Edit topic' : `New topic in ${topic?.category.name ?? ''}`} initial={topicInitial} fields={topicFields}
        onClose={() => setTopic(null)}
        onSubmit={async (v) => {
          if (topic?.topic) await api.put(`/admin/topics/${topic.topic.id}`, v); else await api.post('/admin/topics', v);
          notify('Topic saved'); await refresh();
        }} />
      <ConfirmDialog open={!!deleting} title={`Delete ${deleting?.name}?`} onClose={() => setDeleting(null)}
        message={deleting?.kind === 'category' ? 'Categories that still contain questions cannot be deleted.' : 'Questions keep their category; the topic link is removed.'}
        onConfirm={async () => { await api.del(`/admin/${deleting!.kind === 'category' ? 'categories' : 'topics'}/${deleting!.id}`); notify('Deleted'); await refresh(); }} />
    </>
  );
}
