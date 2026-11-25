import { useEffect, useState } from 'react';
import { useQueries, useQuery } from '@tanstack/react-query';
import { Autocomplete, Box, Chip, Stack, TextField, Typography } from '@mui/material';
import { api, type Paged } from '../api/client';
import type { AdminChallenge, AdminChallengeListItem, AdminQuestion, AdminQuestionListItem } from '../api/types';

function useDebounced(value: string, ms = 300) {
  const [v, setV] = useState(value);
  useEffect(() => { const t = setTimeout(() => setV(value), ms); return () => clearTimeout(t); }, [value, ms]);
  return v;
}

/** Selects questions (by id) with server-side search; selected items show their titles. */
export function QuestionPicker({ label = 'Questions', value, onChange, technologyId, publishedOnly = true }: {
  label?: string; value: string[]; onChange: (ids: string[]) => void; technologyId?: string | null; publishedOnly?: boolean;
}) {
  const [input, setInput] = useState('');
  const search = useDebounced(input);
  const results = useQuery({
    queryKey: ['admin', 'question-search', search, technologyId, publishedOnly],
    queryFn: () => api.get<Paged<AdminQuestionListItem>>('/admin/questions', { search, technologyId: technologyId ?? undefined, status: publishedOnly ? 'Published' : undefined, pageSize: 25 }),
  });
  const details = useQueries({
    queries: value.map((id) => ({ queryKey: ['admin', 'question', id], queryFn: () => api.get<AdminQuestion>(`/admin/questions/${id}`), staleTime: 300_000 })),
  });
  const title = (id: string, i: number) => details[i]?.data?.title ?? results.data?.items.find((q) => q.id === id)?.title ?? 'Loading…';
  return (
    <Box>
      <Autocomplete options={(results.data?.items ?? []).filter((q) => !value.includes(q.id))} getOptionLabel={(q) => q.title}
        filterOptions={(x) => x} inputValue={input} onInputChange={(_, v, reason) => reason !== 'reset' && setInput(v)} value={null}
        onChange={(_, q) => { if (q) { onChange([...value, q.id]); setInput(''); } }} loading={results.isFetching}
        renderOption={(props, q) => <li {...props} key={q.id}><Stack><Typography variant="body2">{q.title}</Typography><Typography variant="caption" color="text.secondary">{q.technologyName} · {q.difficulty} · {q.status}</Typography></Stack></li>}
        renderInput={(p) => <TextField {...p} label={`Add ${label.toLowerCase()}`} helperText={publishedOnly ? 'Only published questions are listed.' : undefined} />} />
      <Typography variant="caption" color="text.secondary">{value.length} selected</Typography>
      <Stack direction="row" gap={0.5} flexWrap="wrap" sx={{ mt: 0.5 }}>
        {value.map((id, i) => <Chip key={id} size="small" label={`${i + 1}. ${title(id, i)}`} onDelete={() => onChange(value.filter((x) => x !== id))} />)}
      </Stack>
    </Box>
  );
}

export function ChallengePicker({ value, onChange }: { value: string[]; onChange: (ids: string[]) => void }) {
  const [input, setInput] = useState('');
  const search = useDebounced(input);
  const results = useQuery({
    queryKey: ['admin', 'challenge-search', search],
    queryFn: () => api.get<Paged<AdminChallengeListItem>>('/admin/challenges', { search, pageSize: 25 }),
  });
  const details = useQueries({
    queries: value.map((id) => ({ queryKey: ['admin', 'challenge', id], queryFn: () => api.get<AdminChallenge>(`/admin/challenges/${id}`), staleTime: 300_000 })),
  });
  return (
    <Box>
      <Autocomplete options={(results.data?.items ?? []).filter((c) => !value.includes(c.id))} getOptionLabel={(c) => c.title}
        filterOptions={(x) => x} inputValue={input} onInputChange={(_, v, reason) => reason !== 'reset' && setInput(v)} value={null}
        onChange={(_, c) => { if (c) { onChange([...value, c.id]); setInput(''); } }}
        renderInput={(p) => <TextField {...p} size="small" label="Add coding challenges" />} />
      <Stack direction="row" gap={0.5} flexWrap="wrap" sx={{ mt: 0.5 }}>
        {value.map((id, i) => <Chip key={id} size="small" label={details[i]?.data?.title ?? 'Loading…'} onDelete={() => onChange(value.filter((x) => x !== id))} />)}
      </Stack>
    </Box>
  );
}
