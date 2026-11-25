import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { Accordion, AccordionDetails, AccordionSummary, Alert, Button, Divider, Paper, Stack, Typography } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import { api, ApiError, errorMessage, type Paged } from '../api/client';
import { toOptions, useTechnologies } from '../api/hooks';
import {
  Difficulties, LessonTypes,
  type AdminLearningPath, type AdminLearningPathListItem, type AdminQuiz, type LearningPathUpsert, type LessonUpsert, type ModuleUpsert,
} from '../api/types';
import { DataTable, ErrorAlert, Loading, PageHeader, StatusChip, useNotify } from '../components/Common';
import { ConfirmDialog, FormFields, ListEditor, type Field } from '../components/Form';
import { QuestionPicker } from '../components/Pickers';

export function LearningPathsPage() {
  const navigate = useNavigate();
  const notify = useNotify();
  const qc = useQueryClient();
  const [deleting, setDeleting] = useState<AdminLearningPathListItem | null>(null);
  const list = useQuery({ queryKey: ['admin', 'paths'], queryFn: () => api.get<AdminLearningPathListItem[]>('/admin/learning-paths') });
  return (
    <>
      <PageHeader title="Learning paths" subtitle="Roadmaps of modules and lessons."
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={() => navigate('/learning-paths/new')}>New path</Button>} />
      <DataTable rows={list.data} loading={list.isLoading} error={list.error} onRetry={() => list.refetch()} rowKey={(p) => p.id}
        onRowClick={(p) => navigate(`/learning-paths/${p.id}`)} columns={[
          { header: 'Title', render: (p) => p.title },
          { header: 'Level', render: (p) => p.level },
          { header: 'Modules', render: (p) => p.moduleCount, align: 'right' },
          { header: 'Lessons', render: (p) => p.lessonCount, align: 'right' },
          { header: 'Status', render: (p) => <Stack direction="row" spacing={0.5}><StatusChip status={p.isPublished ? 'Published' : 'Draft'} />{p.isPremium && <StatusChip status="Premium" />}</Stack> },
          { header: '', align: 'right', render: (p) => <Button size="small" color="error" onClick={(e) => { e.stopPropagation(); setDeleting(p); }}>Delete</Button> },
        ]} />
      <ConfirmDialog open={!!deleting} title={`Delete “${deleting?.title}”?`} message="Learner progress on this path will no longer be shown." onClose={() => setDeleting(null)}
        onConfirm={async () => { await api.del(`/admin/learning-paths/${deleting!.id}`); notify('Path deleted'); await qc.invalidateQueries({ queryKey: ['admin', 'paths'] }); }} />
    </>
  );
}

const emptyLesson = (): LessonUpsert => ({ id: null, title: '', content: '', type: 'Reading', estimatedMinutes: 10, exerciseInstructions: null, quizId: null, questionIds: [] });
const emptyModule = (): ModuleUpsert => ({ id: null, title: '', description: null, lessons: [emptyLesson()] });

