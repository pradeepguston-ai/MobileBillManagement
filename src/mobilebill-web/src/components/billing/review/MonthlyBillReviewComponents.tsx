import { Alert, Box, Button, Card, CardContent, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, Drawer, FormControl, InputLabel, MenuItem, Paper, Select, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material'
import { useState, type FormEvent, type ReactNode } from 'react'

import { assessMonthlyBill, bulkAssessMonthlyBills, type AssessmentRequest, type BulkAssessmentResult, type ReviewDetail, type ReviewRow, type ReviewSummary } from '../../../api/billReviewApi'
import type { WorkflowCapabilities } from '../../../api/billWorkflowApi'
import type { PagedResponse } from '../../../api/types'
import { workflowStageLabel } from '../../../billing/workflowPresentation'
import { formatCurrency, formatDateTime } from '../../../utils/formatters'
import { ApprovalTimeline } from '../ApprovalTimeline'
import { AuditTimeline } from '../AuditTimeline'
import { BatchStatusChip } from '../BatchStatusChip'
import { BillChargeBreakdown } from '../BillChargeBreakdown'
import { ValidationLevelChip } from '../ValidationLevelChip'
import { FilterBar } from '../../common/FilterBar'
import { KpiCard } from '../../common/KpiCard'
import { SortableHeaderCell, TableEmptyRow } from '../../common/DataTable'
import { reviewColumns, type MasterOption, type ReviewColumn, type SelectOption, type WorkflowDialogState } from './reviewPresentation'
const moneyColumns = new Set<keyof ReviewRow>(['creditLimit', 'monthlyRental', 'actualBill', 'variance', 'calculatedExcess', 'finalDeduction'])
const filterDefaults: Record<string, string> = { search: '', factoryCode: '', departmentCode: '', categoryCode: '', responsibility: '', exception: 'All', status: '', calculatedExcessMin: '', calculatedExcessMax: '' }

export function ReviewHeader({ summary, period }: { summary?: ReviewSummary; period: string }) {
  return <Box><Typography component="h1" variant="h4">Monthly Bill Review</Typography><Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 0.5, alignItems: { sm: 'center' }, flexWrap: 'wrap' }}><Typography color="text.secondary">Billing Period: {period} · Provider: {summary?.provider ?? '—'} · Corporate Code: {summary?.corporateCode ?? '—'}</Typography>{summary && <><BatchStatusChip status={summary.batchStatus} /><ValidationLevelChip level={summary.validationLevel} /></>}</Stack>{summary?.batchStatus === 'Locked' && <Alert severity="info" sx={{ mt: 1 }}><strong>Locked Billing Period</strong> — billing details remain available in read-only mode.</Alert>}</Box>
}

export function ReviewKpis({ summary }: { summary: ReviewSummary }) {
  const cards: Array<[string, string | number]> = [['Total Accounts', summary.totalAccounts], ['Total Actual Bill', formatCurrency(summary.totalActualBill)], ['Total Calculated Excess', formatCurrency(summary.totalCalculatedExcess)], ['Total Final Deduction', formatCurrency(summary.totalFinalDeduction)], ['Company Responsibility Amount', formatCurrency(summary.companyResponsibilityAmount)], ['Exception Count', summary.exceptionCount], ['Unmatched Count', summary.unmatchedCount], ['Assessed Count', summary.assessedCount], ['Unassessed Count', summary.unassessedCount]]
  return <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 2 }}>{cards.map(([label, value]) => <KpiCard key={label} label={label} value={value} />)}</Box>
}

