import { Button, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from '@mui/material'
import { useCallback, useEffect, useState } from 'react'

import { approvePasswordReset, listPasswordResetRequests, rejectPasswordReset, roleLabels, type PasswordResetRequest } from '../../api/authApi'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { LoadingState } from '../../components/common/LoadingState'
import { PageHeader } from '../../components/common/PageHeader'
import { formatDateTime } from '../../utils/formatters'

export function PasswordResetRequestsPage() {
  const [requests, setRequests] = useState<PasswordResetRequest[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string>()
  const [busyId, setBusyId] = useState<string>()

  const load = useCallback(async () => {
    setLoading(true)
    setError(undefined)
    try { setRequests(await listPasswordResetRequests()) }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load password reset requests.') }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { void load() }, [load])

  const decide = async (id: string, action: (id: string) => Promise<void>, failure: string) => {
    setBusyId(id)
    setError(undefined)
    try { await action(id); await load() }
    catch (reason) { setError(reason instanceof Error ? reason.message : failure) }
    finally { setBusyId(undefined) }
  }

  return (
    <Stack spacing={2}>
      <PageHeader title="Password Reset Requests" subtitle="Approve a request to apply the new password the user chose, or reject it to keep the current one." />
      {error && <ErrorState message={error} />}
      {loading && <LoadingState label="Loading password reset requests…" />}
      {!loading && requests.length === 0 && <EmptyState message="No pending password reset requests." />}
      {!loading && requests.length > 0 && (
        <TableContainer sx={{ overflowX: 'auto' }}>
          <Table size="small" stickyHeader aria-label="Password reset requests">
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Email</TableCell>
                <TableCell>Role</TableCell>
                <TableCell>Requested At</TableCell>
                <TableCell>Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {requests.map(request => (
                <TableRow key={request.id} hover>
                  <TableCell>{request.displayName}</TableCell>
                  <TableCell>{request.email}</TableCell>
                  <TableCell>{roleLabels[request.role]}</TableCell>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDateTime(request.requestedAtUtc)}</TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={1}>
                      <Button size="small" variant="contained" disabled={busyId === request.id} onClick={() => void decide(request.id, approvePasswordReset, 'Unable to approve this request.')}>Approve</Button>
                      <Button size="small" color="error" disabled={busyId === request.id} onClick={() => void decide(request.id, rejectPasswordReset, 'Unable to reject this request.')}>Reject</Button>
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
