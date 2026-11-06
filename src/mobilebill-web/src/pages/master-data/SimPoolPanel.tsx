import { Alert, Box, Button, Chip, Table, TableBody, TableCell, TableHead, TableRow } from '@mui/material'
import { useEffect, useState } from 'react'

import { disconnectSim, getSimPool, type SimPoolItem } from '../../api/masterDataApi'
import { useCanEditMasterData } from '../../auth/AuthContext'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/master-data/ErrorState'
import { LoadingState } from '../../components/master-data/LoadingState'
import { MasterDataDialog, type FormField } from '../../components/master-data/MasterDataDialog'
import { employeeLabel } from '../../utils/employeeLabel'
import { formatCurrency } from '../../utils/formatters'

export const longIdleDays = 60

const disconnectFields: FormField[] = [{ key: 'disconnectedOn', label: 'Disconnection date', type: 'date', required: true }, { key: 'reason', label: 'Reason' }]
const today = () => { const now = new Date(); return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}` }
type Disconnecting = { item: SimPoolItem; values: Record<string, string>; errors: Record<string, string> }

// Numbers waiting for a new holder. Their bills are paid by the company, so every idle month is a cost;
// numbers idle over 60 days should be reassigned or disconnected. Administrator and IT Engineer can disconnect a number here.
export function SimPoolPanel({ reloadKey = 0 }: { reloadKey?: number }) {
  const [items, setItems] = useState<SimPoolItem[]>()
  const [error, setError] = useState<string>()
  const [reloads, setReloads] = useState(0)
  const [disconnecting, setDisconnecting] = useState<Disconnecting>()
  const canDisconnect = useCanEditMasterData()
  useEffect(() => {
    let active = true
    getSimPool()
      .then(result => { if (active) { setItems(result); setError(undefined) } })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'Unable to load the SIM Pool.') })
    return () => { active = false }
  }, [reloadKey, reloads])

  const disconnect = async () => {
    if (!disconnecting) return
    if (!disconnecting.values.disconnectedOn?.trim()) { setDisconnecting({ ...disconnecting, errors: { disconnectedOn: 'Disconnection date is required.' } }); return }
    try { await disconnectSim(disconnecting.item.id, disconnecting.values.disconnectedOn, disconnecting.values.reason ?? ''); setDisconnecting(undefined); setReloads(count => count + 1) }
    catch (exception) { setDisconnecting(current => current && { ...current, errors: { form: exception instanceof Error ? exception.message : 'Unable to disconnect the number.' } }) }
  }

  if (error) return <ErrorState message={error} />
  if (!items) return <LoadingState />
  if (items.length === 0) return <EmptyState message="The SIM Pool is empty. Every active number has a holder." />
  const longIdle = items.filter(item => item.isLongIdle).length
  return <Box sx={{ display: 'grid', gap: 2 }}>
    <Alert severity={longIdle > 0 ? 'warning' : 'info'}>
      {items.length} {items.length === 1 ? 'number is' : 'numbers are'} in the SIM Pool and billed to the company.
      {longIdle > 0 && ` ${longIdle} ${longIdle === 1 ? 'has' : 'have'} been idle for more than ${longIdleDays} days: assign ${longIdle === 1 ? 'it' : 'them'} to a new holder or disconnect ${longIdle === 1 ? 'it' : 'them'}.`}
      {' '}{canDisconnect ? 'Disconnect a number here, or use Assign from Pool on the Allocations tab.' : 'Numbers are assigned or disconnected on the Allocations tab.'}
    </Alert>
    <Box sx={{ overflowX: 'auto' }}>
      <Table size="small" stickyHeader aria-label="SIM Pool">
        <TableHead><TableRow><TableCell>Mobile Number</TableCell><TableCell>EPF</TableCell><TableCell>Previous Holder</TableCell><TableCell>Factory</TableCell><TableCell>Department</TableCell><TableCell>In Pool Since</TableCell><TableCell>Days in Pool</TableCell><TableCell>Reason</TableCell><TableCell align="right">Credit Limit</TableCell><TableCell align="right">Rental</TableCell>{canDisconnect && <TableCell align="right">Actions</TableCell>}</TableRow></TableHead>
        <TableBody>{items.map(item => <TableRow key={item.id} hover>
          <TableCell>{item.mobileNumber}</TableCell>
          <TableCell>{item.previousEpf}</TableCell>
          {/* Name with initials (full name when there are none) and the calling name in brackets. */}
          <TableCell>{employeeLabel({ nameWithInitials: item.previousNameWithInitials, fullName: item.previousEmployeeName, callingName: item.previousCallingName })}</TableCell>
          <TableCell>{item.factory}</TableCell>
          <TableCell>{item.department}</TableCell>
          <TableCell>{item.pooledOn}</TableCell>
          <TableCell><Chip size="small" color={item.isLongIdle ? 'error' : 'default'} label={item.isLongIdle ? `${item.daysInPool} days · over ${longIdleDays}` : `${item.daysInPool} days`} /></TableCell>
          <TableCell>{item.reason ?? '—'}</TableCell>
          <TableCell align="right">{formatCurrency(item.monthlyCreditLimit)}</TableCell>
          <TableCell align="right">{formatCurrency(item.monthlyRental)}</TableCell>
          {/* The reason starts as the one the number was pooled with and can be changed. */}
          {canDisconnect && <TableCell align="right"><Button size="small" color="error" onClick={() => setDisconnecting({ item, values: { disconnectedOn: today(), reason: item.reason ?? '' }, errors: {} })}>Disconnect</Button></TableCell>}
        </TableRow>)}</TableBody>
      </Table>
    </Box>
    {disconnecting && <MasterDataDialog open title={`Disconnect ${disconnecting.item.mobileNumber}`} fields={disconnectFields} values={disconnecting.values} errors={disconnecting.errors}
      onChange={(key, value) => setDisconnecting(current => current && { ...current, values: { ...current.values, [key]: value } })}
      onCancel={() => setDisconnecting(undefined)} onSave={() => void disconnect()} saveLabel="Disconnect" />}
  </Box>
}
