import { TableCell } from '@mui/material'

import { formatCurrency } from '../../utils/formatters'

export function CurrencyDisplay({ value, unavailable = 'Unavailable' }: { value: number | null | undefined; unavailable?: string }) {
  return <>{value == null ? unavailable : formatCurrency(value)}</>
}

export function CurrencyCell({ value, unavailable = '—' }: { value: number | null | undefined; unavailable?: string }) {
  return <TableCell align="right" sx={{ whiteSpace: 'nowrap' }}><CurrencyDisplay value={value} unavailable={unavailable} /></TableCell>
}
