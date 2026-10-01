import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { MonthlyBillReviewPage } from './MonthlyBillReviewPage'

const row = { id: 'bill-1', mobileNumber: '768791861', employeeEpf: 'EPF-1', employeeName: 'Employee One', callingName: 'Sam', category: 'Executive', designation: 'Manager', factory: 'Factory', department: 'IT', creditLimit: 100, monthlyRental: 10, availableEntitlement: 110, actualBill: 150, variance: -40, calculatedExcess: 40, responsibility: 'ByUser', finalDeduction: 40, remark: null, status: 'Reviewed', hasException: false, isAssessed: true }
const approval = { id: 'approval-1', workflowStage: 'ITReview', action: 'Approve', workflowRole: 'ITReviewer', userRole: 'ITEngineer', userId: 'it-user', displayName: 'IT User', timestamp: '2026-09-10T04:00:00Z', comment: 'Checked', previousStatus: 'ITReview', newStatus: 'HRApproval' }
const baseSummary = { batchId: 'batch-1', billingYear: 2026, billingMonth: 8, provider: 'Telecom', corporateCode: 'CORP', batchStatus: 'Validated', validationLevel: 'StructuralOnly', totalAccounts: 1, totalActualBill: 150, totalCalculatedExcess: 40, totalFinalDeduction: 40, companyResponsibilityAmount: 0, exceptionCount: 0, unmatchedCount: 0, assessedCount: 1, unassessedCount: 0, approvalHistory: [] }
const capabilityDefaults = { canSubmit: false, canApprove: false, canReject: false, canReturnForCorrection: false, canLock: false, currentStage: 'Validation', status: 'Validated' }
const json = (body: unknown, status = 200, headers: Record<string, string> = {}) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json', ...headers } })

type HarnessOptions = {
  status: string
  capabilities?: Partial<typeof capabilityDefaults>
  approvalHistory?: typeof approval[]
  capabilityFailure?: boolean
  commandFailure?: { status: number; detail: string }
  exportFailure?: string
}

function installHarness(options: HarnessOptions) {
  let status = options.status
  const requests: Array<{ url: string; body?: Record<string, unknown> }> = []
  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = String(input)
    const body = init?.body ? JSON.parse(String(init.body)) as Record<string, unknown> : undefined
    requests.push({ url, body })
    if (url.includes('/api/factories') || url.includes('/api/departments') || url.includes('/api/categories')) return json({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })
    if (url.endsWith('/workflow-capabilities')) {
      if (options.capabilityFailure) return json({ detail: 'Unable to determine workflow authorization.' }, 500)
      return json({ ...capabilityDefaults, ...options.capabilities, status })
    }
    if (url.endsWith('/workflow/submit')) {
      if (options.commandFailure) return json({ detail: options.commandFailure.detail }, options.commandFailure.status)
      status = 'ITReview'; return json({ batchId: 'batch-1', status })
    }
    if (url.endsWith('/workflow/decision')) {
      if (options.commandFailure) return json({ detail: options.commandFailure.detail }, options.commandFailure.status)
      if (body?.action === 'Approve') status = status === 'ITReview' ? 'HRApproval' : status === 'HRApproval' ? 'FinanceApproval' : 'Completed'
      else status = 'Validated'
      return json({ batchId: 'batch-1', status })
    }
    if (url.endsWith('/lock')) { status = 'Locked'; return json({ batchId: 'batch-1', status }) }
    if (url.endsWith('/excel')) {
      if (options.exportFailure) return json({ detail: options.exportFailure }, 409)
      return new Response('xlsx', { headers: { 'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet', 'Content-Disposition': 'attachment; filename="August-2026-Mobile-Bill.xlsx"' } })
    }
    if (url.includes('/rows?')) return json({ items: [row], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 })
    return json({ ...baseSummary, batchStatus: status, approvalHistory: options.approvalHistory ?? [] })
  })
  return { requests, getStatus: () => status }
}

