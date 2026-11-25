import { useState, type FormEvent } from 'react';
import { Alert, Box, Button, Paper, Stack, TextField, Typography } from '@mui/material';
import { useAuth } from '../auth/AuthContext';
import { errorMessage } from '../api/client';

export default function LoginPage() {
  const { login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (!email.trim() || !password) { setError('Enter your email and password.'); return; }
    setBusy(true); setError(null);
    try { await login(email, password); } catch (err) { setError(errorMessage(err)); } finally { setBusy(false); }
  };

  return (
    <Box sx={{ minHeight: '100vh', display: 'grid', placeItems: 'center', p: 2, background: 'linear-gradient(135deg,#4F46E5,#0EA5E9)' }}>
      <Paper component="form" onSubmit={submit} sx={{ p: 4, width: '100%', maxWidth: 400 }} noValidate>
        <Stack spacing={2}>
          <Box>
            <Typography variant="h5" fontWeight={700}>DevInterview Admin</Typography>
            <Typography color="text.secondary">Sign in with an administrator or content manager account.</Typography>
          </Box>
          {error && <Alert severity="error" role="alert">{error}</Alert>}
          <TextField label="Email" type="email" autoComplete="username" value={email} onChange={(e) => setEmail(e.target.value)} autoFocus />
          <TextField label="Password" type="password" autoComplete="current-password" value={password} onChange={(e) => setPassword(e.target.value)} />
          <Button type="submit" variant="contained" size="large" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</Button>
        </Stack>
      </Paper>
    </Box>
  );
}
