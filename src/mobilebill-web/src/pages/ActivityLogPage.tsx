import { Button, Chip, FormControl, InputLabel, MenuItem, Pagination, Select, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material'
import { useCallback, useEffect, useState } from 'react'

import { getActivityFilters, getActivityLog, type ActivityEntry, type ActivityFilters } from '../api/activityLogApi'
import { roleLabels, type UserRole } from '../api/authApi'
import type { PagedResponse } from '../api/types'
import { EmptyState } from '../components/common/EmptyState'
import { ErrorState } from '../components/common/ErrorState'
import { LoadingState } from '../components/common/LoadingState'
import { PageHeader } from '../components/common/PageHeader'
import { formatDateTime } from '../utils/formatters'

export const activityPageSize = 50
type Filters = { userId: string; area: string; from: string; to: string }
const noFilters: Filters = { userId: '', area: '', from: '', to: '' }

// Every user's activity, newest first: sign-ins, changes, approvals, report downloads and report emails. Open to every user.
export function ActivityLogPage() {
  const [result, setResult] = useState<PagedResponse<ActivityEntry>>()
  const [options, setOptions] = useState<ActivityFilters>({ users: [], areas: [] })
  const [error, setError] = useState<string>()
  const [filters, setFilters] = useState<Filters>(noFilters)
  const [search, setSearch] = useState('')
  const [query, setQuery] = useState('')
  const [page, setPage] = useState(1)

  useEffect(() => { getActivityFilters().then(setOptions).catch(() => { /* the lists are optional; search still works */ }) }, [])
  // Searching waits until typing pauses.
  useEffect(() => { const timer = window.setTimeout(() => { setQuery(search); setPage(1) }, 300); return () => window.clearTimeout(timer) }, [search])

  const load = useCallback(async () => {
    try { setResult(await getActivityLog({ ...filters, search: query, pageNumber: page, pageSize: activityPageSize })); setError(undefined) }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load the activity log.') }
  }, [filters, query, page])
  useEffect(() => { const timer = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(timer) }, [load])

  const change = (key: keyof Filters, value: string) => { setFilters(current => ({ ...current, [key]: value })); setPage(1) }
  const filtered = Object.values(filters).some(Boolean) || query !== ''

  return <Stack spacing={2}>
    <PageHeader title="Activity Log" subtitle="What each user has done: sign-ins, changes, approvals, report downloads and report emails." />
    <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ flexWrap: 'wrap', alignItems: { md: 'center' } }}>
      <TextField size="small" label="Search" placeholder="Activity or user" value={search} onChange={event => setSearch(event.target.value)} sx={{ minWidth: 240 }} />
      <FormControl size="small" sx={{ minWidth: 220 }}>
        <InputLabel id="activity-user">User</InputLabel>
        <Select labelId="activity-user" label="User" value={filters.userId} onChange={event => change('userId', event.target.value)}>
          <MenuItem value="">All users</MenuItem>
          {options.users.map(user => <MenuItem key={user.userId} value={user.userId}>{user.userName}</MenuItem>)}
        </Select>
      </FormControl>
      <FormControl size="small" sx={{ minWidth: 180 }}>
        <InputLabel id="activity-area">Area</InputLabel>
        <Select labelId="activity-area" label="Area" value={filters.area} onChange={event => change('area', event.target.value)}>
          <MenuItem value="">All areas</MenuItem>
          {options.areas.map(area => <MenuItem key={area} value={area}>{area}</MenuItem>)}
        </Select>
      </FormControl>
      <TextField size="small" type="date" label="From" value={filters.from} onChange={event => change('from', event.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
      <TextField size="small" type="date" label="To" value={filters.to} onChange={event => change('to', event.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
      {filtered && <Button onClick={() => { setFilters(noFilters); setSearch(''); setQuery(''); setPage(1) }}>Clear</Button>}
    </Stack>
    {error && <ErrorState message={error} onRetry={() => void load()} />}
    {!result ? (!error && <LoadingState label="Loading activity…" />) : result.items.length === 0
      ? <EmptyState message={filtered ? 'No activity matches the filters.' : 'No activity has been recorded yet.'} />
      : <>
        <Typography variant="body2" color="text.secondary">{result.totalCount} {result.totalCount === 1 ? 'activity' : 'activities'}</Typography>
        <TableContainer sx={{ overflowX: 'auto' }}><Table size="small" aria-label="Activity log">
          <TableHead><TableRow><TableCell>When</TableCell><TableCell>User</TableCell><TableCell>Area</TableCell><TableCell>Activity</TableCell></TableRow></TableHead>
          <TableBody>{result.items.map(item => <TableRow key={item.id} hover>
            <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDateTime(item.occurredAt)}</TableCell>
            <TableCell sx={{ whiteSpace: 'nowrap' }}><Typography variant="body2" sx={{ fontWeight: 600 }}>{item.userName}</Typography><Typography variant="caption" color="text.secondary">{roleLabels[item.role as UserRole] ?? item.role}</Typography></TableCell>
            <TableCell><Chip size="small" label={item.area} /></TableCell>
            <TableCell>{item.description}</TableCell>
          </TableRow>)}</TableBody>
        </Table></TableContainer>
        {result.totalPages > 1 && <Pagination page={page} count={result.totalPages} onChange={(_, value) => setPage(value)} />}
      </>}
  </Stack>
}
