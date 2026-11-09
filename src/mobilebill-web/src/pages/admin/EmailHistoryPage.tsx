import { Alert, Button, FormControl, InputLabel, MenuItem, Pagination, Select, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material'
import { useCallback, useEffect, useState } from 'react'

import { resendReportEmail, searchReportEmails, type ReportEmailDelivery, type ReportEmailStatus } from '../../api/reportDistributionApi'
import type { PagedResponse } from '../../api/types'
import { billingPeriod } from '../../billing/billingRoutes'
import { DeliveryStatus } from '../../components/billing/EmailReportsDialog'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { LoadingState } from '../../components/common/LoadingState'
import { PageHeader } from '../../components/common/PageHeader'
import { formatDateTime } from '../../utils/formatters'

export const historyPageSize = 20
export const historyRefreshMs = 10_000

// Every monthly bill report email, across batches and newest first: sent by hand from Monthly Bill Report or automatically
// when a batch was locked. Filter by status or search a rule name or recipient; a failed or sent email can be sent again.
export function EmailHistoryPage() {
  const [result, setResult] = useState<PagedResponse<ReportEmailDelivery>>()
  const [error, setError] = useState<string>()
  const [actionError, setActionError] = useState<string>()
  const [status, setStatus] = useState<ReportEmailStatus | ''>('')
  const [search, setSearch] = useState('')
  const [query, setQuery] = useState('')
  const [page, setPage] = useState(1)

  // Searching waits until typing pauses.
  useEffect(() => { const timer = window.setTimeout(() => { setQuery(search); setPage(1) }, 300); return () => window.clearTimeout(timer) }, [search])

  const load = useCallback(async () => {
    try { setResult(await searchReportEmails({ status, search: query, pageNumber: page, pageSize: historyPageSize })); setError(undefined) }
    catch (reason) { setError(message(reason, 'Unable to load the email history.')) }
  }, [status, query, page])
  useEffect(() => { const timer = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(timer) }, [load])

  // While emails on this page are waiting to be sent, keep their status up to date.
  const waiting = result?.items.some(item => item.status === 'Pending') ?? false
  useEffect(() => {
    if (!waiting) return
    const timer = window.setInterval(() => void load(), historyRefreshMs)
    return () => window.clearInterval(timer)
  }, [waiting, load])

  const resend = async (id: string) => {
    setActionError(undefined)
    try { await resendReportEmail(id); await load() } catch (reason) { setActionError(message(reason, 'Unable to resend the email.')) }
  }

  return <Stack spacing={2}>
    <PageHeader title="Email History" subtitle="Every monthly bill report email: sent from Monthly Bill Report or automatically when a batch is locked." />
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
      <TextField size="small" label="Search" placeholder="Rule or recipient" value={search} onChange={event => setSearch(event.target.value)} sx={{ minWidth: 260 }} />
      <FormControl size="small" sx={{ minWidth: 180 }}>
        <InputLabel id="email-history-status">Status</InputLabel>
        <Select labelId="email-history-status" label="Status" value={status} onChange={event => { setStatus(event.target.value as ReportEmailStatus | ''); setPage(1) }}>
          <MenuItem value="">All</MenuItem>
          <MenuItem value="Sent">Sent</MenuItem>
          <MenuItem value="Pending">Sending</MenuItem>
          <MenuItem value="Failed">Failed</MenuItem>
        </Select>
      </FormControl>
    </Stack>
    {actionError && <Alert severity="error">{actionError}</Alert>}
    {error && <ErrorState message={error} onRetry={() => void load()} />}
    {!result ? (!error && <LoadingState label="Loading email history…" />) : result.items.length === 0
      ? <EmptyState message={query || status ? 'No report emails match the filters.' : 'No report emails have been sent yet.'} />
      : <>
        <Typography variant="body2" color="text.secondary">{result.totalCount} {result.totalCount === 1 ? 'email' : 'emails'}</Typography>
        <TableContainer sx={{ overflowX: 'auto' }}><Table size="small" aria-label="Email history">
          <TableHead><TableRow><TableCell>Requested</TableCell><TableCell>Billing Period</TableCell><TableCell>Rule</TableCell><TableCell>Recipients</TableCell><TableCell>Status</TableCell><TableCell>Requested By</TableCell><TableCell /></TableRow></TableHead>
          <TableBody>{result.items.map(item => <TableRow key={item.id} hover>
            <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDateTime(item.requestedAt)}</TableCell>
            <TableCell sx={{ whiteSpace: 'nowrap' }}>{item.billingYear && item.billingMonth ? billingPeriod(item.billingYear, item.billingMonth) : '—'}</TableCell>
            <TableCell sx={{ fontWeight: 600 }}>{item.ruleName}</TableCell>
            <TableCell sx={{ maxWidth: 320, wordBreak: 'break-word' }}>{item.recipients}</TableCell>
            <TableCell sx={{ maxWidth: 280 }}><DeliveryStatus delivery={item} /></TableCell>
            <TableCell>{item.requestedBy}</TableCell>
            <TableCell>{item.status !== 'Pending' && <Button size="small" onClick={() => void resend(item.id)}>Resend</Button>}</TableCell>
          </TableRow>)}</TableBody>
        </Table></TableContainer>
        {result.totalPages > 1 && <Pagination page={page} count={result.totalPages} onChange={(_, value) => setPage(value)} />}
      </>}
  </Stack>
}

function message(reason: unknown, fallback: string) { return reason instanceof Error ? reason.message : fallback }
