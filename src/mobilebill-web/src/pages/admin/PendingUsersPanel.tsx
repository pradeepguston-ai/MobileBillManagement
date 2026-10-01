import { Button, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from '@mui/material'
import { useCallback, useEffect, useState } from 'react'

import { activateUser, deactivateUser, listPendingUsers, roleLabels, type PendingUser } from '../../api/authApi'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { LoadingState } from '../../components/common/LoadingState'
import { PageHeader } from '../../components/common/PageHeader'
import { formatDateTime } from '../../utils/formatters'

export function PendingUsersPage() {
  const [users, setUsers] = useState<PendingUser[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string>()
  const [busyId, setBusyId] = useState<string>()

  const load = useCallback(async () => {
    setLoading(true)
    setError(undefined)
    try { setUsers(await listPendingUsers()) }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load pending registrations.') }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { void load() }, [load])

  const activate = async (id: string) => {
    setBusyId(id)
    setError(undefined)
    try { await activateUser(id); await load() }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to activate this account.') }
    finally { setBusyId(undefined) }
  }

  const deactivate = async (id: string) => {
    setBusyId(id)
    setError(undefined)
    try { await deactivateUser(id); await load() }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to deactivate this account.') }
    finally { setBusyId(undefined) }
  }

  return (
    <Stack spacing={2}>
      <PageHeader title="Pending Users" subtitle="Activate a registration to grant its requested role, or deactivate it to reject." />
      {error && <ErrorState message={error} />}
      {loading && <LoadingState label="Loading pending registrations…" />}
      {!loading && users.length === 0 && <EmptyState message="No pending registrations." />}
      {!loading && users.length > 0 && (
        <TableContainer sx={{ overflowX: 'auto' }}>
          <Table size="small" stickyHeader aria-label="Pending users">
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Email</TableCell>
                <TableCell>Requested Role</TableCell>
                <TableCell>Registered At</TableCell>
                <TableCell>Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {users.map(user => (
                <TableRow key={user.id} hover>
                  <TableCell>{user.displayName}</TableCell>
                  <TableCell>{user.email}</TableCell>
                  <TableCell>{roleLabels[user.requestedRole]}</TableCell>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDateTime(user.registeredAtUtc)}</TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={1}>
                      <Button size="small" variant="contained" disabled={busyId === user.id} onClick={() => void activate(user.id)}>Activate</Button>
                      <Button size="small" color="error" disabled={busyId === user.id} onClick={() => void deactivate(user.id)}>Reject</Button>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Stack>
  )
}
