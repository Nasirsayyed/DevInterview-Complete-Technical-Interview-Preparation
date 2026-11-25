import { useEffect, useState, type ReactNode } from 'react';
import {
  Alert, Autocomplete, Box, Button, Checkbox, Chip, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, IconButton,
  MenuItem, Paper, Stack, Switch, TextField, Tooltip, Typography,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/DeleteOutline';
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward';
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward';
import { ApiError, errorMessage } from '../api/client';
import { humanize } from '../api/types';

/** Declarative field definition used by FormFields, FormDialog and ListEditor. */
export type Field<T> = {
  name: keyof T & string;
  label: string;
  type?: 'text' | 'textarea' | 'number' | 'select' | 'switch' | 'checkbox' | 'tags' | 'multiselect' | 'code';
  options?: readonly string[] | { value: string; label: string }[];
  required?: boolean;
  helper?: string;
  rows?: number;
  nullable?: boolean;
  width?: number | string;
  hidden?: (value: T) => boolean;
};

const optionList = (o: Field<never>['options']) =>
  (o ?? []).map((x) => (typeof x === 'string' ? { value: x, label: humanize(x) } : x));

export function FormFields<T extends object>({ fields, value, onChange, errors, dense }: {
  fields: Field<T>[]; value: T; onChange: (v: T) => void; errors?: ApiError | null; dense?: boolean;
}) {
  const set = (name: string, v: unknown) => onChange({ ...value, [name]: v } as T);
  return (
    <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2, alignItems: 'flex-start' }}>
      {fields.filter((f) => !f.hidden?.(value)).map((f) => {
        const raw = (value as Record<string, unknown>)[f.name];
        const error = errors?.field(f.name);
        const common = {
          label: f.label, required: f.required, error: !!error, helperText: error ?? f.helper, size: dense ? 'small' as const : 'medium' as const,
          sx: { flex: f.width ? `0 0 ${typeof f.width === 'number' ? `${f.width}px` : f.width}` : '1 1 100%', minWidth: 160 },
        };
        switch (f.type) {
          case 'switch':
          case 'checkbox': {
            const Control = f.type === 'switch' ? Switch : Checkbox;
            return <FormControlLabel key={f.name} label={f.label} sx={{ flex: '0 0 auto' }} control={<Control checked={!!raw} onChange={(e) => set(f.name, e.target.checked)} />} />;
          }
          case 'select':
            return (
              <TextField key={f.name} {...common} select value={(raw as string | null) ?? ''} onChange={(e) => set(f.name, e.target.value === '' && f.nullable ? null : e.target.value)}>
                {f.nullable && <MenuItem value=""><em>None</em></MenuItem>}
                {optionList(f.options).map((o) => <MenuItem key={o.value} value={o.value}>{o.label}</MenuItem>)}
              </TextField>
            );
          case 'multiselect': {
            const opts = optionList(f.options);
            const selected = opts.filter((o) => ((raw as string[]) ?? []).includes(o.value));
            return (
              <Autocomplete key={f.name} multiple options={opts} value={selected} sx={common.sx} size={common.size}
                getOptionLabel={(o) => o.label} isOptionEqualToValue={(a, b) => a.value === b.value}
                onChange={(_, v) => set(f.name, v.map((o) => o.value))}
                renderInput={(p) => <TextField {...p} label={f.label} error={!!error} helperText={error ?? f.helper} />} />
            );
          }
          case 'tags':
            return (
              <Autocomplete key={f.name} multiple freeSolo options={[] as string[]} value={(raw as string[]) ?? []} sx={common.sx} size={common.size}
                onChange={(_, v) => set(f.name, v.map((s) => s.trim()).filter(Boolean))}
                renderTags={(v, getTagProps) => v.map((t, i) => <Chip {...getTagProps({ index: i })} key={t} label={t} size="small" />)}
                renderInput={(p) => <TextField {...p} label={f.label} error={!!error} helperText={error ?? f.helper ?? 'Press Enter to add'} />} />
            );
          case 'number':
            return <TextField key={f.name} {...common} type="number" value={raw ?? ''} onChange={(e) => set(f.name, e.target.value === '' ? (f.nullable ? null : 0) : Number(e.target.value))} />;
          case 'textarea':
          case 'code':
            return (
              <TextField key={f.name} {...common} multiline minRows={f.rows ?? 3} value={(raw as string | null) ?? ''}
                slotProps={f.type === 'code' ? { htmlInput: { style: { fontFamily: 'ui-monospace, Menlo, Consolas, monospace', fontSize: 13 }, spellCheck: false } } : undefined}
                onChange={(e) => set(f.name, e.target.value === '' && f.nullable ? null : e.target.value)} />
            );
          default:
            return <TextField key={f.name} {...common} value={(raw as string | null) ?? ''} onChange={(e) => set(f.name, e.target.value === '' && f.nullable ? null : e.target.value)} />;
        }
      })}
    </Box>
  );
}

