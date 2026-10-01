import { Box, TableCell, TableRow } from '@mui/material'
import ArrowUpwardIcon from '@mui/icons-material/ArrowUpward'
import ArrowDownwardIcon from '@mui/icons-material/ArrowDownward'
import type { ReactNode } from 'react'

export function SortableHeaderCell({ label, active, direction, onClick, sortable = true }: { label: ReactNode; active?: boolean; direction?: 'asc' | 'desc'; onClick?: () => void; sortable?: boolean }) {
  return <TableCell
    onClick={sortable ? onClick : undefined}
    sx={{ cursor: sortable ? 'pointer' : 'default', whiteSpace: 'nowrap', userSelect: 'none', ...(sortable ? { '&:hover': { bgcolor: 'action.hover' } } : {}) }}
  >
    <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}>
      {label}
      {sortable && active && (direction === 'asc' ? <ArrowUpwardIcon sx={{ fontSize: 14 }} /> : <ArrowDownwardIcon sx={{ fontSize: 14 }} />)}
    </Box>
  </TableCell>
}

export function TableEmptyRow({ colSpan, message }: { colSpan: number; message: string }) {
  return <TableRow><TableCell colSpan={colSpan} sx={{ py: 4, textAlign: 'center', color: 'text.secondary' }}>{message}</TableCell></TableRow>
}
