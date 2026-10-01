import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { MonthlyBillReviewPage } from './MonthlyBillReviewPage'

const summary = { batchId: 'batch-1', billingYear: 2026, billingMonth: 8, provider: 'Telecom', corporateCode: 'CORP', batchStatus: 'Validated', validationLevel: 'StructuralOnly', totalAccounts: 1, totalActualBill: 150, totalCalculatedExcess: 40, totalFinalDeduction: 0, companyResponsibilityAmount: 0, exceptionCount: 1, unmatchedCount: 0, assessedCount: 0, unassessedCount: 1 }
const row = { id: 'bill-1', mobileNumber: '768791861', employeeEpf: 'EPF-1', employeeName: 'Employee One', callingName: 'Sam', category: 'Executive', designation: 'Manager', factory: 'Main Factory', department: 'IT', creditLimit: 100, monthlyRental: 10, availableEntitlement: 110, actualBill: 150, variance: -40, calculatedExcess: 40, responsibility: null, finalDeduction: 0, remark: null, status: 'Pending', hasException: true, isAssessed: false }
const charges = { previousDue: 1, payments: -2, totalUsage: 3, idd: 4, roaming: 5, vas: 6, discounts: -7, billAdjustments: 8, commitmentCharges: 9, latePaymentCharges: 10, addToBill: 11, instalmentPlans: 12, governmentTaxesLevies: 13, vat: 14, chargesForBillPeriod: 149, totalDueAmount: 150 }
const detail = { row, entitlementEffectiveFrom: '2026-08-01', entitlementEffectiveTo: null, charges, exceptions: ['ZERO_BILL'], approvalHistory: [], auditHistory: ['Matched by dev-user'], assessedBy: null, assessedAt: null, deductionOverrideReason: null }
const paged = (items: Array<Record<string, unknown>> = [row], totalPages = 1) => ({ items, pageNumber: 1, pageSize: 20, totalCount: items.length, totalPages })
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' } })

function renderPage() {
  return render(<MemoryRouter initialEntries={['/billing/batch-1/review']}><Routes><Route path="/billing/:batchId/review" element={<MonthlyBillReviewPage />} /></Routes></MemoryRouter>)
}

function mockReview(options: { status?: string; detailAfterSave?: { row: Record<string, unknown>; [key: string]: unknown }; assessmentError?: { status: number; detail: string }; totalPages?: number; bulkResult?: { successCount: number; failureCount: number; items: Array<{ monthlyBillId: string; success: boolean; error: string | null }> } } = {}) {
  let saved = false
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
    const url = String(input)
    if (url.includes('/api/factories') || url.includes('/api/departments') || url.includes('/api/categories')) return json({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })
    if (url.endsWith('/bulk-assessment')) return json(options.bulkResult ?? { successCount: 1, failureCount: 0, items: [{ monthlyBillId: 'bill-1', success: true, error: null }] })
    if (url.endsWith('/assessment')) {
      if (options.assessmentError) return json({ title: 'Assessment failed', detail: options.assessmentError.detail }, options.assessmentError.status)
      saved = true
      return json({ monthlyBillId: 'bill-1', responsibility: 'ByUser', actualBill: 150, availableEntitlement: 110, variance: -40, calculatedExcess: 40, finalDeduction: 40, assessedAt: '2026-09-09T05:00:00Z', assessedBy: 'dev-user', deductionOverrideAmount: null, deductionOverrideReason: null, deductionOverrideBy: null, deductionOverrideAt: null })
    }
    if (url.includes('/rows/bill-1')) return json(saved && options.detailAfterSave ? options.detailAfterSave : detail)
    if (url.includes('/rows?')) return json(paged(saved && options.detailAfterSave ? [options.detailAfterSave.row] : [row], options.totalPages ?? 1))
    return json({ ...summary, batchStatus: options.status ?? 'Validated', assessedCount: saved ? 1 : 0, unassessedCount: saved ? 0 : 1, totalFinalDeduction: saved ? 40 : 0 })
  })
}

async function openDrawer() {
  fireEvent.click(await screen.findByText('Employee One'))
  return screen.findByRole('form', { name: 'Monthly bill assessment' })
}

