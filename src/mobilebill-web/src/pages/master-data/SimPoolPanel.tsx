import { Alert, Box, Button, Chip, Stack, Table, TableBody, TableCell, TableHead, TableRow } from '@mui/material'
import { useEffect, useState } from 'react'

import { assignFromPool, disconnectSim, getSimPool, listAllMasterData, type SimPoolItem } from '../../api/masterDataApi'
import { usePermission } from '../../auth/AuthContext'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/master-data/ErrorState'
import { LoadingState } from '../../components/master-data/LoadingState'
import { MasterDataDialog, type FormField, type FormFieldOption } from '../../components/master-data/MasterDataDialog'
import { employeeLabel } from '../../utils/employeeLabel'
import { formatCurrency } from '../../utils/formatters'
import { employeeOption, packageOption } from './pageConfigs'

export const longIdleDays = 60

const disconnectFields: FormField[] = [{ key: 'disconnectedOn', label: 'Disconnection date', type: 'date', required: true }, { key: 'reason', label: 'Reason' }]
// The same choices as Assign from Pool on the Allocations tab. Leaving the package empty keeps the pooled SIM's package.
const assignFields = (employees: FormFieldOption[], packages: FormFieldOption[]): FormField[] => [
  { key: 'employeeId', label: 'New holder', type: 'select', required: true, options: employees },
  { key: 'packageId', label: 'Package', type: 'select', options: packages },
  { key: 'monthlyCreditLimit', label: 'Monthly Credit Limit', type: 'number', required: true },
  { key: 'monthlyRental', label: 'Monthly Rental', type: 'number', required: true },
]
const today = () => { const now = new Date(); return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}` }
type PoolAction = { item: SimPoolItem; values: Record<string, string>; errors: Record<string, string> }

// Numbers waiting for a new holder. Their bills are paid by the company, so every idle month is a cost;
// numbers idle over 60 days should be reassigned or disconnected. Roles that may edit mobile allocations can assign
// a number to a new holder here, and roles that may delete them can disconnect one.
export function SimPoolPanel({ reloadKey = 0 }: { reloadKey?: number }) {
  const [items, setItems] = useState<SimPoolItem[]>()
  const [error, setError] = useState<string>()
  const [reloads, setReloads] = useState(0)
  const [disconnecting, setDisconnecting] = useState<PoolAction>()
  const [assigning, setAssigning] = useState<PoolAction>()
  // Active employees and packages, loaded the first time the Assign dialog opens.
  const [choices, setChoices] = useState<{ employees: FormFieldOption[]; packages: FormFieldOption[] }>()
  const canDisconnect = usePermission('mobile-allocations', 'Delete')
  const canAssign = usePermission('mobile-allocations', 'Edit')
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

  // The credit limit and rental start as the pooled SIM's and can be changed for the new holder.
  const startAssign = (item: SimPoolItem) => {
    setAssigning({ item, values: { employeeId: '', packageId: '', monthlyCreditLimit: String(item.monthlyCreditLimit), monthlyRental: String(item.monthlyRental) }, errors: {} })
    if (choices) return
    Promise.all([listAllMasterData('/api/employees', '', true), listAllMasterData('/api/mobile-packages', '', true)])
      .then(([employees, packages]) => setChoices({ employees: employees.map(employeeOption), packages: packages.map(packageOption) }))
      .catch(exception => setAssigning(current => current && { ...current, errors: { form: exception instanceof Error ? exception.message : 'Unable to load employees and packages.' } }))
  }
  // Choosing a package fills in its rental, as on the Allocations tab.
  const changeAssign = (key: string, value: string) => setAssigning(current => {
    if (!current) return current
    const rental = key === 'packageId' ? choices?.packages.find(option => option.value === value)?.data?.monthlyRental : undefined
    return { ...current, values: { ...current.values, [key]: value, ...(rental !== undefined ? { monthlyRental: String(rental) } : {}) } }
  })
  const assign = async () => {
    if (!assigning) return
    const { values } = assigning
    const errors = Object.fromEntries(assignFields([], []).filter(field => field.required && !values[field.key]?.trim()).map(field => [field.key, `${field.label} is required.`]))
    if (Object.keys(errors).length) { setAssigning({ ...assigning, errors }); return }
    try {
      await assignFromPool(assigning.item.id, values.employeeId, { monthlyCreditLimit: Number(values.monthlyCreditLimit), monthlyRental: Number(values.monthlyRental), packageId: values.packageId || undefined })
      setAssigning(undefined); setReloads(count => count + 1)
    } catch (exception) { setAssigning(current => current && { ...current, errors: { form: exception instanceof Error ? exception.message : 'Unable to assign the number.' } }) }
  }

  if (error) return <ErrorState message={error} />
  if (!items) return <LoadingState />
  if (items.length === 0) return <EmptyState message="The SIM Pool is empty. Every active number has a holder." />
  const longIdle = items.filter(item => item.isLongIdle).length
  const hasActions = canAssign || canDisconnect
  return <Box sx={{ display: 'grid', gap: 2 }}>
    <Alert severity={longIdle > 0 ? 'warning' : 'info'}>
      {items.length} {items.length === 1 ? 'number is' : 'numbers are'} in the SIM Pool and billed to the company.
      {longIdle > 0 && ` ${longIdle} ${longIdle === 1 ? 'has' : 'have'} been idle for more than ${longIdleDays} days: assign ${longIdle === 1 ? 'it' : 'them'} to a new holder or disconnect ${longIdle === 1 ? 'it' : 'them'}.`}
      {' '}{canAssign && canDisconnect ? 'Assign a number to a new holder or disconnect it here.' : canAssign ? 'Assign a number to a new holder here.' : canDisconnect ? 'Disconnect a number here.' : 'Numbers are assigned or disconnected by IT.'}
    </Alert>
    <Box sx={{ overflowX: 'auto' }}>
      <Table size="small" stickyHeader aria-label="SIM Pool">
        <TableHead><TableRow><TableCell>Mobile Number</TableCell><TableCell>EPF</TableCell><TableCell>Previous Holder</TableCell><TableCell>Factory</TableCell><TableCell>Department</TableCell><TableCell>In Pool Since</TableCell><TableCell>Days in Pool</TableCell><TableCell>Reason</TableCell><TableCell align="right">Credit Limit</TableCell><TableCell align="right">Rental</TableCell>{hasActions && <TableCell align="right">Actions</TableCell>}</TableRow></TableHead>
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
          {hasActions && <TableCell align="right"><Stack direction="row" spacing={1} sx={{ justifyContent: 'flex-end' }}>
            {canAssign && <Button size="small" onClick={() => startAssign(item)}>Assign</Button>}
            {/* The reason starts as the one the number was pooled with and can be changed. */}
            {canDisconnect && <Button size="small" color="error" onClick={() => setDisconnecting({ item, values: { disconnectedOn: today(), reason: item.reason ?? '' }, errors: {} })}>Disconnect</Button>}
          </Stack></TableCell>}
        </TableRow>)}</TableBody>
      </Table>
    </Box>
    {assigning && <MasterDataDialog open title={`Assign ${assigning.item.mobileNumber} to a new holder`} fields={assignFields(choices?.employees ?? [], choices?.packages ?? [])} values={assigning.values} errors={assigning.errors}
      onChange={changeAssign} onCancel={() => setAssigning(undefined)} onSave={() => void assign()} saveLabel="Assign" />}
    {disconnecting && <MasterDataDialog open title={`Disconnect ${disconnecting.item.mobileNumber}`} fields={disconnectFields} values={disconnecting.values} errors={disconnecting.errors}
      onChange={(key, value) => setDisconnecting(current => current && { ...current, values: { ...current.values, [key]: value } })}
      onCancel={() => setDisconnecting(undefined)} onSave={() => void disconnect()} saveLabel="Disconnect" />}
  </Box>
}
