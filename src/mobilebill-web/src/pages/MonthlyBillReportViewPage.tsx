import { Box, Button, Stack, Tooltip, Typography } from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import DownloadIcon from '@mui/icons-material/Download'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link as RouterLink, useParams } from 'react-router-dom'

import { downloadDeductionReport } from '../api/billingApi'
import { getAllReviewRows, getReviewDetail, getReviewSummary, type ReviewDetail, type ReviewRow, type ReviewSummary } from '../api/billReviewApi'
import { listMasterData } from '../api/masterDataApi'
import type { PagedResponse } from '../api/types'
import { billingPeriod, canDownloadReport } from '../billing/billingRoutes'
import { BillDetailDrawer, ReviewFilters, ReviewTable } from '../components/billing/review/MonthlyBillReviewComponents'
import type { MasterOption, ReviewColumn } from '../components/billing/review/reviewPresentation'
import { ErrorState } from '../components/common/ErrorState'
import { LoadingState } from '../components/common/LoadingState'

const noSelection = new Set<string>()
const masterOptions = (path: string) => listMasterData<MasterOption>(path, { pageNumber: 1, pageSize: 100, search: '', isActive: true }).then(page => page.items ?? []).catch(() => [] as MasterOption[])

// The report opened from Monthly Bill Report: only the filters and the bill rows, read-only. The workflow,
// approvals and assessment stay on the Monthly Bill Review screen.
export function MonthlyBillReportViewPage() {
  const { batchId = '' } = useParams()
  const [summary, setSummary] = useState<ReviewSummary>()
  const [rows, setRows] = useState<PagedResponse<ReviewRow>>()
  const [error, setError] = useState<string>()
  const [loading, setLoading] = useState(true)
  const [sort, setSort] = useState('mobileNumber')
  const [direction, setDirection] = useState('asc')
  const [filters, setFilters] = useState<Record<string, string>>({ exception: 'All' })
  const [detail, setDetail] = useState<ReviewDetail>()
  const [downloading, setDownloading] = useState(false)
  const [options, setOptions] = useState({ factories: [] as MasterOption[], departments: [] as MasterOption[], sections: [] as MasterOption[], categories: [] as MasterOption[] })
  // Every matching row is loaded and shown in one scrolling table (no pages).
  const query = useMemo(() => ({ sortBy: sort, sortDirection: direction, ...Object.fromEntries(Object.entries(filters).filter(([, value]) => value.trim() !== '')) }), [sort, direction, filters])

  const load = useCallback(async () => {
    if (!batchId) return
    try { setError(undefined); setRows(await getAllReviewRows(batchId, query)) }
    catch (reason) { setError(message(reason, 'Unable to load the report.')) }
    finally { setLoading(false) }
  }, [batchId, query])

  useEffect(() => { const timer = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(timer) }, [load])
  useEffect(() => { if (batchId) void getReviewSummary(batchId).then(setSummary).catch(() => setSummary(undefined)) }, [batchId])
  useEffect(() => { void Promise.all(['/api/factories', '/api/departments', '/api/sections', '/api/categories'].map(masterOptions))
    .then(([factories, departments, sections, categories]) => setOptions({ factories, departments, sections, categories })) }, [])

  const updateFilter = (key: string, value: string) => { setError(undefined); setLoading(true); setFilters(current => ({ ...current, [key]: value })) }
  // Numbers with Deduction lists the highest deduction first; switching it off returns to the mobile number order.
  const sortByDeduction = (excessOnly: boolean) => { setSort(excessOnly ? 'calculatedExcess' : 'mobileNumber'); setDirection(excessOnly ? 'desc' : 'asc') }
  const toggleSort = (column: ReviewColumn) => { if (!column.sortKey) return; setLoading(true); if (sort === column.sortKey) setDirection(current => current === 'asc' ? 'desc' : 'asc'); else { setSort(column.sortKey); setDirection('asc') } }
  const openDetail = async (row: ReviewRow) => { try { setError(undefined); setDetail(await getReviewDetail(batchId, row.id)) } catch (reason) { setError(message(reason, 'Unable to load bill detail.')) } }

  // The deduction Excel uses the same filters as the table (not its sort: payroll rows are in EPF order).
  const downloadDeductions = async () => { setDownloading(true); setError(undefined); try { await downloadDeductionReport(batchId, filters) } catch (reason) { setError(message(reason, 'Unable to download the deduction report.')) } finally { setDownloading(false) } }
  const deductionsReady = summary ? canDownloadReport(summary.batchStatus) : false
  const deductionButton = <Tooltip title={summary && !deductionsReady ? 'Available once the batch is Completed or Locked (after Finance approval).' : ''}><span><Button variant="contained" startIcon={<DownloadIcon />} disabled={!deductionsReady || downloading} onClick={() => void downloadDeductions()} sx={{ whiteSpace: 'nowrap' }}>{downloading ? 'Downloading…' : 'Download Deduction Excel'}</Button></span></Tooltip>

  const title = summary ? `${billingPeriod(summary.billingYear, summary.billingMonth)} · ${summary.provider} · ${summary.corporateCode}` : 'Monthly bill report'
  return <Stack spacing={2}>
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, flexWrap: 'wrap' }}>
      <Button component={RouterLink} to="/reports/monthly-bill" startIcon={<ArrowBackIcon />} size="small">Monthly Bill Report</Button>
      <Typography component="h1" variant="h6">{title}</Typography>
    </Box>
    <ReviewFilters variant="report" filters={filters} options={options} onChange={updateFilter} onExcessOnlyChange={sortByDeduction} deductionAction={deductionButton} />
    {error && <ErrorState message={error} />}
    {loading && !rows && <LoadingState label="Loading report…" />}
    <ReviewTable rows={rows} loading={loading} sort={sort} direction={direction} selectable={false} selectedIds={noSelection} allOnPageSelected={false} onToggleRow={() => undefined} onToggleAllOnPage={() => undefined} onSort={toggleSort} onOpen={row => void openDetail(row)} />
    <BillDetailDrawer batchId={batchId} canAssess={false} detail={detail} batchStatus={summary?.batchStatus} onClose={() => setDetail(undefined)} onSaved={() => Promise.resolve()} />
  </Stack>
}

function message(reason: unknown, fallback: string) { return reason instanceof Error ? reason.message : fallback }
