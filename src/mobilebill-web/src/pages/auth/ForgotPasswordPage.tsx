import { Box, Button, Link as MuiLink, Stack, TextField, Typography } from '@mui/material'
import { type FormEvent, useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'

import { requestPasswordReset } from '../../api/authApi'
import { AuthLayout } from '../../components/common/AuthLayout'
import { ErrorState } from '../../components/common/ErrorState'

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [error, setError] = useState<string>()
  const [submitting, setSubmitting] = useState(false)
  const [submitted, setSubmitted] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (password.length < 8) { setError('Password must be at least 8 characters.'); return }
    if (password !== confirm) { setError('Passwords do not match.'); return }
    setSubmitting(true)
    setError(undefined)
    try {
      await requestPasswordReset(email.trim(), password)
      setSubmitted(true)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to send the reset request.')
    } finally {
      setSubmitting(false)
    }
  }

  if (submitted) {
    return (
      <AuthLayout maxWidth={480}>
        <Stack spacing={2} sx={{ textAlign: 'center' }}>
          <Typography component="h1" variant="h4">Request sent</Typography>
          <Typography color="text.secondary">If this email belongs to an active account, an administrator will review your password reset request. Once it is approved, sign in with the new password you chose.</Typography>
          <Button component={RouterLink} to="/login" variant="contained">Back to sign in</Button>
        </Stack>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout>
      <Stack spacing={2} component="form" onSubmit={event => void submit(event)}>
        <Box>
          <Typography component="h1" variant="h4">Reset password</Typography>
          <Typography color="text.secondary">Choose a new password. An administrator must approve the request before it takes effect.</Typography>
        </Box>
        {error && <ErrorState message={error} />}
        <TextField required label="Email" type="email" autoComplete="username" value={email} onChange={event => setEmail(event.target.value)} />
        <TextField required label="New password" type="password" autoComplete="new-password" helperText="At least 8 characters." value={password} onChange={event => setPassword(event.target.value)} />
        <TextField required label="Confirm new password" type="password" autoComplete="new-password" value={confirm} onChange={event => setConfirm(event.target.value)} />
        <Button type="submit" variant="contained" disabled={submitting}>{submitting ? 'Sending…' : 'Request reset'}</Button>
        <Typography variant="body2">Remembered it? <MuiLink component={RouterLink} to="/login">Sign in</MuiLink></Typography>
      </Stack>
    </AuthLayout>
  )
}