describe('MonthlyBillReviewPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('renders the complete server-provided KPI set and review row', async () => {
    mockReview(); renderPage()

    expect(await screen.findByText('Employee One')).toBeTruthy()
    for (const label of ['Total Accounts', 'Total Actual Bill', 'Total Calculated Excess', 'Total Final Deduction', 'Company Responsibility Amount', 'Exception Count', 'Unmatched Count', 'Assessed Count', 'Unassessed Count']) expect(screen.getByText(label)).toBeTruthy()
    expect(screen.getByText('Billing Period: August 2026', { exact: false })).toBeTruthy()
    expect(screen.getByText('Sam')).toBeTruthy()
    expect(screen.getAllByText('Unassessed').length).toBeGreaterThan(0)
  })

  it('keeps review filters sorting and paging on server requests', async () => {
    const fetchMock = mockReview({ totalPages: 2 }); renderPage()
    await screen.findByText('Employee One')

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Responsibility' }))
    fireEvent.click(screen.getByRole('option', { name: 'By User' }))
    fireEvent.change(screen.getByLabelText('Search mobile / EPF / employee / calling name'), { target: { value: 'Sam' } })
    fireEvent.click(screen.getByText('EPF'))
    fireEvent.click(screen.getByRole('button', { name: 'Go to page 2' }))

    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) => {
      const url = String(input)
      return url.includes('/rows?') && url.includes('responsibility=ByUser') && url.includes('search=Sam') && url.includes('sortBy=epf') && url.includes('pageNumber=2')
    })).toBe(true))
  })

  it('filters rows by a Calculated Excess amount range', async () => {
    const fetchMock = mockReview(); renderPage()
    await screen.findByText('Employee One')

    fireEvent.change(screen.getByLabelText('Min Calculated Excess'), { target: { value: '100' } })
    fireEvent.change(screen.getByLabelText('Max Calculated Excess'), { target: { value: '500' } })

    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) => {
      const url = String(input)
      return url.includes('/rows?') && url.includes('calculatedExcessMin=100') && url.includes('calculatedExcessMax=500')
    })).toBe(true))
  })

  it('opens a structured snapshot, charge, calculation and history drawer', async () => {
    mockReview(); renderPage()
    await openDrawer()

    for (const title of ['Employee Snapshot', 'Allocation Snapshot', 'PDF Charge Breakdown', 'Calculation', 'Assessment', 'Exceptions', 'Audit History']) expect(screen.getAllByText(title).length).toBeGreaterThan(0)
    expect(screen.getAllByText('Approval History').length).toBeGreaterThan(0)
    for (const label of ['Previous Due', 'Payments', 'Total Usage', 'IDD', 'Roaming', 'VAS', 'Discounts', 'Bill Adjustments', 'Commitment Charges', 'Late Payment Charges', 'Add To Bill', 'Instalment Plans', 'Government Taxes / Levies', 'VAT', 'Charges For Bill Period', 'Total Due']) expect(screen.getByText(label, { exact: true })).toBeTruthy()
    expect(screen.getAllByText('No approval history yet').length).toBeGreaterThan(0)
    expect(screen.getByText('ZERO_BILL')).toBeTruthy()
  })

  it('assesses By User with the server default and refreshes drawer grid and KPIs', async () => {
    const assessedDetail = { ...detail, row: { ...row, responsibility: 'ByUser', finalDeduction: 40, isAssessed: true }, assessedBy: 'dev-user', assessedAt: '2026-09-09T05:00:00Z' }
    const fetchMock = mockReview({ detailAfterSave: assessedDetail }); renderPage()
    const form = await openDrawer()

    fireEvent.mouseDown(within(form).getByRole('combobox', { name: 'Responsibility' }))
    fireEvent.click(screen.getByRole('option', { name: 'By User' }))
    expect((within(form).getByLabelText('Final Deduction') as HTMLInputElement).value).toBe('40')
    fireEvent.click(within(form).getByRole('button', { name: 'Save Assessment' }))

    expect(await screen.findByText('dev-user')).toBeTruthy()
    expect(screen.getByText('Assessed Count').parentElement?.textContent).toContain('1')
    const assessmentCall = fetchMock.mock.calls.find(([input]) => String(input).endsWith('/assessment'))
    expect(JSON.parse(String(assessmentCall?.[1]?.body))).toEqual({ responsibility: 'ByUser', finalDeduction: 40, reason: null })
  })

  it('forces zero deduction when assessing By Company', async () => {
    const fetchMock = mockReview(); renderPage()
    const form = await openDrawer()

    fireEvent.mouseDown(within(form).getByRole('combobox', { name: 'Responsibility' }))
    fireEvent.click(screen.getByRole('option', { name: 'By Company' }))
    expect((within(form).getByLabelText('Final Deduction') as HTMLInputElement).value).toBe('0')
    fireEvent.click(within(form).getByRole('button', { name: 'Save Assessment' }))

    await waitFor(() => {
      const call = fetchMock.mock.calls.find(([input]) => String(input).endsWith('/assessment'))
      expect(JSON.parse(String(call?.[1]?.body))).toEqual({ responsibility: 'ByCompany', finalDeduction: 0, reason: null })
    })
  })

  it('requires an override reason before sending a reduced By User deduction', async () => {
    const fetchMock = mockReview(); renderPage()
    const form = await openDrawer()
    fireEvent.mouseDown(within(form).getByRole('combobox', { name: 'Responsibility' }))
    fireEvent.click(screen.getByRole('option', { name: 'By User' }))
    fireEvent.change(within(form).getByLabelText('Final Deduction'), { target: { value: '10' } })
    fireEvent.click(within(form).getByRole('button', { name: 'Save Assessment' }))

    expect(await within(form).findByText('Enter an override reason when the deduction differs from Calculated Excess.')).toBeTruthy()
    expect(fetchMock.mock.calls.some(([input]) => String(input).endsWith('/assessment'))).toBe(false)
  })

  it('shows authorization errors returned by the assessment API', async () => {
    mockReview({ assessmentError: { status: 403, detail: 'The current user is not authorized to resolve billing exceptions.' } }); renderPage()
    const form = await openDrawer()
    fireEvent.mouseDown(within(form).getByRole('combobox', { name: 'Responsibility' }))
    fireEvent.click(screen.getByRole('option', { name: 'By User' }))
    fireEvent.click(within(form).getByRole('button', { name: 'Save Assessment' }))

    expect(await within(form).findByText('The current user is not authorized to resolve billing exceptions.')).toBeTruthy()
  })

  it('keeps locked batch details readable and assessment read-only', async () => {
    mockReview({ status: 'Locked' }); renderPage()
    fireEvent.click(await screen.findByText('Employee One'))

    expect(await screen.findByText('PDF Charge Breakdown')).toBeTruthy()
    expect(screen.queryByRole('form', { name: 'Monthly bill assessment' })).toBeNull()
    expect(screen.getByText('Assessment is read-only because this billing batch is locked.')).toBeTruthy()
  })

  it('bulk-assigns responsibility to selected rows via checkboxes', async () => {
    const fetchMock = mockReview(); renderPage()
    await screen.findByText('Employee One')

    fireEvent.click(screen.getByRole('checkbox', { name: 'Select 768791861' }))
    expect(await screen.findByText('1 selected')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Assign Responsibility' }))

    const dialog = await screen.findByRole('dialog', { name: 'Assign Responsibility to 1 bill' })
    fireEvent.mouseDown(within(dialog).getByRole('combobox', { name: 'Responsibility' }))
    fireEvent.click(screen.getByRole('option', { name: 'By Company' }))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Apply' }))

    expect(await within(dialog).findByText('1 succeeded, 0 failed.')).toBeTruthy()
    const bulkCall = fetchMock.mock.calls.find(([input]) => String(input).endsWith('/bulk-assessment'))
    expect(JSON.parse(String(bulkCall?.[1]?.body))).toEqual({ monthlyBillIds: ['bill-1'], responsibility: 'ByCompany', reason: null })

    fireEvent.click(within(dialog).getByRole('button', { name: 'Close' }))
    await waitFor(() => expect(screen.queryByText('1 selected')).toBeNull())
  })

  it('selects every row on the page via the header checkbox', async () => {
    mockReview(); renderPage()
    await screen.findByText('Employee One')

    fireEvent.click(screen.getByRole('checkbox', { name: 'Select all rows on this page' }))

    expect(await screen.findByText('1 selected')).toBeTruthy()
  })

  it('shows a partial failure summary when some selected bills cannot be assessed', async () => {
    mockReview({ bulkResult: { successCount: 0, failureCount: 1, items: [{ monthlyBillId: 'bill-1', success: false, error: 'Locked bill batches cannot be assessed or reassessed.' }] } })
    renderPage()
    await screen.findByText('Employee One')

    fireEvent.click(screen.getByRole('checkbox', { name: 'Select 768791861' }))
    fireEvent.click(screen.getByRole('button', { name: 'Assign Responsibility' }))
    const dialog = await screen.findByRole('dialog', { name: 'Assign Responsibility to 1 bill' })
    fireEvent.mouseDown(within(dialog).getByRole('combobox', { name: 'Responsibility' }))
    fireEvent.click(screen.getByRole('option', { name: 'By User' }))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Apply' }))

    expect(await within(dialog).findByText('0 succeeded, 1 failed.')).toBeTruthy()
    expect(within(dialog).getByText(/768791861: Locked bill batches cannot be assessed or reassessed\./)).toBeTruthy()
  })

  it('disables bulk selection for a locked batch', async () => {
    mockReview({ status: 'Locked' }); renderPage()
    await screen.findByText('Employee One')

    expect(screen.getByRole('checkbox', { name: 'Select all rows on this page' })).toHaveProperty('disabled', true)
    expect(screen.getByRole('checkbox', { name: 'Select 768791861' })).toHaveProperty('disabled', true)
  })
})