export function ReviewFilters({ filters, options, onChange }: { filters: Record<string, string>; options: { factories: MasterOption[]; departments: MasterOption[]; categories: MasterOption[] }; onChange: (key: string, value: string) => void }) {
  const activeCount = Object.entries(filterDefaults).filter(([key, fallback]) => (filters[key] ?? fallback) !== fallback).length
  const clear = () => Object.entries(filterDefaults).forEach(([key, fallback]) => onChange(key, fallback))
  return <Stack spacing={1.5}>
    <FilterBar activeCount={activeCount} onClear={clear}>
      <TextField size="small" label="Search mobile / EPF / employee / calling name" value={filters.search ?? ''} onChange={event => onChange('search', event.target.value)} />
      <MasterFilter label="Factory" value={filters.factoryCode ?? ''} onChange={value => onChange('factoryCode', value)} options={options.factories} />
      <MasterFilter label="Department" value={filters.departmentCode ?? ''} onChange={value => onChange('departmentCode', value)} options={options.departments} />
      <MasterFilter label="Category" value={filters.categoryCode ?? ''} onChange={value => onChange('categoryCode', value)} options={options.categories} />
      <FilterSelect label="Responsibility" value={filters.responsibility ?? ''} onChange={value => onChange('responsibility', value)} options={[['', 'All responsibilities'], ['Unassessed', 'Unassessed'], ['ByUser', 'By User'], ['ByCompany', 'By Company']]} />
      <FilterSelect label="Exception" value={filters.exception ?? 'All'} onChange={value => onChange('exception', value)} options={[['All', 'All exceptions'], ['HasException', 'Has exception'], ['NoException', 'No exception']]} />
      <FilterSelect label="Status" value={filters.status ?? ''} onChange={value => onChange('status', value)} options={[['', 'All statuses'], ['Pending', 'Pending'], ['Reviewed', 'Reviewed'], ['Approved', 'Approved'], ['Excluded', 'Excluded']]} />
    </FilterBar>
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Stack spacing={1}>
        <Typography variant="subtitle2" color="text.secondary">Calculated Excess Range</Typography>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
          <TextField size="small" label="Min Calculated Excess" type="number" value={filters.calculatedExcessMin ?? ''} onChange={event => onChange('calculatedExcessMin', event.target.value)} sx={{ width: 170 }} slotProps={{ htmlInput: { min: 0, step: '0.01' } }} />
          <TextField size="small" label="Max Calculated Excess" type="number" value={filters.calculatedExcessMax ?? ''} onChange={event => onChange('calculatedExcessMax', event.target.value)} sx={{ width: 170 }} slotProps={{ htmlInput: { min: 0, step: '0.01' } }} />
        </Stack>
      </Stack>
    </Paper>
  </Stack>
}

export function ReviewTable({ rows, loading, sort, direction, selectable, selectedIds, allOnPageSelected, onToggleRow, onToggleAllOnPage, onSort, onOpen }: { rows?: PagedResponse<ReviewRow>; loading: boolean; sort: string; direction: string; selectable: boolean; selectedIds: Set<string>; allOnPageSelected: boolean; onToggleRow: (id: string) => void; onToggleAllOnPage: () => void; onSort: (column: ReviewColumn) => void; onOpen: (row: ReviewRow) => void }) {
  const someOnPageSelected = !allOnPageSelected && (rows?.items.some(row => selectedIds.has(row.id)) ?? false)
  return <Box sx={{ maxWidth: '100%', overflowX: 'auto' }}><Table stickyHeader size="small" aria-label="Monthly bill review rows" sx={{ minWidth: 1900 }}><TableHead><TableRow><TableCell padding="checkbox"><Checkbox slotProps={{ input: { 'aria-label': 'Select all rows on this page' } }} checked={allOnPageSelected} indeterminate={someOnPageSelected} disabled={!selectable || !rows?.items.length} onChange={onToggleAllOnPage} /></TableCell>{reviewColumns.map(column => <SortableHeaderCell key={column.key} label={column.label} sortable={Boolean(column.sortKey)} active={sort === column.sortKey} direction={direction === 'asc' ? 'asc' : 'desc'} onClick={() => onSort(column)} />)}</TableRow></TableHead><TableBody>{!loading && rows?.items.map(row => <TableRow hover key={row.id} onClick={() => onOpen(row)} sx={{ cursor: 'pointer', bgcolor: row.hasException ? 'rgba(237, 108, 2, 0.08)' : undefined }}><TableCell padding="checkbox" onClick={event => event.stopPropagation()}><Checkbox slotProps={{ input: { 'aria-label': `Select ${row.mobileNumber}` } }} checked={selectedIds.has(row.id)} disabled={!selectable} onChange={() => onToggleRow(row.id)} /></TableCell>{reviewColumns.map(column => <TableCell key={column.key} sx={{ whiteSpace: 'nowrap' }}>{moneyColumns.has(column.key) ? formatCurrency(row[column.key] as number) : column.key === 'responsibility' ? row.responsibility ?? 'Unassessed' : String(row[column.key] ?? '—')}</TableCell>)}</TableRow>)}{!loading && rows?.items.length === 0 && <TableEmptyRow colSpan={reviewColumns.length + 1} message="No monthly bill rows match the selected filters." />}</TableBody></Table></Box>
}

