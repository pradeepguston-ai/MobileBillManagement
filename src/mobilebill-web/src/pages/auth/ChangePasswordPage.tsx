import { Alert, Box, Button, Stack, TextField, Typography } from '@mui/material'
import { type FormEvent, useState } from 'react'

import { useAuth } from '../../auth/AuthContext'
import { AuthLayout } from '../../components/common/AuthLayout'
import { ErrorState } from '../../components/common/ErrorState'

// Shown instead of the app to a user who signed in with the one-time password their account was created with.
// Nothing else is available (the API refuses it too) until they choose their own password.
export function ChangePasswordPage() {
  const { user, changePassword, logout } = useAuth()
  const [current, setCurrent] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string>()
  const [submitting, setSubmitting] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (password.length < 8) { setError('The new password must be at least 8 characters.'); return }
    if (password !== confirm) { setError('The new passwords do not match.'); return }
    if (password === current) { setError('Choose a new password that is different from the one-time password.'); return }
    setSubmitting(true)
    setError(undefined)
    try {
      await changePassword(current, password)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to change the password.')
      setSubmitting(false)
    }
  }

  return (
    <AuthLayout>
      <Stack spacing={2} component="form" onSubmit={event => void submit(event)}>
        <Box>
          <Typography component="h1" variant="h4">Set your password</Typography>
          <Typography color="text.secondary">
            Welcome{user ? `, ${user.displayName}` : ''}. You signed in with a one-time password. Choose your own password to continue.
          </Typography>
        </Box>
        <Alert severity="info">The one-time password stops working once you set your own.</Alert>
        {error && <ErrorState message={error} />}
        <TextField required label="One-time password" type="password" autoComplete="current-password" value={current} onChange={event => setCurrent(event.target.value)} />
        <TextField required label="New password" type="password" autoComplete="new-password" helperText="At least 8 characters." value={password} onChange={event => setPassword(event.target.value)} slotProps={{ htmlInput: { maxLength: 128 } }} />
        <TextField required label="Confirm new password" type="password" autoComplete="new-password" value={confirm} onChange={event => setConfirm(event.target.value)} slotProps={{ htmlInput: { maxLength: 128 } }} />
        <Button type="submit" variant="contained" disabled={submitting}>{submitting ? 'Saving…' : 'Set password and continue'}</Button>
        <Button variant="text" color="inherit" onClick={logout}>Sign out</Button>
      </Stack>
    </AuthLayout>
  )
}
