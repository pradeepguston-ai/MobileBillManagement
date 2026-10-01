import { Alert, Stack } from '@mui/material'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { useParams } from 'react-router-dom'

import { downloadReportAs, type ReportFilters, type ReportFormat } from '../api/billingApi'
import { getAllReviewRows, getReviewDetail, getReviewSummary, type ReviewDetail, type ReviewRow, type ReviewSummary } from '../api/billReviewApi'
import { decideWorkflow, getWorkflowCapabilities, lockBatch, submitForItReview, type WorkflowCapabilities } from '../api/billWorkflowApi'
import { listMasterData } from '../api/masterDataApi'
import type { PagedResponse } from '../api/types'
import { billingPeriod, exceptionDownloadWarning } from '../billing/billingRoutes'
import { BatchContextNavigation } from '../components/billing/BatchContextNavigation'
import { WorkflowStepper } from '../components/billing/WorkflowStepper'
import { ApprovalHistorySection, BillDetailDrawer, BulkAssignBar, BulkAssignDialog, ReportCard, ReviewFilters, ReviewHeader, ReviewKpis, ReviewTable, WorkflowActionDialog, WorkflowActions } from '../components/billing/review/MonthlyBillReviewComponents'
import type { MasterOption, ReviewColumn, WorkflowDialogState } from '../components/billing/review/reviewPresentation'
import { ConfirmActionDialog } from '../components/common/ConfirmActionDialog'
import { ErrorState } from '../components/common/ErrorState'
import { LoadingState } from '../components/common/LoadingState'