export function BulkAssignBar({ selectedCount, matchingCount, selectable, selectingAll, onSelectAllMatching, onClear, onAssign }: { selectedCount: number; matchingCount: number; selectable: boolean; selectingAll?: boolean; onSelectAllMatching: () => void; onClear: () => void; onAssign: () => void }) {
  if (selectedCount === 0 || !selectable) return null
  return <Card sx={{ bgcolor: 'primary.light' }}><CardContent sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 2, '&:last-child': { pb: 2 } }}>
    <Typography variant="body2" sx={{ fontWeight: 600 }}>{selectedCount} selected</Typography>
    {selectedCount < matchingCount && <Button size="small" disabled={selectingAll} onClick={onSelectAllMatching}>{selectingAll ? 'Selecting…' : `Select all ${matchingCount} matching filters`}</Button>}
    <Button size="small" onClick={onClear}>Clear selection</Button>
    <Button variant="contained" size="small" onClick={onAssign} sx={{ ml: 'auto' }}>Assign Responsibility</Button>
  </CardContent></Card>
}

export function BulkAssignDialog({ open, selectedIds, mobileNumberById, onClose, onCompleted }: { open: boolean; selectedIds: string[]; mobileNumberById: Record<string, string>; onClose: () => void; onCompleted: () => void }) {
  const [responsibility, setResponsibility] = useState<'ByUser' | 'ByCompany' | ''>('')
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string>()
  const [saving, setSaving] = useState(false)
  const [result, setResult] = useState<BulkAssessmentResult>()
  const reset = () => { setResponsibility(''); setReason(''); setError(undefined); setResult(undefined) }
  const close = () => { reset(); onClose() }
  const submit = async () => {
    if (!responsibility) return
    setSaving(true); setError(undefined)
    try { setResult(await bulkAssessMonthlyBills({ monthlyBillIds: selectedIds, responsibility, reason: reason.trim() || null })) }
    catch (reason_) { setError(reason_ instanceof Error ? reason_.message : 'Unable to apply the bulk assignment.') }
    finally { setSaving(false) }
  }
  const finish = () => { reset(); onCompleted() }
  return <Dialog open={open} onClose={close} fullWidth maxWidth="sm" aria-labelledby="bulk-assign-dialog-title"><DialogTitle id="bulk-assign-dialog-title">Assign Responsibility to {selectedIds.length} bill{selectedIds.length === 1 ? '' : 's'}</DialogTitle><DialogContent><Stack spacing={2} sx={{ pt: 1 }}>
    {!result && <>
      <Typography variant="body2" color="text.secondary">The deduction for each bill is set automatically — 0.00 for By Company, or that bill's own Calculated Excess for By User. Use the row-level detail panel for a custom deduction amount instead.</Typography>
      <FilterSelect label="Responsibility" value={responsibility} onChange={value => setResponsibility(value as 'ByUser' | 'ByCompany')} options={[["", 'Select responsibility'], ['ByUser', 'By User'], ['ByCompany', 'By Company']]} />
      <TextField label="Reason" multiline minRows={2} value={reason} onChange={event => setReason(event.target.value)} helperText="Required for any selected bill that is already assessed and would change." />
      {error && <Alert severity="error">{error}</Alert>}
    </>}
    {result && <>
      <Alert severity={result.failureCount === 0 ? 'success' : 'warning'}>{result.successCount} succeeded, {result.failureCount} failed.</Alert>
      {result.items.filter(item => !item.success).map(item => <Typography key={item.monthlyBillId} variant="body2" color="text.secondary">{mobileNumberById[item.monthlyBillId] ?? item.monthlyBillId}: {item.error}</Typography>)}
    </>}
  </Stack></DialogContent><DialogActions>
    {!result && <><Button onClick={close}>Cancel</Button><Button variant="contained" disabled={!responsibility || saving} onClick={() => void submit()}>{saving ? 'Applying…' : 'Apply'}</Button></>}
    {result && <Button variant="contained" onClick={finish}>Close</Button>}
  </DialogActions></Dialog>
}

