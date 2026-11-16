import { Alert, Box, Button, FormControl, InputLabel, MenuItem, Select, Stack, TextField } from '@mui/material'
import { useState, type FormEvent } from 'react'

import { allRoles, createUser, roleLabels, type UserRole } from '../../api/authApi'
import { useAuth } from '../../auth/AuthContext'
import { ErrorState } from '../../components/common/ErrorState'

const empty = { displayName: '', email: '', role: '' as UserRole | '', password: '', confirm: '' }

// Creates an account directly (User Management Create permission). Unlike self-registration it is active at once.
// Only an Administrator can create another Administrator.
export function CreateUserPanel({ onCreated }: { onCreated?: () => void }) {
  const { user } = useAuth()
  const roleOptions = user?.role === 'Administrator' ? allRoles : allRoles.filter(option => option.value !== 'Administrator')
  const [form, setForm] = useState(empty)
  const [error, setError] = useState<string>()
  const [notice, setNotice] = useState<string>()
  const [busy, setBusy] = useState(false)

  const set = (key: keyof typeof empty, value: string) => setForm(current => ({ ...current, [key]: value }))

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setError(undefined)
    setNotice(undefined)
    if (!form.role) { setError('Choose a role.'); return }
    if (form.password.length < 8) { setError('Password must be at least 8 characters.'); return }
    if (form.password !== form.confirm) { setError('The passwords do not match.'); return }
    setBusy(true)
    try {
      const created = await createUser({ displayName: form.displayName.trim(), email: form.email.trim(), role: form.role, password: form.password })
      setNotice(`${created.displayName} (${created.email}) can now sign in as ${roleLabels[created.role]}.`)
      setForm(empty)
      onCreated?.()
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to create the user.') }
    finally { setBusy(false) }
  }

  return (
    <Box component="form" onSubmit={event => void submit(event)} sx={{ maxWidth: 520 }}>
      <Stack spacing={2}>
        {error && <ErrorState message={error} />}
        {notice && <Alert severity="success">{notice}</Alert>}
        <TextField required label="Full Name" value={form.displayName} onChange={event => set('displayName', event.target.value)} slotProps={{ htmlInput: { maxLength: 200 } }} />
        <TextField required label="Email" type="email" autoComplete="off" value={form.email} onChange={event => set('email', event.target.value)} slotProps={{ htmlInput: { maxLength: 256 } }} />
        <FormControl required>
          <InputLabel id="create-user-role">Role</InputLabel>
          <Select labelId="create-user-role" label="Role" value={form.role} onChange={event => set('role', event.target.value)}>
            {roleOptions.map(option => <MenuItem key={option.value} value={option.value}>{option.label}</MenuItem>)}
          </Select>
        </FormControl>
        <TextField required label="Password" type="password" autoComplete="new-password" helperText="At least 8 characters. Give it to the user to sign in." value={form.password} onChange={event => set('password', event.target.value)} slotProps={{ htmlInput: { maxLength: 128 } }} />
        <TextField required label="Confirm Password" type="password" autoComplete="new-password" value={form.confirm} onChange={event => set('confirm', event.target.value)} slotProps={{ htmlInput: { maxLength: 128 } }} />
        <Box><Button type="submit" variant="contained" disabled={busy}>{busy ? 'Creating…' : 'Create User'}</Button></Box>
      </Stack>
    </Box>
  )
}