export function MonthlyBillReviewPage() {
  const { batchId = '' } = useParams()
  const [summary, setSummary] = useState<ReviewSummary>()
  const [rows, setRows] = useState<PagedResponse<ReviewRow>>()
  const [error, setError] = useState<string>()
  const [loading, setLoading] = useState(true)
  const [sort, setSort] = useState('mobileNumber')
  const [direction, setDirection] = useState('asc')
  const [filters, setFilters] = useState<Record<string, string>>({ exception: 'All' })
  const [detail, setDetail] = useState<ReviewDetail>()
  const [capabilities, setCapabilities] = useState<WorkflowCapabilities>()
  const [capabilityError, setCapabilityError] = useState<string>()
  const [workflowDialog, setWorkflowDialog] = useState<WorkflowDialogState>()
  const [workflowComment, setWorkflowComment] = useState('')
  const [workflowDialogError, setWorkflowDialogError] = useState<string>()
  const [lockConfirmationOpen, setLockConfirmationOpen] = useState(false)
  const [workflowSaving, setWorkflowSaving] = useState(false)
  const [downloading, setDownloading] = useState(false)
  const [options, setOptions] = useState({ factories: [] as MasterOption[], departments: [] as MasterOption[], categories: [] as MasterOption[] })
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [bulkDialogOpen, setBulkDialogOpen] = useState(false)
  const [downloadWarningOpen, setDownloadWarningOpen] = useState(false)
  const [pendingFormat, setPendingFormat] = useState<ReportFormat>('excel')
  const [pendingFilters, setPendingFilters] = useState<ReportFilters>()
  // Every matching row is loaded and shown in one scrolling table (no pages).
  const query = useMemo(() => ({ sortBy: sort, sortDirection: direction, ...Object.fromEntries(Object.entries(filters).filter(([, value]) => value.trim() !== '')) }), [sort, direction, filters])

  const load = useCallback(async () => {
    if (!batchId) return
    try {
      setError(undefined)
      const [nextSummary, nextRows] = await Promise.all([getReviewSummary(batchId), getAllReviewRows(batchId, query)])
      setSummary(nextSummary); setRows(nextRows)
    } catch (reason) { setError(message(reason, 'Unable to load bill review.')) }
    finally { setLoading(false) }
  }, [batchId, query])

  const loadCapabilities = useCallback(async () => {
    if (!batchId) return
    try { setCapabilities(await getWorkflowCapabilities(batchId)); setCapabilityError(undefined) }
    catch (reason) { setCapabilities(undefined); setCapabilityError(message(reason, 'Unable to determine workflow authorization.')) }
  }, [batchId])

  useEffect(() => { const timer = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(timer) }, [load])
  useEffect(() => { const timer = window.setTimeout(() => void loadCapabilities(), 0); return () => window.clearTimeout(timer) }, [loadCapabilities])
  useEffect(() => { void Promise.all([
    listMasterData<MasterOption>('/api/factories', { pageNumber: 1, pageSize: 100, search: '', isActive: true }),
    listMasterData<MasterOption>('/api/departments', { pageNumber: 1, pageSize: 100, search: '', isActive: true }),
    listMasterData<MasterOption>('/api/categories', { pageNumber: 1, pageSize: 100, search: '', isActive: true }),
  ]).then(([factories, departments, categories]) => setOptions({ factories: factories.items ?? [], departments: departments.items ?? [], categories: categories.items ?? [] })).catch(() => setOptions({ factories: [], departments: [], categories: [] })) }, [])
  useEffect(() => { setSelectedIds(new Set()) }, [filters])

  const updateFilter = (key: string, value: string) => { setError(undefined); setLoading(true); setFilters(current => ({ ...current, [key]: value })) }
  const openDetail = async (row: ReviewRow) => { try { setError(undefined); setDetail(await getReviewDetail(batchId, row.id)) } catch (reason) { setError(message(reason, 'Unable to load bill detail.')) } }
  const toggleRow = (id: string) => setSelectedIds(current => { const next = new Set(current); if (next.has(id)) next.delete(id); else next.add(id); return next })
  const allOnPageSelected = (rows?.items.length ?? 0) > 0 && (rows?.items.every(row => selectedIds.has(row.id)) ?? false)
  const toggleAllOnPage = () => setSelectedIds(current => {
    const next = new Set(current)
    const ids = rows?.items.map(row => row.id) ?? []
    if (allOnPageSelected) ids.forEach(id => next.delete(id)); else ids.forEach(id => next.add(id))
    return next
  })
  const clearSelection = () => setSelectedIds(new Set())
  // All matching rows are already loaded, so selecting them all needs no extra requests.
  const selectAllMatching = () => setSelectedIds(new Set(rows?.items.map(row => row.id) ?? []))
  const mobileNumberById = Object.fromEntries((rows?.items ?? []).map(row => [row.id, row.mobileNumber]))
  const canBulkAssign = summary?.batchStatus !== 'Locked'
  const toggleSort = (column: ReviewColumn) => { if (!column.sortKey) return; setLoading(true); if (sort === column.sortKey) setDirection(current => current === 'asc' ? 'desc' : 'asc'); else { setSort(column.sortKey); setDirection('asc') } }
  const refreshAfterAssessment = async (monthlyBillId: string) => { const [nextDetail] = await Promise.all([getReviewDetail(batchId, monthlyBillId), load()]); setDetail(nextDetail) }
  const refreshAuthoritativeState = async () => { setLoading(true); await Promise.all([load(), loadCapabilities()]) }
  const openWorkflowDialog = (state: WorkflowDialogState) => { setWorkflowComment(''); setWorkflowDialogError(undefined); setWorkflowDialog(state) }
  const runWorkflow = async () => {
    if (!workflowDialog) return
    if (workflowDialog.commentRequired && workflowComment.trim().length < 5) { setWorkflowDialogError('Enter a meaningful reason.'); return }
    setWorkflowSaving(true); setError(undefined); setWorkflowDialogError(undefined)
    try {
      const comment = workflowComment.trim() || undefined
      if (workflowDialog.kind === 'submit') await submitForItReview(batchId, comment)
      else await decideWorkflow(batchId, workflowDialog.action!, comment)
      setWorkflowDialog(undefined); setWorkflowComment(''); await refreshAuthoritativeState()
    } catch (reason) { const errorMessage = message(reason, 'Unable to update the approval workflow.'); await refreshAuthoritativeState(); setError(errorMessage) }
    finally { setWorkflowSaving(false) }
  }
  const runLock = async () => {
    setWorkflowSaving(true); setError(undefined)
    try { await lockBatch(batchId); setLockConfirmationOpen(false); await refreshAuthoritativeState() }
    catch (reason) { const errorMessage = message(reason, 'Unable to lock the billing period.'); await refreshAuthoritativeState(); setError(errorMessage) }
    finally { setWorkflowSaving(false) }
  }
  const downloadReport = async (format: ReportFormat, filters?: ReportFilters) => { setDownloading(true); setError(undefined); try { await downloadReportAs(batchId, format, filters) } catch (reason) { setError(message(reason, `Unable to download the ${format === 'pdf' ? 'PDF' : 'Excel'} report.`)) } finally { setDownloading(false) } }
  const requestReportDownload = (format: ReportFormat, filters: ReportFilters) => { if ((summary?.unresolvedExceptionCount ?? 0) > 0) { setPendingFormat(format); setPendingFilters(filters); setDownloadWarningOpen(true) } else void downloadReport(format, filters) }

  const period = summary ? billingPeriod(summary.billingYear, summary.billingMonth) : 'Billing period'
  return <Stack spacing={2}>
    <ReviewHeader summary={summary} period={period} />
    {summary && <BatchContextNavigation batchId={batchId} status={summary.batchStatus} />}
    {summary && <WorkflowStepper status={summary.batchStatus} />}
    {capabilityError && <Alert severity="warning">Workflow actions are unavailable: {capabilityError}</Alert>}
    {summary && <WorkflowActions status={summary.batchStatus} capabilities={capabilities} disabled={workflowSaving} onOpen={openWorkflowDialog} onLock={() => setLockConfirmationOpen(true)} />}
    {summary && <ReviewKpis summary={summary} />}
    {summary && ['Completed', 'Locked'].includes(summary.batchStatus) && <ReportCard summary={summary} period={period} downloading={downloading} onDownload={requestReportDownload} />}
    {summary && <ApprovalHistorySection summary={summary} />}
    <ReviewFilters filters={filters} options={options} onChange={updateFilter} />
    {error && <ErrorState message={error} />}
    {loading && !rows && <LoadingState label="Loading review…" />}
    <BulkAssignBar selectedCount={selectedIds.size} matchingCount={rows?.totalCount ?? 0} selectable={canBulkAssign} onSelectAllMatching={selectAllMatching} onClear={clearSelection} onAssign={() => setBulkDialogOpen(true)} />
    <ReviewTable rows={rows} loading={loading} sort={sort} direction={direction} selectable={canBulkAssign} selectedIds={selectedIds} allOnPageSelected={allOnPageSelected} onToggleRow={toggleRow} onToggleAllOnPage={toggleAllOnPage} onSort={toggleSort} onOpen={row => void openDetail(row)} />
    <BillDetailDrawer detail={detail} batchStatus={summary?.batchStatus} onClose={() => setDetail(undefined)} onSaved={() => detail ? refreshAfterAssessment(detail.row.id) : Promise.resolve()} />
    <WorkflowActionDialog state={workflowDialog} comment={workflowComment} error={workflowDialogError} saving={workflowSaving} onComment={setWorkflowComment} onCancel={() => setWorkflowDialog(undefined)} onConfirm={() => void runWorkflow()} />
    <ConfirmActionDialog open={lockConfirmationOpen} title="Lock Billing Period" message="Locking this billing period will prevent further bill, matching, exception and deduction changes. Continue?" confirmLabel="Confirm Lock" busy={workflowSaving} onCancel={() => setLockConfirmationOpen(false)} onConfirm={() => void runLock()} />
    <ConfirmActionDialog open={downloadWarningOpen} title="Unresolved exceptions" message={exceptionDownloadWarning(summary?.unresolvedExceptionCount ?? 0)} confirmLabel="Download anyway" busy={downloading} onCancel={() => setDownloadWarningOpen(false)} onConfirm={() => { setDownloadWarningOpen(false); void downloadReport(pendingFormat, pendingFilters) }} />
    <BulkAssignDialog open={bulkDialogOpen} selectedIds={Array.from(selectedIds)} mobileNumberById={mobileNumberById} onClose={() => setBulkDialogOpen(false)} onCompleted={() => { setBulkDialogOpen(false); clearSelection(); setLoading(true); void load() }} />
  </Stack>
}

function message(reason: unknown, fallback: string) { return reason instanceof Error ? reason.message : fallback }
