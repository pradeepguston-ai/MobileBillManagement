import { Button, Pagination, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import DownloadIcon from '@mui/icons-material/Download'
import { useCallback, useEffect, useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'

import { downloadExcelReport, getBatches, type BillBatchListItem } from '../api/billingApi'
import { getReviewSummary, type ReviewSummary } from '../api/billReviewApi'
import { billingPeriod, canDownloadReport } from '../billing/billingRoutes'
import { BatchStatusChip } from '../components/billing/BatchStatusChip'
import { CurrencyCell } from '../components/billing/CurrencyDisplay'
import { EmptyState } from '../components/common/EmptyState'
import { ErrorState } from '../components/common/ErrorState'
import { LoadingState } from '../components/common/LoadingState'
import { PageHeader } from '../components/common/PageHeader'

export function MonthlyBillReportPage() {
  const [items, setItems] = useState<BillBatchListItem[]>([])
  const [summaries, setSummaries] = useState<Record<string, ReviewSummary>>({})
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string>()
  const [downloadingId, setDownloadingId] = useState<string>()

  const load = useCallback(async () => {
    setLoading(true); setError(undefined)
    try {
      const result = await getBatches({ page, pageSize: 20, sortBy: 'billingYear', sortDirection: 'desc' })
      setItems(result.items); setTotalPages(result.totalPages)
      const results = await Promise.allSettled(result.items.map(batch => getReviewSummary(batch.id)))
      const next: Record<string, ReviewSummary> = {}
      results.forEach((resultItem, index) => { if (resultItem.status === 'fulfilled') next[result.items[index].id] = resultItem.value })
      setSummaries(next)
    } catch (reason) { setItems([]); setError(message(reason, 'Unable to load monthly bill reports.')) }
    finally { setLoading(false) }
  }, [page])

  useEffect(() => { const timer = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(timer) }, [load])
  const download = async (id: string) => { setDownloadingId(id); setError(undefined); try { await downloadExcelReport(id) } catch (reason) { setError(message(reason, 'Unable to download the Excel report.')) } finally { setDownloadingId(undefined) } }

  return <Stack spacing={2}>
    <PageHeader title="Monthly Bill Report" subtitle="Download completed and locked monthly reports using server-approved values." />
    {error && <ErrorState message={error} />}
    {loading ? <LoadingState label="Loading reports…" /> : items.length === 0 ? <EmptyState message="No billing batches are available for reporting." action={<Button component={RouterLink} to="/billing/new" variant="contained" startIcon={<AddIcon />}>Create Billing Batch</Button>} /> : <TableContainer sx={{ overflowX: 'auto' }}><Table stickyHeader size="small" aria-label="Monthly bill reports"><TableHead><TableRow><TableCell>Billing Period</TableCell><TableCell>Provider</TableCell><TableCell>Corporate Code</TableCell><TableCell align="right">Actual Bill Total</TableCell><TableCell align="right">Final Deduction Total</TableCell><TableCell>Status</TableCell><TableCell>Actions</TableCell></TableRow></TableHead><TableBody>{items.map(batch => { const summary = summaries[batch.id]; const eligible = canDownloadReport(batch.status); return <TableRow hover key={batch.id}><TableCell>{billingPeriod(batch.billingYear, batch.billingMonth)}</TableCell><TableCell>{batch.provider}</TableCell><TableCell>{batch.corporateCode}</TableCell><CurrencyCell value={summary?.totalActualBill} /><CurrencyCell value={summary?.totalFinalDeduction} /><TableCell><BatchStatusChip status={batch.status} /></TableCell><TableCell><Stack direction="row" spacing={1}><Button component={RouterLink} to={`/billing/${batch.id}/review`}>Open Review</Button><Button variant="contained" startIcon={<DownloadIcon />} disabled={!eligible || downloadingId === batch.id} onClick={() => void download(batch.id)}>Download Excel</Button></Stack></TableCell></TableRow> })}</TableBody></Table></TableContainer>}
    {!loading && items.length > 0 && <Pagination page={page} count={totalPages} onChange={(_, value) => setPage(value)} />}
  </Stack>
}

function message(reason: unknown, fallback: string) { return reason instanceof Error ? reason.message : fallback }
