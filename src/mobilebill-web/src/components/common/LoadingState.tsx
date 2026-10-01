import { CircularProgress, Stack, Typography } from '@mui/material'

export function LoadingState({ label = 'Loading…' }: { label?: string }) {
  return <Stack role="status" aria-live="polite" direction="row" spacing={1.5} sx={{ py: 4, alignItems: 'center', justifyContent: 'center' }}><CircularProgress size={24} /><Typography color="text.secondary">{label}</Typography></Stack>
}
