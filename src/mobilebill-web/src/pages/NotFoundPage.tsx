import { Button, Stack, Typography } from '@mui/material'
import { Link as RouterLink } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <Stack
      spacing={2}
      sx={{ alignItems: 'center', justifyContent: 'center', minHeight: '100vh' }}
    >
      <Typography component="h1" variant="h3">
        Page not found
      </Typography>
      <Button component={RouterLink} to="/" variant="contained">
        Return to Dashboard
      </Button>
    </Stack>
  )
}
