import { Button, FormControl, MenuItem, Select, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material'
import { useCallback, useEffect, useState } from 'react'

import { activateUser, allRoles, changeUserRole, deactivateUser, listUsers, statusLabels, type ManagedUser, type UserRole } from '../../api/authApi'
import { useAuth } from '../../auth/AuthContext'
import { ConfirmActionDialog } from '../../components/common/ConfirmActionDialog'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { LoadingState } from '../../components/common/LoadingState'
import { StatusBadge, type StatusTone } from '../../components/common/StatusBadge'
import { formatDateTime } from '../../utils/formatters'

const statusTone: Record<ManagedUser['status'], StatusTone> = { Active: 'success', PendingActivation: 'warning', Deactivated: 'neutral' }

// Every account: change its role, or activate / deactivate it. Administrators cannot lock themselves out.
export function AllUsersPanel({ refreshKey }: { refreshKey?: number }) {
  const { user: currentUser } = useAuth()
  const [users, setUsers] = useState<ManagedUser[]>([])
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string>()
  const [notice, setNotice] = useState<string>()
  const [busyId, setBusyId] = useState<string>()
  const [confirmDeactivate, setConfirmDeactivate] = useState<ManagedUser>()

  const load = useCallback(async (term: string) => {
    setLoading(true)
    setError(undefined)
    try { setUsers(await listUsers(term)) }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load users.') }
    finally { setLoading(false) }
  }, [])

  useEffect(() => {
    const timer = window.setTimeout(() => void load(search), 250)
    return () => window.clearTimeout(timer)
  }, [load, search, refreshKey])

  const run = async (id: string, action: () => Promise<unknown>, success: string, failure: string) => {
    setBusyId(id)
    setError(undefined)
    setNotice(undefined)
    try { await action(); setNotice(success); await load(search) }
    catch (reason) { setError(reason instanceof Error ? reason.message : failure) }
    finally { setBusyId(undefined) }
  }

  const changeRole = (target: ManagedUser, role: UserRole) => {
    if (role === target.role) return
    void run(target.id, () => changeUserRole(target.id, role), `${target.displayName} is now ${allRoles.find(option => option.value === role)?.label ?? role}.`, 'Unable to change the role.')
  }

  return (
    <Stack spacing={2}>
      <TextField size="small" label="Search name or email" value={search} onChange={event => setSearch(event.target.value)} sx={{ maxWidth: 360 }} />
      {error && <ErrorState message={error} />}
      {notice && <Typography color="success.main" variant="body2">{notice}</Typography>}
      {loading && users.length === 0 && <LoadingState label="Loading users…" />}
      {!loading && users.length === 0 && <EmptyState message={search.trim() ? 'No users match this search.' : 'No users found.'} />}
      {users.length > 0 && (
        <TableContainer sx={{ overflowX: 'auto' }}>
          <Table size="small" stickyHeader aria-label="All users">
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Email</TableCell>
                <TableCell>Role</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Registered At</TableCell>
                <TableCell>Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {users.map(item => {
                const isSelf = item.id === currentUser?.id
                const busy = busyId === item.id
                return (
                  <TableRow key={item.id} hover>
                    <TableCell>{item.displayName}{isSelf && <Typography component="span" variant="caption" color="text.secondary"> (you)</Typography>}</TableCell>
                    <TableCell>{item.email}</TableCell>
                    <TableCell>
                      <FormControl size="small" sx={{ minWidth: 180 }}>
                        <Select value={item.role} disabled={busy || isSelf} onChange={event => changeRole(item, event.target.value as UserRole)} slotProps={{ input: { 'aria-label': `Role for ${item.displayName}` } }}>
                          {allRoles.map(option => <MenuItem key={option.value} value={option.value}>{option.label}</MenuItem>)}
                        </Select>
                      </FormControl>
                    </TableCell>
                    <TableCell><StatusBadge label={statusLabels[item.status]} tone={statusTone[item.status]} /></TableCell>
                    <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDateTime(item.registeredAtUtc)}</TableCell>
                    <TableCell>
                      {item.status === 'Active'
                        ? <Button size="small" color="error" disabled={busy || isSelf} onClick={() => setConfirmDeactivate(item)}>Deactivate</Button>
                        : <Button size="small" variant="contained" disabled={busy} onClick={() => void run(item.id, () => activateUser(item.id), `${item.displayName} has been activated.`, 'Unable to activate this account.')}>Activate</Button>}
                    </TableCell>
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      <ConfirmActionDialog
        open={Boolean(confirmDeactivate)}
        title="Deactivate user"
        message={confirmDeactivate ? `${confirmDeactivate.displayName} will no longer be able to sign in. You can activate the account again later.` : ''}
        confirmLabel="Deactivate"
        busy={Boolean(busyId)}
        onCancel={() => setConfirmDeactivate(undefined)}
        onConfirm={() => {
          const target = confirmDeactivate
          setConfirmDeactivate(undefined)
          if (target) void run(target.id, () => deactivateUser(target.id), `${target.displayName} has been deactivated.`, 'Unable to deactivate this account.')
        }}
      />
    </Stack>
  )
}
