import AutorenewOutlined from '@mui/icons-material/AutorenewOutlined'
import ContentCopyOutlined from '@mui/icons-material/ContentCopyOutlined'
import { Alert, Box, Button, FormControl, IconButton, InputAdornment, InputLabel, MenuItem, Select, Stack, TextField, Tooltip, Typography } from '@mui/material'
import { useState, type FormEvent } from 'react'

import { allRoles, createUser, roleLabels, type UserRole } from '../../api/authApi'
import { useAuth } from '../../auth/AuthContext'
import { ErrorState } from '../../components/common/ErrorState'
import { generateOneTimePassword } from '../../utils/oneTimePassword'

const blank = () => ({ displayName: '', email: '', role: '' as UserRole | '', password: generateOneTimePassword() })
type Created = { displayName: string; email: string; role: UserRole; password: string }

async function copy(text: string) {
  try { await navigator.clipboard.writeText(text); return true }
  catch { return false }
}

// Creates an account directly (User Management Create permission). Unlike self-registration it is active at once,
// with a one-time password the user must replace at the first sign-in. Only an Administrator can create another Administrator.
export function CreateUserPanel({ onCreated }: { onCreated?: () => void }) {
  const { user } = useAuth()
  const roleOptions = user?.role === 'Administrator' ? allRoles : allRoles.filter(option => option.value !== 'Administrator')
  const [form, setForm] = useState(blank)
  const [error, setError] = useState<string>()
  const [created, setCreated] = useState<Created>()
  const [copied, setCopied] = useState<'form' | 'notice'>()
  const [busy, setBusy] = useState(false)

  const set = (key: 'displayName' | 'email' | 'role' | 'password', value: string) => {
    setForm(current => ({ ...current, [key]: value }))
    if (key === 'password') setCopied(undefined)
  }

  const copyPassword = async (text: string, where: 'form' | 'notice') => {
    if (await copy(text)) setCopied(where)
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setError(undefined)
    setCreated(undefined)
    if (!form.role) { setError('Choose a role.'); return }
    if (form.password.length < 8) { setError('The one-time password must be at least 8 characters.'); return }
    setBusy(true)
    try {
      const result = await createUser({ displayName: form.displayName.trim(), email: form.email.trim(), role: form.role, password: form.password })
      setCreated({ displayName: result.displayName, email: result.email, role: result.role, password: form.password })
      setForm(blank())
      setCopied(undefined)
      onCreated?.()
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to create the user.') }
    finally { setBusy(false) }
  }

  return (
    <Box component="form" onSubmit={event => void submit(event)} sx={{ maxWidth: 520 }}>
      <Stack spacing={2}>
        {error && <ErrorState message={error} />}
        {created && (
          <Alert severity="success" action={
            <Button color="inherit" size="small" startIcon={<ContentCopyOutlined />} onClick={() => void copyPassword(created.password, 'notice')}>
              {copied === 'notice' ? 'Copied' : 'Copy'}
            </Button>
          }>
            <Typography variant="body2">{created.displayName} ({created.email}) was created as {roleLabels[created.role]}.</Typography>
            <Typography variant="body2">
              One-time password: <Box component="span" sx={{ fontFamily: 'monospace', fontWeight: 700, letterSpacing: 0.5 }}>{created.password}</Box>
            </Typography>
            <Typography variant="body2">Give it to the user. They must choose their own password the first time they sign in.</Typography>
          </Alert>
        )}
        <TextField required label="Full Name" value={form.displayName} onChange={event => set('displayName', event.target.value)} slotProps={{ htmlInput: { maxLength: 200 } }} />
        <TextField required label="Email" type="email" autoComplete="off" value={form.email} onChange={event => set('email', event.target.value)} slotProps={{ htmlInput: { maxLength: 256 } }} />
        <FormControl required>
          <InputLabel id="create-user-role">Role</InputLabel>
          <Select labelId="create-user-role" label="Role" value={form.role} onChange={event => set('role', event.target.value)}>
            {roleOptions.map(option => <MenuItem key={option.value} value={option.value}>{option.label}</MenuItem>)}
          </Select>
        </FormControl>
        <TextField
          required
          label="One-Time Password"
          autoComplete="off"
          helperText="At least 8 characters. The user must replace it with their own password at the first sign-in."
          value={form.password}
          onChange={event => set('password', event.target.value)}
          slotProps={{
            htmlInput: { maxLength: 128, spellCheck: false, style: { fontFamily: 'monospace', letterSpacing: 0.5 } },
            input: {
              endAdornment: (
                <InputAdornment position="end">
                  <Tooltip title="Generate a new password">
                    <IconButton aria-label="Generate one-time password" edge="end" onClick={() => set('password', generateOneTimePassword())}><AutorenewOutlined /></IconButton>
                  </Tooltip>
                  <Tooltip title={copied === 'form' ? 'Copied' : 'Copy'}>
                    <span>
                      <IconButton aria-label="Copy one-time password" edge="end" disabled={!form.password} onClick={() => void copyPassword(form.password, 'form')}><ContentCopyOutlined /></IconButton>
                    </span>
                  </Tooltip>
                </InputAdornment>
              ),
            },
          }}
        />
        <Box><Button type="submit" variant="contained" disabled={busy}>{busy ? 'Creating…' : 'Create User'}</Button></Box>
      </Stack>
    </Box>
  )
}
