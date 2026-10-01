import { Box, Typography } from '@mui/material'
import type { ReactNode } from 'react'

export function EmptyState({ message, action }: { message: string; action?: ReactNode }) {
  return <Box sx={{ py: 6, px: 2, textAlign: 'center', border: 1, borderColor: 'divider', borderRadius: 2, bgcolor: 'background.paper' }}><Typography color="text.secondary" sx={{ mb: action ? 2 : 0 }}>{message}</Typography>{action ?? null}</Box>
}
