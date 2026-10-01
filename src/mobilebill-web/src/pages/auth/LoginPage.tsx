import { Box, Button, Link as MuiLink, Stack, TextField, Typography } from '@mui/material'
import { type FormEvent, useState } from 'react'
import { Link as RouterLink, useLocation, useNavigate } from 'react-router-dom'

import { useAuth } from '../../auth/AuthContext'
import { AuthLayout } from '../../components/common/AuthLayout'
import { ErrorState } from '../../components/common/ErrorState'

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string>()
  const [submitting, setSubmitting] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setSubmitting(true)
    setError(undefined)
    try {
      await login(email.trim(), password)
      const from = (location.state as { from?: { pathname: string } } | null)?.from?.pathname ?? '/'
      navigate(from, { replace: true })
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to sign in.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthLayout>
      <Stack spacing={2} component="form" onSubmit={event => void submit(event)}>
        <Box>
          <Typography component="h1" variant="h4">Mobile Bill Management</Typography>
          <Typography color="text.secondary">Sign in to continue.</Typography>
        </Box>
        {error && <ErrorState message={error} />}
        <TextField required label="Email" type="email" autoComplete="username" value={email} onChange={event => setEmail(event.target.value)} />
        <TextField required label="Password" type="password" autoComplete="current-password" value={password} onChange={event => setPassword(event.target.value)} />
        <Button type="submit" variant="contained" disabled={submitting}>{submitting ? 'Signing in…' : 'Sign in'}</Button>
        <Typography variant="body2">No account? <MuiLink component={RouterLink} to="/register">Register</MuiLink></Typography>
      </Stack>
    </AuthLayout>
  )
}
