import { useMemo, useState } from 'react';
import { keepPreviousData, useQuery, useQueryClient } from '@tanstack/react-query';
import { Box, Button, Stack } from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { api, type Paged } from '../api/client';
import { toOptions, useTechnologies } from '../api/hooks';
import { Difficulties, QuizModes, type AdminQuiz, type QuizUpsert } from '../api/types';
import { DataTable, PageHeader, StatusChip, formatDate, useNotify } from '../components/Common';
import { ConfirmDialog, FormDialog, type Field } from '../components/Form';
import { QuestionPicker } from '../components/Pickers';

type QuizForm = Omit<QuizUpsert, 'timeLimitSeconds' | 'negativeMarkingFraction'> & { timeLimitMinutes: number | null; negativeMarkingPercent: number };

const toForm = (q?: AdminQuiz): QuizForm => q
  ? { ...q, timeLimitMinutes: q.timeLimitSeconds ? Math.round(q.timeLimitSeconds / 60) : null, negativeMarkingPercent: Math.round(Number(q.negativeMarkingFraction) * 100) }
  : { title: '', description: null, mode: 'Technology', timeLimitMinutes: 10, passingPercentage: 60, negativeMarkingPercent: 0, isPublished: false, isPremium: false, technologyId: null, difficulty: null, questionIds: [] };

export const fromForm = ({ timeLimitMinutes, negativeMarkingPercent, ...rest }: QuizForm): QuizUpsert => ({
  ...rest, timeLimitSeconds: timeLimitMinutes ? timeLimitMinutes * 60 : null, negativeMarkingFraction: negativeMarkingPercent / 100,
});

export default function QuizzesPage() {
  const [page, setPage] = useState({ page: 1, pageSize: 20 });
  const [editing, setEditing] = useState<AdminQuiz | 'new' | null>(null);
  const [deleting, setDeleting] = useState<AdminQuiz | null>(null);
  const techs = useTechnologies();
  const notify = useNotify();
  const qc = useQueryClient();
  const list = useQuery({ queryKey: ['admin', 'quizzes', page], queryFn: () => api.get<Paged<AdminQuiz>>('/admin/quizzes', page), placeholderData: keepPreviousData });
  const initial = useMemo(() => toForm(editing && editing !== 'new' ? editing : undefined), [editing]);
  const fields: Field<QuizForm>[] = [
    { name: 'title', label: 'Title', required: true },
    { name: 'description', label: 'Description', type: 'textarea', nullable: true },
    { name: 'mode', label: 'Mode', type: 'select', options: QuizModes, width: '31%' },
    { name: 'technologyId', label: 'Technology', type: 'select', options: toOptions(techs.data), nullable: true, width: '31%' },
    { name: 'difficulty', label: 'Difficulty', type: 'select', options: Difficulties, nullable: true, width: '31%' },
    { name: 'timeLimitMinutes', label: 'Time limit (minutes, empty = untimed)', type: 'number', nullable: true, width: '31%' },
    { name: 'passingPercentage', label: 'Pass mark (%)', type: 'number', width: '31%' },
    { name: 'negativeMarkingPercent', label: 'Negative marking (% per wrong answer)', type: 'number', width: '31%' },
    { name: 'isPublished', label: 'Published', type: 'switch' },
    { name: 'isPremium', label: 'Premium only', type: 'switch' },
  ];
  return (
    <>
      <PageHeader title="Quizzes" subtitle="Curated quizzes. Learners can also generate their own from the question bank."
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing('new')}>New quiz</Button>} />
      <DataTable rows={list.data?.items} loading={list.isLoading} error={list.error} onRetry={() => list.refetch()} rowKey={(q) => q.id}
        page={list.data} onPage={(p, s) => setPage({ page: p, pageSize: s })} onRowClick={(q) => setEditing(q)}
        columns={[
          { header: 'Title', render: (q) => q.title },
          { header: 'Mode', render: (q) => q.mode },
          { header: 'Questions', render: (q) => q.questionIds.length, align: 'right' },
          { header: 'Time', render: (q) => (q.timeLimitSeconds ? `${Math.round(q.timeLimitSeconds / 60)} min` : 'Untimed') },
          { header: 'Attempts', render: (q) => q.attemptCount, align: 'right' },
          { header: 'Status', render: (q) => <Stack direction="row" spacing={0.5}><StatusChip status={q.isPublished ? 'Published' : 'Draft'} />{q.isPremium && <StatusChip status="Premium" />}</Stack> },
          { header: 'Updated', render: (q) => formatDate(q.updatedAt) },
          { header: '', align: 'right', render: (q) => <Button size="small" color="error" onClick={(e) => { e.stopPropagation(); setDeleting(q); }}>Delete</Button> },
        ]} />
      <FormDialog open={editing !== null} title={editing === 'new' ? 'New quiz' : 'Edit quiz'} initial={initial} fields={fields} onClose={() => setEditing(null)}
        onSubmit={async (v) => {
          if (v.questionIds.length === 0) throw new Error('Add at least one question.');
          const body = fromForm(v);
          if (editing === 'new') await api.post('/admin/quizzes', body); else await api.put(`/admin/quizzes/${(editing as AdminQuiz).id}`, body);
          notify('Quiz saved'); await qc.invalidateQueries({ queryKey: ['admin', 'quizzes'] });
        }}>
        {(v, setV) => <Box sx={{ mt: 2 }}><QuestionPicker value={v.questionIds} technologyId={v.technologyId} onChange={(questionIds) => setV({ ...v, questionIds })} /></Box>}
      </FormDialog>
      <ConfirmDialog open={!!deleting} title={`Delete “${deleting?.title}”?`} message="Past attempts and results are kept for learners' history." onClose={() => setDeleting(null)}
        onConfirm={async () => { await api.del(`/admin/quizzes/${deleting!.id}`); notify('Quiz deleted'); await qc.invalidateQueries({ queryKey: ['admin', 'quizzes'] }); }} />
    </>
  );
}