export function BillDetailDrawer({ detail, batchStatus, onClose, onSaved }: { detail?: ReviewDetail; batchStatus?: string; onClose: () => void; onSaved: () => Promise<void> }) {
  return <Drawer anchor="right" open={Boolean(detail)} onClose={onClose} slotProps={{ paper: { sx: { width: { xs: '100%', sm: 'min(680px, 92vw)' }, p: { xs: 2, sm: 3 } } } }}>{detail && <Stack spacing={2}><Typography variant="h5">{detail.row.mobileNumber}</Typography>
    <Section title="Employee Snapshot"><SectionValues values={{ EPF: detail.row.employeeEpf, Name: detail.row.employeeName, 'Calling Name': detail.row.callingName, Category: detail.row.category, Designation: detail.row.designation, Factory: detail.row.factory, Department: detail.row.department }} /></Section>
    <Section title="Allocation Snapshot"><SectionValues values={{ 'Credit Limit': formatCurrency(detail.row.creditLimit), 'Monthly Rental': formatCurrency(detail.row.monthlyRental) }} /></Section>
    <Section title="PDF Charge Breakdown"><BillChargeBreakdown charges={detail.charges} /></Section>
    <Section title="Calculation"><SectionValues values={{ 'Actual Bill': formatCurrency(detail.row.actualBill), 'Available Entitlement': formatCurrency(detail.row.availableEntitlement), Variance: formatCurrency(detail.row.variance), 'Calculated Excess': formatCurrency(detail.row.calculatedExcess), Responsibility: displayResponsibility(detail.row.responsibility), 'Final Deduction': formatCurrency(detail.row.finalDeduction), Status: detail.row.status }} /></Section>
    <Section title="Assessment"><SectionValues values={{ 'Assessed By': detail.assessedBy ?? 'Not assessed', 'Assessed At': detail.assessedAt ? formatDateTime(detail.assessedAt) : 'Not assessed', 'Deduction Override Reason': detail.deductionOverrideReason ?? 'None' }} />{batchStatus === 'Locked' ? <Alert severity="info" sx={{ mt: 1 }}>Assessment is read-only because this billing batch is locked.</Alert> : <AssessmentSection key={detail.row.id} detail={detail} onSaved={onSaved} />}</Section>
    <Section title="Exceptions"><TextTimeline entries={detail.exceptions} empty="None" /></Section>
    <Section title="Approval History"><TextTimeline entries={detail.approvalHistory} empty="No approval history yet" /></Section>
    <Section title="Audit History"><AuditTimeline entries={detail.auditHistory} /></Section>
  </Stack>}</Drawer>
}

