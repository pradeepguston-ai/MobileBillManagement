import { Button, Stack, Typography } from '@mui/material'
import { Link as RouterLink } from 'react-router-dom'

import { AuthLayout } from '../../components/common/AuthLayout'

export function PendingApprovalPage() {
  return (
    <AuthLayout maxWidth={480}>
      <Stack spacing={2} sx={{ textAlign: 'center' }}>
        <Typography component="h1" variant="h4">Registration received</Typography>
        <Typography color="text.secondary">Your account is pending administrator activation. You'll be able to sign in once it has been approved.</Typography>
        <Button component={RouterLink} to="/login" variant="contained">Back to sign in</Button>
      </Stack>
    </AuthLayout>
  )
}
