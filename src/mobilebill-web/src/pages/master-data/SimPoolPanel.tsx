import { Alert, Box, Chip, Table, TableBody, TableCell, TableHead, TableRow } from '@mui/material'
import { useEffect, useState } from 'react'

import { getSimPool, type SimPoolItem } from '../../api/masterDataApi'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/master-data/ErrorState'
import { LoadingState } from '../../components/master-data/LoadingState'
import { formatCurrency } from '../../utils/formatters'

export const longIdleDays = 60

// Numbers waiting for a new holder. Their bills are paid by the company, so every idle month is a cost;
// numbers idle over 60 days should be reassigned or disconnected.
export function SimPoolPanel({ reloadKey = 0 }: { reloadKey?: number }) {
  const [items, setItems] = useState<SimPoolItem[]>()
  const [error, setError] = useState<string>()
  useEffect(() => {
    let active = true
    getSimPool()
      .then(result => { if (active) { setItems(result); setError(undefined) } })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'Unable to load the SIM Pool.') })
    return () => { active = false }
  }, [reloadKey])

  if (error) return <ErrorState message={error} />
  if (!items) return <LoadingState />
  if (items.length === 0) return <EmptyState message="The SIM Pool is empty. Every active number has a holder." />
  const longIdle = items.filter(item => item.isLongIdle).length
  return <Box sx={{ display: 'grid', gap: 2 }}>
    <Alert severity={longIdle > 0 ? 'warning' : 'info'}>
      {items.length} {items.length === 1 ? 'number is' : 'numbers are'} in the SIM Pool and billed to the company.
      {longIdle > 0 && ` ${longIdle} ${longIdle === 1 ? 'has' : 'have'} been idle for more than ${longIdleDays} days: assign ${longIdle === 1 ? 'it' : 'them'} to a new holder or disconnect ${longIdle === 1 ? 'it' : 'them'}.`}
      {' '}Use Assign from Pool or Disconnect on the Allocations tab.
    </Alert>
    <Box sx={{ overflowX: 'auto' }}>
      <Table size="small" stickyHeader aria-label="SIM Pool">
        <TableHead><TableRow><TableCell>Mobile Number</TableCell><TableCell>Previous Holder</TableCell><TableCell>Factory</TableCell><TableCell>Department</TableCell><TableCell>In Pool Since</TableCell><TableCell>Days in Pool</TableCell><TableCell>Reason</TableCell><TableCell align="right">Credit Limit</TableCell><TableCell align="right">Rental</TableCell></TableRow></TableHead>
        <TableBody>{items.map(item => <TableRow key={item.id} hover>
          <TableCell>{item.mobileNumber}</TableCell>
          <TableCell>{item.previousEpf} — {item.previousEmployeeName}</TableCell>
          <TableCell>{item.factory}</TableCell>
          <TableCell>{item.department}</TableCell>
          <TableCell>{item.pooledOn}</TableCell>
          <TableCell><Chip size="small" color={item.isLongIdle ? 'error' : 'default'} label={item.isLongIdle ? `${item.daysInPool} days · over ${longIdleDays}` : `${item.daysInPool} days`} /></TableCell>
          <TableCell>{item.reason ?? '—'}</TableCell>
          <TableCell align="right">{formatCurrency(item.monthlyCreditLimit)}</TableCell>
          <TableCell align="right">{formatCurrency(item.monthlyRental)}</TableCell>
        </TableRow>)}</TableBody>
      </Table>
    </Box>
  </Box>
}
