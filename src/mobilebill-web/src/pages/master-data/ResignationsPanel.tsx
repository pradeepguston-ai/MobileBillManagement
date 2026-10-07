import { Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, Table, TableBody, TableCell, TableHead, TableRow } from '@mui/material'
import { useEffect, useState } from 'react'

import { cancelResignation, getResignations, type EmployeeResignation, type ResignationStatus } from '../../api/masterDataApi'
import { useCanEditMasterData } from '../../auth/AuthContext'
import { organisationMasterEditorRoles } from '../../auth/roles'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/master-data/ErrorState'
import { LoadingState } from '../../components/master-data/LoadingState'

// Employees serving notice (Pending) or already resigned. A pending resignation completes on its date: the
// employee becomes inactive and their numbers move to the SIM Pool.
export function ResignationsPanel({ status }: { status: ResignationStatus }) {
  const pending = status === 'Pending'
  const canEdit = useCanEditMasterData(organisationMasterEditorRoles)
  const [items, setItems] = useState<EmployeeResignation[]>()
  const [error, setError] = useState<string>()
  const [reloadKey, setReloadKey] = useState(0)
  const [cancelling, setCancelling] = useState<EmployeeResignation>()
  const [cancelError, setCancelError] = useState<string>()
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    let active = true
    getResignations(status)
      .then(result => { if (active) { setItems(result); setError(undefined) } })
      .catch(reason => { if (active) setError(reason instanceof Error ? reason.message : 'Unable to load resignations.') })
    return () => { active = false }
  }, [status, reloadKey])

  const confirmCancel = async () => {
    if (!cancelling) return
    setSaving(true); setCancelError(undefined)
    try { await cancelResignation(cancelling.id); setCancelling(undefined); setReloadKey(key => key + 1) }
    catch (reason) { setCancelError(reason instanceof Error ? reason.message : 'Unable to cancel the resignation.') }
    finally { setSaving(false) }
  }

  if (error) return <ErrorState message={error} />
  if (!items) return <LoadingState />
  if (items.length === 0) return <EmptyState message={pending ? 'No pending resignations. Use Resign with a future date on the Employees tab to record one.' : 'No resigned employees yet.'} />
  const showActions = pending && canEdit
  return <Box sx={{ display: 'grid', gap: 2 }}>
    <Alert severity="info">
      {pending
        ? `${items.length} ${items.length === 1 ? 'employee is' : 'employees are'} serving notice. They stay active and keep their numbers until their resignation date, when they become inactive and their numbers move to the SIM Pool automatically.`
        : `${items.length} resigned ${items.length === 1 ? 'employee' : 'employees'}. Numbers shown are still waiting in the SIM Pool; assign or disconnect them on Mobile Allocations. Devices shown have not been collected yet (Mobile Devices › To Collect).`}
    </Alert>
    <Box sx={{ overflowX: 'auto' }}>
      <Table size="small" stickyHeader aria-label={pending ? 'Pending resignations' : 'Resigned employees'}>
        <TableHead><TableRow>
          {['EPF', 'Name', 'Calling Name', 'Factory', 'Department', 'Section', pending ? 'Last Day' : 'Resigned On', ...(pending ? ['Days Left'] : []), 'Reason', pending ? 'Numbers Held' : 'Numbers in SIM Pool', pending ? 'Devices Held' : 'Devices to Collect', ...(showActions ? [''] : [])]
            .map((heading, index) => <TableCell key={`${heading}-${index}`} sx={{ whiteSpace: 'nowrap' }}>{heading}</TableCell>)}
        </TableRow></TableHead>
        <TableBody>{items.map(item => <TableRow key={item.id} hover>
          <TableCell>{item.epf}</TableCell>
          <TableCell>{item.fullName}</TableCell>
          <TableCell>{item.callingName ?? '—'}</TableCell>
          <TableCell>{item.factory}</TableCell>
          <TableCell>{item.department}</TableCell>
          <TableCell>{item.section ?? '—'}</TableCell>
          <TableCell sx={{ whiteSpace: 'nowrap' }}>{item.resignedOn}</TableCell>
          {pending && <TableCell><Chip size="small" color={item.daysLeft <= 7 ? 'warning' : 'default'} label={`${item.daysLeft} ${item.daysLeft === 1 ? 'day' : 'days'}`} /></TableCell>}
          <TableCell>{item.reason ?? '—'}</TableCell>
          <TableCell>{item.mobileNumbers.length === 0 ? '—' : item.mobileNumbers.join(', ')}</TableCell>
          <TableCell sx={{ color: !pending && item.devices?.length ? 'warning.main' : undefined }}>{item.devices?.length ? item.devices.join(', ') : '—'}</TableCell>
          {showActions && <TableCell><Button size="small" onClick={() => { setCancelError(undefined); setCancelling(item) }}>Cancel Resignation</Button></TableCell>}
        </TableRow>)}</TableBody>
      </Table>
    </Box>
    <Dialog open={Boolean(cancelling)} onClose={() => setCancelling(undefined)} fullWidth maxWidth="xs">
      <DialogTitle>Cancel resignation</DialogTitle>
      <DialogContent>
        <DialogContentText>{cancelling && `${cancelling.fullName} (${cancelling.epf}) will stay an active employee and keep their numbers.`}</DialogContentText>
        {cancelError && <Alert severity="error" sx={{ mt: 2 }}>{cancelError}</Alert>}
      </DialogContent>
      <DialogActions><Button onClick={() => setCancelling(undefined)}>Keep Resignation</Button><Button variant="contained" color="warning" disabled={saving} onClick={() => void confirmCancel()}>Cancel Resignation</Button></DialogActions>
    </Dialog>
  </Box>
}
