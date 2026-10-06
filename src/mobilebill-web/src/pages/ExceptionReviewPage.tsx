import { Autocomplete, Box, Button, Card, CardContent, Dialog, DialogActions, DialogContent, DialogTitle, FormControl, InputLabel, MenuItem, Pagination, Select, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import TaskAltIcon from '@mui/icons-material/TaskAlt'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useCanPrepareBilling } from '../auth/AuthContext'
import { Link as RouterLink, useParams } from 'react-router-dom'

import { billExceptionTypes, getBillExceptions, getBillExceptionSummary, getMobileAccountCandidates, resolveMobileAccountException, type BillExceptionResolutionFilter, type BillExceptionRow, type BillExceptionSummary, type BillExceptionType, type MobileAccountCandidate } from '../api/billExceptionsApi'
import { getBatches, type BillBatchListItem } from '../api/billingApi'
import { getReviewSummary, type ReviewSummary } from '../api/billReviewApi'
import type { PagedResponse } from '../api/types'
import { billingPeriod } from '../billing/billingRoutes'
import { BatchContextNavigation } from '../components/billing/BatchContextNavigation'
import { EmptyState } from '../components/common/EmptyState'
import { ErrorState } from '../components/common/ErrorState'
import { LoadingState } from '../components/common/LoadingState'
import { PageHeader } from '../components/common/PageHeader'

async function loadAllBatches(): Promise<BillBatchListItem[]> {
  const first = await getBatches({ page: 1, pageSize: 100, sortBy: 'billingYear', sortDirection: 'desc' })
  const items = [...first.items]
  for (let page = 2; page <= first.totalPages; page += 1) {
    const next = await getBatches({ page, pageSize: 100, sortBy: 'billingYear', sortDirection: 'desc' })
    items.push(...next.items)
  }
  return items
}

function batchOptionLabel(batch: BillBatchListItem) {
  return `${batch.provider} · ${batch.corporateCode} · ${billingPeriod(batch.billingYear, batch.billingMonth)} · ${batch.status}`
}

