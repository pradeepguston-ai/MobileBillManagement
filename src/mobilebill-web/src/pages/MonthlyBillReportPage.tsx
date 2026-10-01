import { Button, Pagination, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import DownloadIcon from '@mui/icons-material/Download'
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdf'
import { useCallback, useEffect, useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'

import { downloadReportAs, getBatches, type BillBatchListItem, type ReportFormat } from '../api/billingApi'
import { getReviewSummary, type ReviewSummary } from '../api/billReviewApi'
import { billingPeriod, canDownloadReport, exceptionDownloadWarning } from '../billing/billingRoutes'
import { ConfirmActionDialog } from '../components/common/ConfirmActionDialog'
import { BatchStatusChip } from '../components/billing/BatchStatusChip'
import { CurrencyCell } from '../components/billing/CurrencyDisplay'
import { EmptyState } from '../components/common/EmptyState'
import { ErrorState } from '../components/common/ErrorState'
import { LoadingState } from '../components/common/LoadingState'
import { PageHeader } from '../components/common/PageHeader'
import { ReportCategorySelect, ReportFactorySelect } from '../components/billing/ReportFactorySelect'

export function MonthlyBillReportPage() {
  const [items, setItems] = useState<BillBatchListItem[]>([])
  const [summaries, setSummaries] = useState<Record<string, ReviewSummary>>({})
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string>()
  const [downloadingId, setDownloadingId] = useState<string>()
  const [warningBatchId, setWarningBatchId] = useState<string>()
  const [warningFormat, setWarningFormat] = useState<ReportFormat>('excel')
  const [factoryCodes, setFactoryCodes] = useState<string[]>([])
  const [categoryCodes, setCategoryCodes] = useState<string[]>([])

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
  const requestDownload = (id: string, format: ReportFormat) => { if ((summaries[id]?.unresolvedExceptionCount ?? 0) > 0) { setWarningFormat(format); setWarningBatchId(id) } else void download(id, format) }
  const download = async (id: string, format: ReportFormat) => { setDownloadingId(id); setError(undefined); try { await downloadReportAs(id, format, { factoryCodes, categoryCodes }) } catch (reason) { setError(message(reason, `Unable to download the ${format === 'pdf' ? 'PDF' : 'Excel'} report.`)) } finally { setDownloadingId(undefined) } }

  return <Stack spacing={2}>
    <PageHeader title="Monthly Bill Report" subtitle="Download completed and locked monthly reports using server-approved values." />
    <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ alignItems: { md: 'center' } }}><ReportFactorySelect value={factoryCodes} onChange={setFactoryCodes} /><ReportCategorySelect value={categoryCodes} onChange={setCategoryCodes} /><Typography variant="body2" color="text.secondary">{factoryCodes.length || categoryCodes.length ? `Downloads include only bills for ${[factoryCodes.join(', '), categoryCodes.join(', ')].filter(Boolean).join(' + ')}. Unresolved exception lines appear only in the full report.` : 'Downloads include every factory and category (full report).'}</Typography></Stack>
    {error && <ErrorState message={error} />}
    {loading ? <LoadingState label="Loading reports…" /> : items.length === 0 ? <EmptyState message="No billing batches are available for reporting." action={<Button component={RouterLink} to="/billing/new" variant="contained" startIcon={<AddIcon />}>Create Billing Batch</Button>} /> : <TableContainer sx={{ overflowX: 'auto' }}><Table stickyHeader size="small" aria-label="Monthly bill reports"><TableHead><TableRow><TableCell>Billing Period</TableCell><TableCell>Provider</TableCell><TableCell>Corporate Code</TableCell><TableCell align="right">Actual Bill Total</TableCell><TableCell align="right">Final Deduction Total</TableCell><TableCell>Status</TableCell><TableCell>Actions</TableCell></TableRow></TableHead><TableBody>{items.map(batch => { const summary = summaries[batch.id]; const eligible = canDownloadReport(batch.status); return <TableRow hover key={batch.id}><TableCell>{billingPeriod(batch.billingYear, batch.billingMonth)}</TableCell><TableCell>{batch.provider}</TableCell><TableCell>{batch.corporateCode}</TableCell><CurrencyCell value={summary?.totalActualBill} /><CurrencyCell value={summary?.totalFinalDeduction} /><TableCell><BatchStatusChip status={batch.status} /></TableCell><TableCell><Stack direction="row" spacing={1}><Button component={RouterLink} to={`/billing/${batch.id}/review`}>Open Review</Button><Button variant="contained" startIcon={<DownloadIcon />} disabled={!eligible || downloadingId === batch.id} onClick={() => requestDownload(batch.id, 'excel')}>Download Excel</Button><Button variant="outlined" startIcon={<PictureAsPdfIcon />} disabled={!eligible || downloadingId === batch.id} onClick={() => requestDownload(batch.id, 'pdf')}>Download PDF</Button></Stack></TableCell></TableRow> })}</TableBody></Table></TableContainer>}
    {!loading && items.length > 0 && <Pagination page={page} count={totalPages} onChange={(_, value) => setPage(value)} />}
    <ConfirmActionDialog open={Boolean(warningBatchId)} title="Unresolved exceptions" message={exceptionDownloadWarning(warningBatchId ? summaries[warningBatchId]?.unresolvedExceptionCount ?? 0 : 0)} confirmLabel="Download anyway" busy={Boolean(downloadingId)} onCancel={() => setWarningBatchId(undefined)} onConfirm={() => { const id = warningBatchId; setWarningBatchId(undefined); if (id) void download(id, warningFormat) }} />
  </Stack>
}

function message(reason: unknown, fallback: string) { return reason instanceof Error ? reason.message : fallback }