function AssessmentSection({ detail, onSaved }: { detail: ReviewDetail; onSaved: () => Promise<void> }) {
  const initialResponsibility = detail.row.responsibility === 'ByUser' || detail.row.responsibility === 'ByCompany' ? detail.row.responsibility : ''
  const [responsibility, setResponsibility] = useState<'ByUser' | 'ByCompany' | ''>(initialResponsibility)
  const [finalDeduction, setFinalDeduction] = useState(String(detail.row.finalDeduction))
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string>()
  const [saving, setSaving] = useState(false)
  const chooseResponsibility = (value: 'ByUser' | 'ByCompany') => { setResponsibility(value); setFinalDeduction(String(value === 'ByCompany' ? 0 : detail.row.calculatedExcess)); setError(undefined) }
  const submit = async (event: FormEvent) => {
    event.preventDefault(); setError(undefined)
    if (!responsibility) { setError('Select a responsibility.'); return }
    const deduction = Number(finalDeduction)
    if (!Number.isFinite(deduction) || deduction < 0 || deduction > detail.row.calculatedExcess) { setError(`Final deduction must be between 0.00 and ${detail.row.calculatedExcess.toFixed(2)}.`); return }
    if (responsibility === 'ByCompany' && deduction !== 0) { setError('By Company assessments must have a final deduction of 0.00.'); return }
    if (responsibility === 'ByUser' && deduction !== detail.row.calculatedExcess && !reason.trim()) { setError('Enter an override reason when the deduction differs from Calculated Excess.'); return }
    if (detail.assessedAt && (responsibility !== detail.row.responsibility || deduction !== detail.row.finalDeduction) && !reason.trim()) { setError('Enter a reason when changing an existing assessment.'); return }
    const request: AssessmentRequest = { responsibility, finalDeduction: deduction, reason: reason.trim() || null }
    setSaving(true)
    try { await assessMonthlyBill(detail.row.id, request); await onSaved() }
    catch (problem) { setError(message(problem, 'Unable to save the assessment.')) }
    finally { setSaving(false) }
  }
  return <Box component="form" aria-label="Monthly bill assessment" onSubmit={event => void submit(event)} sx={{ mt: 1 }}><Stack spacing={1.5}><FilterSelect label="Responsibility" value={responsibility} onChange={value => chooseResponsibility(value as 'ByUser' | 'ByCompany')} options={[["", 'Select responsibility'], ['ByUser', 'By User'], ['ByCompany', 'By Company']]} /><TextField label="Final Deduction" type="number" value={finalDeduction} disabled={responsibility === 'ByCompany'} onChange={event => setFinalDeduction(event.target.value)} slotProps={{ htmlInput: { min: 0, max: detail.row.calculatedExcess, step: '0.01' } }} /><TextField label="Reason" multiline minRows={2} value={reason} onChange={event => setReason(event.target.value)} helperText="Required for deduction overrides and reassessments." />{error && <Alert severity="error">{error}</Alert>}<Button type="submit" variant="contained" disabled={saving}>{saving ? 'Saving…' : 'Save Assessment'}</Button></Stack></Box>
}

export function WorkflowActions({ status, capabilities, disabled, onOpen, onLock }: { status?: string; capabilities?: WorkflowCapabilities; disabled: boolean; onOpen: (state: WorkflowDialogState) => void; onLock: () => void }) {
  if (!status || capabilities?.status !== status) return null
  if (status === 'Validated' && capabilities.canSubmit) return <Button variant="contained" disabled={disabled} onClick={() => onOpen({ kind: 'submit', title: 'Submit for IT Review', commentRequired: false })}>Submit for IT Review</Button>
  if (status === 'Completed' && capabilities.canLock) return <Button variant="contained" color="warning" disabled={disabled} onClick={onLock}>Lock Billing Period</Button>
  if (!['ITReview', 'HRApproval', 'FinanceApproval'].includes(status)) return null
  const stage = workflowStageLabel(status)
  return <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
    {capabilities.canApprove && <Button variant="contained" disabled={disabled} onClick={() => onOpen({ kind: 'decision', action: 'Approve', title: `Approve ${stage}`, commentRequired: false })}>Approve {stage}</Button>}
    {capabilities.canReturnForCorrection && <Button disabled={disabled} onClick={() => onOpen({ kind: 'decision', action: 'ReturnForCorrection', title: `Return ${stage} for Correction`, commentRequired: true })}>Return {stage} for Correction</Button>}
    {capabilities.canReject && <Button color="error" disabled={disabled} onClick={() => onOpen({ kind: 'decision', action: 'Reject', title: `Reject ${stage}`, commentRequired: true })}>Reject {stage}</Button>}
  </Stack>
}