export function LearningPathEditorPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const notify = useNotify();
  const qc = useQueryClient();
  const techs = useTechnologies();
  const quizzes = useQuery({ queryKey: ['admin', 'quizzes', 'all'], queryFn: () => api.get<Paged<AdminQuiz>>('/admin/quizzes', { pageSize: 100 }) });
  const existing = useQuery({ queryKey: ['admin', 'path', id], queryFn: () => api.get<AdminLearningPath>(`/admin/learning-paths/${id}`), enabled: !!id });
  const [p, setP] = useState<LearningPathUpsert | null>(id ? null : {
    title: '', slug: null, description: '', level: 'Beginner', estimatedHours: 10, isPremium: false, isPublished: false, displayOrder: 1, technologyId: null, modules: [emptyModule()],
  });
  const [error, setError] = useState<unknown>(null);
  const [saving, setSaving] = useState(false);
  useEffect(() => { if (existing.data) setP(existing.data); }, [existing.data]);
  if (id && existing.isLoading) return <Loading />;
  if (existing.error) return <ErrorAlert error={existing.error} onRetry={() => existing.refetch()} />;
  if (!p) return <Loading />;

  const fields: Field<LearningPathUpsert>[] = [
    { name: 'title', label: 'Title', required: true, width: '60%' },
    { name: 'slug', label: 'Slug', nullable: true, width: '35%' },
    { name: 'description', label: 'Description', type: 'textarea', required: true },
    { name: 'level', label: 'Level', type: 'select', options: Difficulties, width: '23%' },
    { name: 'technologyId', label: 'Technology', type: 'select', options: toOptions(techs.data), nullable: true, width: '23%' },
    { name: 'estimatedHours', label: 'Estimated hours', type: 'number', width: '23%' },
    { name: 'displayOrder', label: 'Order', type: 'number', width: '23%' },
    { name: 'isPublished', label: 'Published', type: 'switch' },
    { name: 'isPremium', label: 'Premium only', type: 'switch' },
  ];
  const lessonFields: Field<LessonUpsert>[] = [
    { name: 'title', label: 'Lesson title', required: true, width: '50%' },
    { name: 'type', label: 'Type', type: 'select', options: LessonTypes, width: '22%' },
    { name: 'estimatedMinutes', label: 'Minutes', type: 'number', width: '22%' },
    { name: 'content', label: 'Content (Markdown)', type: 'textarea', rows: 6, required: true },
    { name: 'exerciseInstructions', label: 'Exercise instructions', type: 'textarea', nullable: true, hidden: (l) => l.type !== 'Exercise' },
    { name: 'quizId', label: 'Quiz', type: 'select', nullable: true, options: (quizzes.data?.items ?? []).map((q) => ({ value: q.id, label: q.title })), hidden: (l) => l.type !== 'Quiz' },
  ];
  const lessonCount = p.modules.reduce((n, m) => n + m.lessons.length, 0);
  const save = async () => {
    setError(null);
    if (p.modules.some((m) => !m.title.trim() || m.lessons.length === 0)) { setError(new Error('Every module needs a title and at least one lesson.')); return; }
    if (p.modules.some((m) => m.lessons.some((l) => l.type === 'Quiz' && !l.quizId))) { setError(new Error('Quiz lessons must link a quiz.')); return; }
    setSaving(true);
    try {
      const saved = id ? await api.put<AdminLearningPath>(`/admin/learning-paths/${id}`, p) : await api.post<AdminLearningPath>('/admin/learning-paths', p);
      notify('Learning path saved');
      await qc.invalidateQueries({ queryKey: ['admin'] });
      if (!id) navigate(`/learning-paths/${saved.id}`, { replace: true });
    } catch (e) { setError(e); } finally { setSaving(false); }
  };

  return (
    <>
      <PageHeader title={id ? 'Edit learning path' : 'New learning path'}
        subtitle={`${p.modules.length} modules · ${lessonCount} lessons${existing.data ? ` · ${existing.data.enrolledUsers} learners enrolled` : ''}`}
        actions={<><Button onClick={() => navigate('/learning-paths')}>Back</Button><Button variant="contained" onClick={save} disabled={saving}>{saving ? 'Saving…' : 'Save'}</Button></>} />
      {error != null && <Alert severity="error" sx={{ mb: 2 }}>{errorMessage(error)}</Alert>}
      <Paper variant="outlined" sx={{ p: 3 }}>
        <FormFields fields={fields} value={p} onChange={setP} errors={error instanceof ApiError ? error : null} />
        <Divider sx={{ my: 3 }} />
        <ListEditor title="Modules" items={p.modules} minItems={1} addLabel="Add module" create={emptyModule} onChange={(modules) => setP({ ...p, modules })}
          render={(m, update) => (
            <Stack spacing={1}>
              <FormFields dense value={m} onChange={update} fields={[
                { name: 'title', label: 'Module title', required: true, width: '48%' },
                { name: 'description', label: 'Description', nullable: true, width: '48%' },
              ]} />
              <Accordion disableGutters variant="outlined">
                <AccordionSummary expandIcon={<ExpandMoreIcon />}><Typography variant="body2">Lessons ({m.lessons.length})</Typography></AccordionSummary>
                <AccordionDetails>
                  <ListEditor title="Lessons" items={m.lessons} minItems={1} addLabel="Add lesson" create={emptyLesson} onChange={(lessons) => update({ ...m, lessons })}
                    render={(l, updateLesson) => (
                      <Stack spacing={1}>
                        <FormFields dense fields={lessonFields} value={l} onChange={updateLesson} />
                        <QuestionPicker label="Practice questions" value={l.questionIds ?? []} technologyId={p.technologyId} onChange={(questionIds) => updateLesson({ ...l, questionIds })} />
                      </Stack>
                    )} />
                </AccordionDetails>
              </Accordion>
            </Stack>
          )} />
      </Paper>
    </>
  );
}