/** Editable list of sub-records (options, rounds, test cases, lessons…) with add, remove and reorder. */
export function ListEditor<T extends object>({ title, items, onChange, fields, create, render, addLabel = 'Add', minItems = 0, errors }: {
  title: string; items: T[]; onChange: (items: T[]) => void; fields?: Field<T>[]; create: () => T;
  render?: (item: T, update: (v: T) => void, index: number) => ReactNode; addLabel?: string; minItems?: number; errors?: ApiError | null;
}) {
  const update = (i: number, v: T) => onChange(items.map((x, j) => (j === i ? v : x)));
  const move = (i: number, d: number) => {
    const next = [...items]; const [x] = next.splice(i, 1); next.splice(i + d, 0, x); onChange(next);
  };
  return (
    <Box>
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 1 }}>
        <Typography variant="subtitle1" fontWeight={600}>{title} ({items.length})</Typography>
        <Button startIcon={<AddIcon />} size="small" onClick={() => onChange([...items, create()])}>{addLabel}</Button>
      </Stack>
      <Stack spacing={1.5}>
        {items.map((item, i) => (
          <Paper key={i} variant="outlined" sx={{ p: 1.5 }}>
            <Stack direction="row" spacing={1} alignItems="flex-start">
              <Typography variant="caption" sx={{ mt: 1, minWidth: 20 }}>{i + 1}.</Typography>
              <Box sx={{ flex: 1 }}>
                {render ? render(item, (v) => update(i, v), i) : <FormFields dense fields={fields ?? []} value={item} onChange={(v) => update(i, v)} errors={errors} />}
              </Box>
              <Stack>
                <Tooltip title="Move up"><span><IconButton size="small" disabled={i === 0} onClick={() => move(i, -1)}><ArrowUpwardIcon fontSize="small" /></IconButton></span></Tooltip>
                <Tooltip title="Move down"><span><IconButton size="small" disabled={i === items.length - 1} onClick={() => move(i, 1)}><ArrowDownwardIcon fontSize="small" /></IconButton></span></Tooltip>
                <Tooltip title="Remove"><span><IconButton size="small" disabled={items.length <= minItems} onClick={() => onChange(items.filter((_, j) => j !== i))}><DeleteIcon fontSize="small" /></IconButton></span></Tooltip>
              </Stack>
            </Stack>
          </Paper>
        ))}
      </Stack>
    </Box>
  );
}

/** Modal form for simple records. `onSubmit` errors (validation details included) are shown inline. */
export function FormDialog<T extends object>({ open, title, initial, fields, onClose, onSubmit, submitLabel = 'Save', children, onValueChange }: {
  open: boolean; title: string; initial: T; fields: Field<T>[]; onClose: () => void; onSubmit: (v: T) => Promise<unknown>; submitLabel?: string;
  children?: (value: T, setValue: (v: T) => void) => ReactNode; onValueChange?: (v: T) => void;
}) {
  const [value, setRawValue] = useState(initial);
  const setValue = (v: T) => { setRawValue(v); onValueChange?.(v); };
  const [error, setError] = useState<unknown>(null);
  const [saving, setSaving] = useState(false);
  useEffect(() => { if (open) { setValue(initial); setError(null); } }, [open, initial]);
  const submit = async () => {
    setSaving(true); setError(null);
    try { await onSubmit(value); onClose(); } catch (e) { setError(e); } finally { setSaving(false); }
  };
  return (
    <Dialog open={open} onClose={saving ? undefined : onClose} maxWidth="md" fullWidth>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent dividers>
        {error != null && <Alert severity="error" sx={{ mb: 2 }}>{errorMessage(error)}</Alert>}
        <FormFields fields={fields} value={value} onChange={setValue} errors={error instanceof ApiError ? error : null} />
        {children?.(value, setValue)}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={saving}>Cancel</Button>
        <Button variant="contained" onClick={submit} disabled={saving}>{saving ? 'Saving…' : submitLabel}</Button>
      </DialogActions>
    </Dialog>
  );
}

export function ConfirmDialog({ open, title, message, confirmLabel = 'Delete', onConfirm, onClose }: {
  open: boolean; title: string; message: ReactNode; confirmLabel?: string; onConfirm: () => Promise<unknown> | void; onClose: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  useEffect(() => { if (open) setError(null); }, [open]);
  return (
    <Dialog open={open} onClose={busy ? undefined : onClose}>
      <DialogTitle>{title}</DialogTitle>
      <DialogContent>
        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}
        <Typography component="div">{message}</Typography>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Cancel</Button>
        <Button color="error" variant="contained" disabled={busy}
          onClick={async () => { setBusy(true); try { await onConfirm(); onClose(); } catch (e) { setError(errorMessage(e)); } finally { setBusy(false); } }}>
          {confirmLabel}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