function renderPage() { return render(<MemoryRouter initialEntries={['/billing/batch-1/review']}><Routes><Route path="/billing/:batchId/review" element={<MonthlyBillReviewPage />} /></Routes></MemoryRouter>) }

describe('MonthlyBillReviewPage workflow and reporting', () => {
  beforeEach(() => {
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:report') })
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() })
  })
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('renders the visual workflow using authoritative current status', async () => {
    installHarness({ status: 'HRApproval', capabilities: { canApprove: true, canReject: true, canReturnForCorrection: true, currentStage: 'HRApproval' } }); renderPage()

    await screen.findByText('Employee One')
    for (const stage of ['Validation', 'IT Review', 'HR Approval', 'Finance Approval', 'Completed', 'Locked']) expect(screen.getAllByText(stage).length).toBeGreaterThan(0)
    expect(screen.getByText('Current stage: HR Approval')).toBeTruthy()
  })

  it.each([
    ['ITReview', 'Approve IT Review'],
    ['HRApproval', 'Approve HR Approval'],
    ['FinanceApproval', 'Approve Finance Approval'],
  ])('shows contextual decisions for %s only when server capabilities allow them', async (status, approveLabel) => {
    installHarness({ status, capabilities: { canApprove: true, canReject: true, canReturnForCorrection: true, currentStage: status } }); renderPage()

    expect(await screen.findByRole('button', { name: approveLabel })).toBeTruthy()
    expect(screen.getByRole('button', { name: `Reject ${approveLabel.replace('Approve ', '')}` })).toBeTruthy()
    expect(screen.getByRole('button', { name: `Return ${approveLabel.replace('Approve ', '')} for Correction` })).toBeTruthy()
  })

  it('does not enable an action from status alone when server authorization is false', async () => {
    installHarness({ status: 'ITReview' }); renderPage()
    await screen.findByText('Employee One')
    expect(screen.queryByRole('button', { name: /Approve IT Review/ })).toBeNull()
  })

  it('uses the reusable Submit dialog and allows an optional comment', async () => {
    const harness = installHarness({ status: 'Validated', capabilities: { canSubmit: true } }); renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Submit for IT Review' }))
    expect(screen.getByRole('dialog', { name: 'Submit for IT Review' })).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Submit' }))

    await waitFor(() => expect(harness.requests.some(request => request.url.endsWith('/workflow/submit') && request.body?.comment === null)).toBe(true))
  })

  it.each([['Reject IT Review', 'Reject'], ['Return IT Review for Correction', 'ReturnForCorrection']])('%s requires a meaningful reason before sending', async (buttonName, action) => {
    const harness = installHarness({ status: 'ITReview', capabilities: { canApprove: true, canReject: true, canReturnForCorrection: true } }); renderPage()
    fireEvent.click(await screen.findByRole('button', { name: buttonName }))
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Action' }))

    expect(await screen.findByText('Enter a meaningful reason.')).toBeTruthy()
    expect(harness.requests.some(request => request.url.endsWith('/workflow/decision') && request.body?.action === action)).toBe(false)
  })

  it('refetches Finance approval and displays Completed without locking it', async () => {
    const harness = installHarness({ status: 'FinanceApproval', capabilities: { canApprove: true, canReject: true, canReturnForCorrection: true } }); renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Approve Finance Approval' }))
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Action' }))

    expect(await screen.findByText('Current stage: Completed')).toBeTruthy()
    expect(harness.getStatus()).toBe('Completed')
    expect(harness.requests.some(request => request.url.endsWith('/lock'))).toBe(false)
  })

  it('shows Lock only for Completed plus CanLock and requires confirmation', async () => {
    const harness = installHarness({ status: 'Completed', capabilities: { canLock: true, currentStage: 'Completed' } }); renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Lock Billing Period' }))
    expect(screen.getByText('Locking this billing period will prevent further bill, matching, exception and deduction changes. Continue?')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Lock' }))

    await waitFor(() => expect(harness.requests.some(request => request.url.endsWith('/lock'))).toBe(true))
  })

  it('hides Lock for Completed when CanLock is false', async () => {
    installHarness({ status: 'Completed' }); renderPage()
    await screen.findByText('Employee One')
    expect(screen.queryByRole('button', { name: 'Lock Billing Period' })).toBeNull()
  })

  it('renders actual chronological approval history and the empty state', async () => {
    installHarness({ status: 'HRApproval', approvalHistory: [approval] }); const view = renderPage()
    expect(await screen.findByText('IT User')).toBeTruthy()
    expect(screen.getByText('Checked')).toBeTruthy()
    expect(screen.getByRole('columnheader', { name: 'User Role' })).toBeTruthy()
    expect(screen.queryByRole('columnheader', { name: 'Workflow Role' })).toBeNull()
    expect(screen.getByText('IT Engineer')).toBeTruthy()

    view.unmount(); cleanup(); vi.restoreAllMocks()
    installHarness({ status: 'Validated' }); renderPage()
    expect(await screen.findByText('No approval history yet')).toBeTruthy()
  })

  it.each(['Completed', 'Locked'])('shows the server-valued report card and download for %s', async status => {
    installHarness({ status }); renderPage()
    expect(await screen.findByText('Monthly Bill Report')).toBeTruthy()
    expect(screen.getByText('Account Count:')).toBeTruthy()
    expect(screen.getByText('Actual Bill Total:')).toBeTruthy()
    expect(screen.getByText('Final Deduction Total:')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Download Excel Report' })).toBeTruthy()
    if (status === 'Locked') expect(screen.getByText('Locked Billing Period')).toBeTruthy()
  })

  it('hides the report before Completed', async () => {
    installHarness({ status: 'FinanceApproval' }); renderPage()
    await screen.findByText('Employee One')
    expect(screen.queryByText('Monthly Bill Report')).toBeNull()
  })

  it('downloads Excel with the server filename', async () => {
    let downloadedName = ''
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) { downloadedName = this.download })
    installHarness({ status: 'Completed' }); renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Download Excel Report' }))

    await waitFor(() => expect(downloadedName).toBe('August-2026-Mobile-Bill.xlsx'))
  })

  it('shows reconciliation errors without creating a download', async () => {
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
    installHarness({ status: 'Completed', exportFailure: 'The report totals do not reconcile.' }); renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Download Excel Report' }))

    expect(await screen.findByText('The report totals do not reconcile.')).toBeTruthy()
    expect(click).not.toHaveBeenCalled()
  })

  it('fails closed when capability lookup fails while keeping the review readable', async () => {
    installHarness({ status: 'Completed', capabilityFailure: true }); renderPage()
    expect(await screen.findByText('Employee One')).toBeTruthy()
    expect(screen.getByText('Workflow actions are unavailable: Unable to determine workflow authorization.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Lock Billing Period' })).toBeNull()
  })

  it('shows a stale-state conflict and refreshes authoritative summary and capabilities', async () => {
    const harness = installHarness({ status: 'ITReview', capabilities: { canApprove: true }, commandFailure: { status: 409, detail: 'The batch status changed.' } }); renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Approve IT Review' }))
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Action' }))

    expect(await screen.findByText('The batch status changed.')).toBeTruthy()
    await waitFor(() => expect(harness.requests.filter(request => request.url.endsWith('/workflow-capabilities')).length).toBeGreaterThan(1))
  })

  it('shows a server authorization denial and does not guess a new status', async () => {
    const harness = installHarness({ status: 'ITReview', capabilities: { canApprove: true }, commandFailure: { status: 403, detail: 'The current user is not authorized to perform this bill-review action.' } }); renderPage()
    fireEvent.click(await screen.findByRole('button', { name: 'Approve IT Review' }))
    fireEvent.click(screen.getByRole('button', { name: 'Confirm Action' }))

    expect(await screen.findByText('The current user is not authorized to perform this bill-review action.')).toBeTruthy()
    expect(harness.getStatus()).toBe('ITReview')
  })
})
