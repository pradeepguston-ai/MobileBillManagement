import { Button, Paper, Stack } from '@mui/material'
import ClearIcon from '@mui/icons-material/Clear'
import type { ReactNode } from 'react'

export function FilterBar({ children, activeCount, onClear }: { children: ReactNode; activeCount?: number; onClear?: () => void }) {
  return <Paper variant="outlined" sx={{ p: 2 }}>
    <Stack direction={{ xs: 'column', lg: 'row' }} spacing={1.5} sx={{ flexWrap: 'wrap', alignItems: { lg: 'center' } }}>
      {children}
      {Boolean(activeCount) && onClear && <Button size="small" onClick={onClear} startIcon={<ClearIcon fontSize="small" />}>Clear filters ({activeCount})</Button>}
    </Stack>
  </Paper>
}