export function WorkflowActionDialog({ state, comment, error, saving, onComment, onCancel, onConfirm }: { state?: WorkflowDialogState; comment: string; error?: string; saving: boolean; onComment: (value: string) => void; onCancel: () => void; onConfirm: () => void }) {
  return <Dialog open={Boolean(state)} onClose={onCancel} fullWidth maxWidth="sm" aria-labelledby="workflow-dialog-title"><DialogTitle id="workflow-dialog-title">{state?.title}</DialogTitle><DialogContent><TextField autoFocus fullWidth multiline minRows={3} margin="dense" label={state?.commentRequired ? 'Reason' : 'Comment'} required={state?.commentRequired} value={comment} onChange={event => onComment(event.target.value)} helperText={state?.commentRequired ? 'A meaningful reason is required.' : 'Optional'} />{error && <Alert severity="error" sx={{ mt: 1 }}>{error}</Alert>}</DialogContent><DialogActions><Button onClick={onCancel}>Cancel</Button><Button variant="contained" disabled={saving} onClick={onConfirm}>{state?.kind === 'submit' ? 'Confirm Submit' : 'Confirm Action'}</Button></DialogActions></Dialog>
}

export function ReportCard({ summary, period, downloading, onDownload }: { summary: ReviewSummary; period: string; downloading: boolean; onDownload: () => void }) {
  return <Card variant="outlined"><CardContent><Stack spacing={1} sx={{ alignItems: 'flex-start' }}><Typography variant="h6">Monthly Bill Report</Typography><SectionValues values={{ 'Billing Period': period, 'Account Count': String(summary.totalAccounts), 'Actual Bill Total': formatCurrency(summary.totalActualBill), 'Final Deduction Total': formatCurrency(summary.totalFinalDeduction), 'Batch Status': summary.batchStatus }} /><Button variant="contained" disabled={downloading} onClick={onDownload}>{downloading ? 'Downloading…' : 'Download Excel Report'}</Button></Stack></CardContent></Card>
}

export function ApprovalHistorySection({ summary }: { summary: ReviewSummary }) { return <Card variant="outlined"><CardContent><Typography variant="h6" sx={{ mb: 1 }}>Approval History</Typography><ApprovalTimeline history={summary.approvalHistory ?? []} /></CardContent></Card> }

function Section({ title, children }: { title: string; children: ReactNode }) { return <Box><Typography variant="subtitle1">{title}</Typography>{children}</Box> }
function SectionValues({ values }: { values: Record<string, string | undefined> }) { return <>{Object.entries(values).map(([key, value]) => <Typography key={key} variant="body2" sx={{ whiteSpace: 'pre-line' }}><strong>{key}:</strong> {value ?? '—'}</Typography>)}</> }
function TextTimeline({ entries, empty }: { entries: string[]; empty: string }) { return entries.length === 0 ? <Typography color="text.secondary">{empty}</Typography> : <AuditTimeline entries={entries} /> }
function displayResponsibility(value?: string) { return value === 'ByUser' ? 'By User' : value === 'ByCompany' ? 'By Company' : 'Unassessed' }
function FilterSelect({ label, value, onChange, options }: { label: string; value: string; onChange: (value: string) => void; options: SelectOption[] }) { const labelId = `review-filter-${label.toLowerCase().replaceAll(' ', '-')}`; return <FormControl size="small" sx={{ minWidth: 170 }}><InputLabel id={labelId}>{label}</InputLabel><Select labelId={labelId} label={label} value={value} onChange={event => onChange(event.target.value)}>{options.map(([optionValue, optionLabel]) => <MenuItem key={optionValue || 'all'} value={optionValue}>{optionLabel}</MenuItem>)}</Select></FormControl> }
function MasterFilter({ label, value, onChange, options }: { label: string; value: string; onChange: (value: string) => void; options: MasterOption[] }) { return <FilterSelect label={label} value={value} onChange={onChange} options={[['', `All ${label.toLowerCase()}s`], ...options.map(option => [String(option.code ?? option.id), option.name ?? option.id] as const)]} /> }
function message(reason: unknown, fallback: string) { return reason instanceof Error ? reason.message : fallback }
