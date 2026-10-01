import { Box, Button, Pagination, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TableSortLabel, Typography } from '@mui/material'
import { useCallback, useEffect, useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'

import { downloadExcelReport, getBatches, type BillBatchListItem } from '../../api/billingApi'
import { batchContinuationRoute, billingPeriod, canDownloadReport } from '../../billing/billingRoutes'
import { BatchStatusChip } from '../../components/billing/BatchStatusChip'
import { CurrencyDisplay } from '../../components/billing/CurrencyDisplay'
import { ValidationLevelChip } from '../../components/billing/ValidationLevelChip'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { LoadingState } from '../../components/common/LoadingState'
import { formatDateTime } from '../../utils/formatters'

type SortDirection = 'asc' | 'desc'

export function BillingBatchListPage() {
  const [items, setItems] = useState<BillBatchListItem[]>([])
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(0)
  const [sortBy, setSortBy] = useState('billingYear')
  const [sortDirection, setSortDirection] = useState<SortDirection>('desc')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string>()
  const load = useCallback(async () => {
    setLoading(true); setError(undefined)
    try { const result = await getBatches({ page, pageSize: 20, sortBy, sortDirection }); setItems(result.items); setTotalPages(result.totalPages) }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load billing batches.') }
    finally { setLoading(false) }
  }, [page, sortBy, sortDirection])
  useEffect(() => { const timer = window.setTimeout(() => { void load() }, 0); return () => window.clearTimeout(timer) }, [load])
  const changeSort = (column: string) => { setPage(1); if (sortBy === column) setSortDirection(value => value === 'asc' ? 'desc' : 'asc'); else { setSortBy(column); setSortDirection('asc') } }
  const download = async (batchId: string) => { try { setError(undefined); await downloadExcelReport(batchId) } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to download the report.') } }

  return <Stack spacing={2}>
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}>
      <Box><Typography component="h1" variant="h4">Billing Batches</Typography><Typography color="text.secondary">Create and continue monthly telecom bill processing.</Typography></Box>
      <Button component={RouterLink} to="/billing/new" variant="contained">New Billing Batch</Button>
    </Stack>
    {error && <ErrorState message={error} />}
    {loading && <LoadingState label="Loading billing batches…" />}
    {!loading && items.length === 0 && <EmptyState message="No billing batches found." action={<Button component={RouterLink} to="/billing/new" variant="contained">Create Billing Batch</Button>} />}
    {!loading && items.length > 0 && <TableContainer sx={{ overflowX: 'auto' }}><Table stickyHeader size="small" aria-label="Billing batches">
      <TableHead><TableRow>
        <Sortable label="Billing Period" column="billingYear" active={sortBy} direction={sortDirection} onSort={changeSort} />
        <TableCell>Provider</TableCell><TableCell>Corporate Code</TableCell>
        <Sortable label="Calculated Grand Total" column="calculatedGrandTotal" active={sortBy} direction={sortDirection} onSort={changeSort} align="right" />
        <TableCell>Validation Level</TableCell><TableCell>Status</TableCell><TableCell>Uploaded File</TableCell><TableCell>Uploaded By</TableCell>
        <Sortable label="Uploaded At" column="uploadedAt" active={sortBy} direction={sortDirection} onSort={changeSort} />
        <TableCell>Actions</TableCell>
      </TableRow></TableHead>
      <TableBody>{items.map(batch => <TableRow key={batch.id} hover>
        <TableCell sx={{ whiteSpace: 'nowrap' }}>{billingPeriod(batch.billingYear, batch.billingMonth)}</TableCell><TableCell>{batch.provider}</TableCell><TableCell>{batch.corporateCode}</TableCell>
        <TableCell align="right"><CurrencyDisplay value={batch.calculatedGrandTotal} unavailable="—" /></TableCell><TableCell><ValidationLevelChip level={batch.validationLevel} /></TableCell><TableCell><BatchStatusChip status={batch.status} /></TableCell><TableCell>{batch.originalFileName ?? '—'}</TableCell><TableCell>{batch.uploadedBy ?? '—'}</TableCell><TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDateTime(batch.uploadedAt)}</TableCell>
        <TableCell><Stack direction="row" spacing={1}><Button component={RouterLink} to={`/billing/${batch.id}/process`} size="small">Open</Button><Button component={RouterLink} to={batchContinuationRoute(batch.id, batch.status)} size="small">Continue Processing</Button>{canDownloadReport(batch.status) && <Button size="small" onClick={() => void download(batch.id)}>Download Report</Button>}</Stack></TableCell>
      </TableRow>)}</TableBody>
    </Table></TableContainer>}
    {totalPages > 0 && <Pagination page={page} count={totalPages} onChange={(_, value) => setPage(value)} />}
  </Stack>
}

function Sortable({ label, column, active, direction, onSort, align }: { label: string; column: string; active: string; direction: SortDirection; onSort: (column: string) => void; align?: 'right' }) {
  return <TableCell align={align}><TableSortLabel active={active === column} direction={active === column ? direction : 'asc'} onClick={() => onSort(column)}>{label}</TableSortLabel></TableCell>
}
