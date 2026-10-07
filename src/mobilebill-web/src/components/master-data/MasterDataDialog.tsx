import { Alert, Autocomplete, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material'

// data: the record behind the option, for filling in other fields when it is chosen.
export type FormFieldOption = { value: string; label: string; parentValue?: string; data?: Record<string, unknown> }
export type FormField = {
  key: string
  label: string
  type?: 'text' | 'number' | 'date' | 'select'
  required?: boolean
  options?: FormFieldOption[]
  dependsOn?: string
  disabled?: boolean
  // Shown under the field when it has no error.
  helperText?: string
}

type MasterDataDialogProps = {
  open: boolean
  title: string
  fields: FormField[]
  values: Record<string, string>
  errors: Record<string, string>
  onChange: (key: string, value: string) => void
  onCancel: () => void
  onSave: () => void
  saveLabel?: string
}

export function MasterDataDialog({ open, title, fields, values, errors, onChange, onCancel, onSave, saveLabel = 'Save' }: MasterDataDialogProps) {
  return <Dialog open={open} onClose={onCancel} fullWidth maxWidth="sm">
    <DialogTitle>{title}</DialogTitle>
    <DialogContent>
      <Stack spacing={2} sx={{ pt: 1 }}>
        {fields.map(field => field.type === 'select'
          ? <Autocomplete
              key={field.key}
              options={field.options ?? []}
              value={field.options?.find(option => option.value === values[field.key]) ?? null}
              onChange={(_, option) => onChange(field.key, option?.value ?? '')}
              getOptionLabel={option => option.label}
              isOptionEqualToValue={(option, selected) => option.value === selected.value}
              disabled={field.disabled}
              renderInput={params => <TextField {...params} label={field.label} required={field.required} error={Boolean(errors[field.key])} helperText={errors[field.key]} />}
            />
          : <TextField
              key={field.key}
              label={field.label}
              type={field.type ?? 'text'}
              required={field.required}
              disabled={field.disabled}
              value={values[field.key] ?? ''}
              onChange={event => onChange(field.key, event.target.value)}
              error={Boolean(errors[field.key])}
              helperText={errors[field.key] ?? field.helperText}
              slotProps={{ inputLabel: field.type === 'date' ? { shrink: true } : undefined, htmlInput: field.type === 'number' ? { min: 0, step: '0.01' } : undefined }}
            />)}
        {errors.form && <Alert severity="error">{errors.form}</Alert>}
      </Stack>
    </DialogContent>
    <DialogActions><Button onClick={onCancel}>Cancel</Button><Button variant="contained" onClick={onSave}>{saveLabel}</Button></DialogActions>
  </Dialog>
}
