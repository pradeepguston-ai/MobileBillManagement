import { Alert, Box, Chip, Table, TableBody, TableCell, TableHead, TableRow } from '@mui/material'
import { useEffect, useState } from 'react'

import { getDevicesToCollect, type DeviceToCollect } from '../../api/devicesApi'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/master-data/ErrorState'
import { LoadingState } from '../../components/master-data/LoadingState'
import { formatCurrency } from '../../utils/formatters'

export const collectionGraceDays = 14

// Devices still with employees who have left. Once overdue, the depreciated value is what may be recovered in the final settlement.
export function DevicesToCollectPanel() {
  const [items, setItems] = useState<DeviceToCollect[]>()
  const [error, setError] = useState<string>()
  useEffect(() => {
    let active = true
    getDevicesToCollect()
      .then(result => { if (active) setItems(result) })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'Unable to load devices to collect.') })
    return () => { active = false }
  }, [])

  if (error) return <ErrorState message={error} />
  if (!items) return <LoadingState />
  if (items.length === 0) return <EmptyState message="No devices to collect. Every leaver has returned their company devices." />
  const overdue = items.filter(item => item.isOverdue)
  return <Box sx={{ display: 'grid', gap: 2 }}>
    <Alert severity={overdue.length > 0 ? 'warning' : 'info'}>
      {items.length} {items.length === 1 ? 'device is' : 'devices are'} still with employees who have left. Use Return on the Devices tab once collected.
      {overdue.length > 0 && ` ${overdue.length} ${overdue.length === 1 ? 'is' : 'are'} overdue (more than ${collectionGraceDays} days): ${formatCurrency(overdue.reduce((sum, item) => sum + item.recoverableAmount, 0))} may be recovered in the final settlement.`}
    </Alert>
    <Box sx={{ overflowX: 'auto' }}>
      <Table size="small" stickyHeader aria-label="Devices to collect">
        <TableHead><TableRow>{['Asset ID', 'Device', 'IMEI', 'Employee', 'Factory', 'Department', 'Left On', 'Waiting', 'Purchase Cost', 'Recoverable'].map(heading =>
          <TableCell key={heading} align={['Purchase Cost', 'Recoverable'].includes(heading) ? 'right' : 'left'} sx={{ whiteSpace: 'nowrap' }}>{heading}</TableCell>)}</TableRow></TableHead>
        <TableBody>{items.map(item => <TableRow key={item.deviceId} hover>
          <TableCell>{item.assetTag}</TableCell>
          <TableCell>{item.brand} {item.model}</TableCell>
          <TableCell>{item.imei1}</TableCell>
          <TableCell>{item.epf} — {item.employeeName}</TableCell>
          <TableCell>{item.factory}</TableCell>
          <TableCell>{item.department}</TableCell>
          <TableCell sx={{ whiteSpace: 'nowrap' }}>{item.resignedOn ?? item.returnPendingSince}</TableCell>
          <TableCell><Chip size="small" color={item.isOverdue ? 'error' : 'default'} label={item.isOverdue ? `${item.daysWaiting} days · overdue` : `${item.daysWaiting} days`} /></TableCell>
          <TableCell align="right">{formatCurrency(item.purchaseCost)}</TableCell>
          <TableCell align="right" sx={{ fontWeight: item.isOverdue ? 700 : undefined }}>{formatCurrency(item.recoverableAmount)}</TableCell>
        </TableRow>)}</TableBody>
      </Table>
    </Box>
  </Box>
}