export function ExceptionReviewPage() {
  const canResolve = useCanPrepareBilling()
  const { batchId: routeBatchId } = useParams()
  const [batchId, setBatchId] = useState(routeBatchId ?? '')
  const [loadedBatchId, setLoadedBatchId] = useState('')
  const [context, setContext] = useState<ReviewSummary>()
  const [summary, setSummary] = useState<BillExceptionSummary>()
  const [data, setData] = useState<PagedResponse<BillExceptionRow>>()
  const [page, setPage] = useState(1)
  const [exceptionType, setExceptionType] = useState<BillExceptionType | ''>('')
  const [resolution, setResolution] = useState<BillExceptionResolutionFilter>('All')
  const [error, setError] = useState<string>()
  const [loading, setLoading] = useState(false)
  const [selected, setSelected] = useState<BillExceptionRow>()
  const [candidates, setCandidates] = useState<MobileAccountCandidate[]>([])
  const [candidateId, setCandidateId] = useState('')
  const [comment, setComment] = useState('')
  const [batchOptions, setBatchOptions] = useState<BillBatchListItem[]>([])
  const [optionsLoading, setOptionsLoading] = useState(false)
  const initialLoadDone = useRef(false)

  const query = useMemo(() => ({ pageNumber: String(page), pageSize: '20', resolution, ...(exceptionType ? { exceptionType } : {}) }), [page, exceptionType, resolution])
  const loadRows = useCallback((id: string) => getBillExceptions(id, query).then(setData), [query])

  const loadBatch = useCallback(async (id: string) => {
    if (!id) return
    initialLoadDone.current = true
    setLoading(true); setError(undefined)
    try {
      const [nextContext, nextSummary] = await Promise.all([getReviewSummary(id), getBillExceptionSummary(id)])
      setContext(nextContext); setSummary(nextSummary); setLoadedBatchId(id)
      await loadRows(id)
    } catch (reason) { setError(message(reason, 'Unable to load bill exceptions.')) }
    finally { setLoading(false) }
  }, [loadRows])

  useEffect(() => {
    if (!routeBatchId || initialLoadDone.current) return
    initialLoadDone.current = true
    void loadBatch(routeBatchId)
  }, [routeBatchId, loadBatch])

  useEffect(() => {
    if (routeBatchId) return
    let cancelled = false
    setOptionsLoading(true)
    loadAllBatches()
      .then(items => { if (!cancelled) setBatchOptions(items) })
      .catch(() => { if (!cancelled) setBatchOptions([]) })
      .finally(() => { if (!cancelled) setOptionsLoading(false) })
    return () => { cancelled = true }
  }, [routeBatchId])

  useEffect(() => {
    if (!loadedBatchId || !initialLoadDone.current) return
    const timer = window.setTimeout(() => { setLoading(true); setError(undefined); void loadRows(loadedBatchId).catch(reason => setError(message(reason, 'Unable to load bill exceptions.'))).finally(() => setLoading(false)) }, 0)
    return () => window.clearTimeout(timer)
  }, [loadedBatchId, loadRows])

  async function openResolution(row: BillExceptionRow) {
    try { setError(undefined); setSelected(row); const values = await getMobileAccountCandidates(row.id); setCandidates(values); setCandidateId(values[0]?.mobileAccountId ?? '') }
    catch (reason) { setSelected(undefined); setError(message(reason, 'Unable to load matching allocations.')) }
  }
  async function resolve() {
    if (!selected || !candidateId || comment.trim().length < 5) { setError('Select an allocation and enter a meaningful resolution comment.'); return }
    try {
      await resolveMobileAccountException(selected.id, candidateId, comment.trim()); setSelected(undefined); setComment('')
      const [nextSummary] = await Promise.all([getBillExceptionSummary(loadedBatchId), loadRows(loadedBatchId)])
      setSummary(nextSummary)
    } catch (reason) { setError(message(reason, 'Unable to resolve the exception.')) }
  }

  const period = context ? new Intl.DateTimeFormat('en', { month: 'long', year: 'numeric' }).format(new Date(context.billingYear, context.billingMonth - 1, 1)) : '—'
  const locked = context?.batchStatus === 'Locked'
  return <Stack spacing={3}>
    <PageHeader title="Bill Exception Review" subtitle="Resolve billing exceptions without changing historical master data." />
    {!routeBatchId && <Autocomplete
      options={batchOptions}
      loading={optionsLoading}
      value={batchOptions.find(option => option.id === batchId) ?? null}
      onChange={(_, option) => { const id = option?.id ?? ''; setBatchId(id); setPage(1); if (id) void loadBatch(id) }}
      getOptionLabel={batchOptionLabel}
      isOptionEqualToValue={(option, selected) => option.id === selected.id}
      renderInput={params => <TextField {...params} label="Bill batch" placeholder="Search by provider, corporate code, or period" helperText={optionsLoading ? 'Loading billing batches…' : undefined} />}
    />}
    {routeBatchId && <Stack direction={{ xs: 'column', md: 'row' }} spacing={1} sx={{ alignItems: { md: 'center' } }}><Button component={RouterLink} to={`/billing/${routeBatchId}/review`} variant="outlined" size="small" startIcon={<ArrowBackIcon />}>Back to Bill Review</Button><Typography><strong>Billing Period:</strong> {period}</Typography><Typography><strong>Provider:</strong> {context?.provider ?? '—'}</Typography><Typography><strong>Corporate Code:</strong> {context?.corporateCode ?? '—'}</Typography><Typography><strong>Status:</strong> {context?.batchStatus ?? '—'}</Typography></Stack>}
    {routeBatchId && context && <BatchContextNavigation batchId={routeBatchId} status={context.batchStatus} />}
    {summary && <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 2 }}><Kpi label="Total" value={summary.totalCount} marker="total" /><Kpi label="Unresolved" value={summary.unresolvedCount} marker="unresolved" /><Kpi label="Resolved" value={summary.resolvedCount} marker="resolved" /></Box>}
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}><SelectFilter label="Exception Type" value={exceptionType} onChange={value => { setPage(1); setExceptionType(value as BillExceptionType | '') }} options={[['', 'All exception types'], ...billExceptionTypes.map(value => [value, value] as [string, string])]} /><SelectFilter label="Resolution" value={resolution} onChange={value => { setPage(1); setResolution(value as BillExceptionResolutionFilter) }} options={[['All', 'All'], ['Unresolved', 'Unresolved'], ['Resolved', 'Resolved']]} /></Stack>
    {error && <ErrorState message={error} />}{loading && <LoadingState label="Loading exceptions…" />}
    {!loading && loadedBatchId && data?.items.length === 0 && <EmptyState message="No exceptions match the selected filters." />}
    {!loading && (data?.items.length ?? 0) > 0 && <Box sx={{ overflowX: 'auto' }}><Table stickyHeader aria-label="Bill exceptions"><TableHead><TableRow><TableCell>Mobile Number</TableCell><TableCell>Exception</TableCell><TableCell>Severity</TableCell><TableCell>Status</TableCell><TableCell>Description</TableCell><TableCell>Resolution</TableCell><TableCell /></TableRow></TableHead><TableBody>{data?.items.map(row => <TableRow hover key={row.id} sx={{ bgcolor: ['Open', 'InReview'].includes(row.status) ? 'rgba(237, 108, 2, 0.08)' : undefined }}><TableCell>{row.mobileNumber ?? '—'}</TableCell><TableCell>{row.exceptionType}</TableCell><TableCell>{row.severity}</TableCell><TableCell>{row.status}</TableCell><TableCell>{row.description}</TableCell><TableCell>{row.resolution ?? '—'}</TableCell><TableCell>{canResolve && !locked && row.exceptionType === 'MOBILE_NOT_FOUND' && row.status === 'Open' && <Button size="small" variant="contained" startIcon={<TaskAltIcon />} onClick={() => void openResolution(row)}>Resolve allocation</Button>}</TableCell></TableRow>)}</TableBody></Table></Box>}
    {(data?.totalPages ?? 0) > 0 && <Pagination count={data?.totalPages ?? 0} page={page} onChange={(_, value) => setPage(value)} />}
    <Dialog open={Boolean(selected)} onClose={() => setSelected(undefined)} fullWidth maxWidth="sm"><DialogTitle>Resolve historical mobile allocation</DialogTitle><DialogContent><Stack spacing={2} sx={{ pt: 1 }}>{candidates.length === 0 ? <Typography variant="body2" color="text.secondary">No historical allocation exists for mobile number {selected?.mobileNumber ?? 'this line'}. This number has never been assigned to an employee, so there is nothing to select here — add it as a Mobile Allocation in Master Data first, then re-run matching.</Typography> : <><Typography variant="body2">Only allocations for the same mobile number are available. This transaction-only override does not modify master data.</Typography><TextField select label="Historical allocation" value={candidateId} onChange={event => setCandidateId(event.target.value)}>{candidates.map(candidate => <MenuItem key={candidate.mobileAccountId} value={candidate.mobileAccountId}>{`${candidate.employeeEpf} — ${candidate.employeeName} (${candidate.isActive ? 'active' : 'inactive'})`}</MenuItem>)}</TextField><TextField required multiline minRows={3} label="Resolution comment" value={comment} onChange={event => setComment(event.target.value)} helperText="Explain why this historical ownership is appropriate." /></>}</Stack></DialogContent><DialogActions><Button onClick={() => setSelected(undefined)}>{candidates.length === 0 ? 'Close' : 'Cancel'}</Button>{candidates.length > 0 && <Button variant="contained" onClick={() => void resolve()}>Resolve</Button>}</DialogActions></Dialog>
  </Stack>
}

function Kpi({ label, value, marker }: { label: string; value: number; marker: string }) { return <Card><CardContent><Typography color="text.secondary" variant="body2">{label}</Typography><Typography data-kpi={marker} variant="h5">{value}</Typography></CardContent></Card> }
function SelectFilter({ label, value, onChange, options }: { label: string; value: string; onChange: (value: string) => void; options: Array<[string, string]> }) { const id = `exception-${label.toLowerCase().replaceAll(' ', '-')}`; return <FormControl size="small" sx={{ minWidth: 220 }}><InputLabel id={id}>{label}</InputLabel><Select labelId={id} label={label} value={value} onChange={event => onChange(event.target.value)}>{options.map(([optionValue, optionLabel]) => <MenuItem key={optionValue || 'all'} value={optionValue}>{optionLabel}</MenuItem>)}</Select></FormControl> }
function message(reason: unknown, fallback: string) { return reason instanceof Error ? reason.message : fallback }
