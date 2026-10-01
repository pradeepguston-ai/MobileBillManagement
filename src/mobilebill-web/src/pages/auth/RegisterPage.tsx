import { Box, Button, FormControl, InputLabel, Link as MuiLink, MenuItem, Select, Stack, TextField, Typography } from '@mui/material'
import { type FormEvent, useState } from 'react'
import { Link as RouterLink, useNavigate } from 'react-router-dom'

import { register, selfRegisterableRoles, type UserRole } from '../../api/authApi'
import { AuthLayout } from '../../components/common/AuthLayout'
import { ErrorState } from '../../components/common/ErrorState'

export function RegisterPage() {
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState<UserRole>('ITEngineer')
  const [error, setError] = useState<string>()
  const [submitting, setSubmitting] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (password.length < 8) { setError('Password must be at least 8 characters.'); return }
    setSubmitting(true)
    setError(undefined)
    try {
      await register({ email: email.trim(), password, displayName: displayName.trim(), requestedRole: role })
      navigate('/pending-approval', { replace: true })
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to register.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthLayout>
      <Stack spacing={2} component="form" onSubmit={event => void submit(event)}>
        <Box>
          <Typography component="h1" variant="h4">Create an account</Typography>
          <Typography color="text.secondary">An administrator must activate your account before you can sign in.</Typography>
        </Box>
        {error && <ErrorState message={error} />}
        <TextField required label="Full Name" value={displayName} onChange={event => setDisplayName(event.target.value)} />
        <TextField required label="Email" type="email" autoComplete="username" value={email} onChange={event => setEmail(event.target.value)} />
        <TextField required label="Password" type="password" autoComplete="new-password" helperText="At least 8 characters." value={password} onChange={event => setPassword(event.target.value)} />
        <FormControl required fullWidth>
          <InputLabel id="role-label">Role</InputLabel>
          <Select labelId="role-label" label="Role" value={role} onChange={event => setRole(event.target.value as UserRole)}>
            {selfRegisterableRoles.map(option => <MenuItem key={option.value} value={option.value}>{option.label}</MenuItem>)}
          </Select>
        </FormControl>
        <Button type="submit" variant="contained" disabled={submitting}>{submitting ? 'Registering…' : 'Register'}</Button>
        <Typography variant="body2">Already have an account? <MuiLink component={RouterLink} to="/login">Sign in</MuiLink></Typography>
      </Stack>
    </AuthLayout>
  )
}
