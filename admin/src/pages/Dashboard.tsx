import { useQuery } from '@tanstack/react-query';
import { Box, Paper, Stack, Typography } from '@mui/material';
import { CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { api } from '../api/client';
import type { AdminDashboard, ContentReport } from '../api/types';
import { DataTable, ErrorAlert, Loading, PageHeader, StatCard } from '../components/Common';

function Trend({ title, data }: { title: string; data: { date: string; value: number }[] }) {
  return (
    <Paper variant="outlined" sx={{ p: 2, flex: '1 1 420px', minWidth: 0 }}>
      <Typography variant="subtitle1" fontWeight={600} gutterBottom>{title}</Typography>
      {data.length === 0 ? <Typography color="text.secondary">No data yet.</Typography> : (
        <Box sx={{ height: 220 }}>
          <ResponsiveContainer>
            <LineChart data={data.map((d) => ({ ...d, label: d.date.slice(5) }))}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="label" fontSize={12} />
              <YAxis allowDecimals={false} fontSize={12} />
              <Tooltip />
              <Line type="monotone" dataKey="value" stroke="#4F46E5" strokeWidth={2} dot={false} />
            </LineChart>
          </ResponsiveContainer>
        </Box>
      )}
    </Paper>
  );
}

export default function DashboardPage() {
  const dash = useQuery({ queryKey: ['admin', 'dashboard'], queryFn: () => api.get<AdminDashboard>('/admin/dashboard') });
  const report = useQuery({ queryKey: ['admin', 'content-report'], queryFn: () => api.get<ContentReport>('/admin/reports/content') });
  if (dash.isLoading) return <Loading />;
  if (dash.error) return <ErrorAlert error={dash.error} onRetry={() => dash.refetch()} />;
  const d = dash.data!;
  return (
    <>
      <PageHeader title="Dashboard" subtitle="Live figures from the API database." />
      <Stack direction="row" flexWrap="wrap" gap={2} sx={{ mb: 3 }}>
        <StatCard label="Users" value={d.totalUsers.toLocaleString()} hint={`${d.newUsers7d} new in 7 days`} />
        <StatCard label="Active (7 days)" value={d.activeUsers7d.toLocaleString()} />
        <StatCard label="Premium users" value={d.premiumUsers.toLocaleString()} />
        <StatCard label="Questions" value={d.totalQuestions.toLocaleString()} hint={`${d.publishedQuestions} published · ${d.draftQuestions} draft`} />
        <StatCard label="Coding challenges" value={d.totalChallenges} />
        <StatCard label="Open reports" value={d.openReports} />
        <StatCard label="Quiz attempts (7d)" value={d.quizAttempts7d} />
        <StatCard label="Code submissions (7d)" value={d.submissions7d} />
        <StatCard label="Mock interviews (7d)" value={d.mockInterviews7d} />
        <StatCard label="AI calls (7d)" value={d.aiCalls7d} hint={`≈ $${Number(d.aiCost7d).toFixed(2)} estimated`} />
      </Stack>
      <Stack direction="row" flexWrap="wrap" gap={2} sx={{ mb: 3 }}>
        <Trend title="Sign-ups (last 30 days)" data={d.signupsTrend} />
        <Trend title="Active users per day" data={d.activityTrend} />
      </Stack>
      <Typography variant="h6" gutterBottom>Content performance</Typography>
      {report.error ? <ErrorAlert error={report.error} /> : (
        <Stack spacing={2}>
          <DataTable rows={report.data?.technologies} loading={report.isLoading} rowKey={(r) => r.technology} columns={[
            { header: 'Technology', render: (r) => r.technology },
            { header: 'Questions', render: (r) => r.questions, align: 'right' },
            { header: 'Attempts', render: (r) => r.attempts, align: 'right' },
            { header: 'Accuracy', render: (r) => `${Number(r.accuracy).toFixed(1)}%`, align: 'right' },
          ]} />
          <DataTable rows={report.data?.hardestQuestions} loading={report.isLoading} rowKey={(r) => r.questionId} empty="Not enough attempts yet." columns={[
            { header: 'Hardest questions', render: (r) => r.title },
            { header: 'Attempts', render: (r) => r.attempts, align: 'right' },
            { header: 'Accuracy', render: (r) => `${Number(r.accuracy).toFixed(1)}%`, align: 'right' },
          ]} />
          <DataTable rows={report.data?.challenges} loading={report.isLoading} rowKey={(r) => r.challengeId} columns={[
            { header: 'Challenge', render: (r) => r.title },
            { header: 'Submissions', render: (r) => r.submissions, align: 'right' },
            { header: 'Acceptance', render: (r) => `${Number(r.acceptanceRate).toFixed(1)}%`, align: 'right' },
          ]} />
        </Stack>
      )}
    </>
  );
}
