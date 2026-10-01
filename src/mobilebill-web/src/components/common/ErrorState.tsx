import { Alert, Button, Stack } from '@mui/material'

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return <Stack spacing={1}><Alert severity="error">{message}</Alert>{onRetry && <Button variant="outlined" onClick={onRetry} sx={{ alignSelf: 'flex-start' }}>Try again</Button>}</Stack>
}
