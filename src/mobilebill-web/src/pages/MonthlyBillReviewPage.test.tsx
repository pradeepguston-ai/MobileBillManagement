import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { MonthlyBillReviewPage } from './MonthlyBillReviewPage'

const summary = { batchId: 'batch-1', billingYear: 2026, billingMonth: 8, provider: 'Telecom', corporateCode: 'CORP', batchStatus: 'Validated', validationLevel: 'StructuralOnly', totalAccounts: 1, totalActualBill: 150, totalCalculatedExcess: 40, totalFinalDeduction: 0, companyResponsibilityAmount: 0, exceptionCount: 1, unmatchedCount: 0, assessedCount: 0, unassessedCount: 1 }
const row = { id: 'bill-1', mobileNumber: '768791861', employeeEpf: 'EPF-1', employeeName: 'Employee One', callingName: 'Sam', category: 'Executive', designation: 'Manager', factory: 'Main Factory', department: 'IT', creditLimit: 100, monthlyRental: 10, availableEntitlement: 110, actualBill: 150, variance: -40, calculatedExcess: 40, responsibility: null, finalDeduction: 0, remark: null, status: 'Pending', hasException: true, isAssessed: false }
const charges = { previousDue: 1, payments: -2, totalUsage: 3, idd: 4, roaming: 5, vas: 6, discounts: -7, billAdjustments: 8, commitmentCharges: 9, latePaymentCharges: 10, addToBill: 11, instalmentPlans: 12, governmentTaxesLevies: 13, vat: 14, chargesForBillPeriod: 149, totalDueAmount: 150 }
const detail = { row, entitlementEffectiveFrom: '2026-08-01', entitlementEffectiveTo: null, charges, exceptions: ['ZERO_BILL'], approvalHistory: [], auditHistory: ['Matched by dev-user'], assessedBy: null, assessedAt: null, deductionOverrideReason: null }
const trend = { monthlyBillId: 'bill-1', mobileNumber: '768791861', employeeEpf: 'EPF-1', employeeName: 'Employee One', scope: 'ThisNumber', months: 12, points: [{ billingYear: 2026, billingMonth: 8, actualBill: 150, entitlement: 110, calculatedExcess: 40, finalDeduction: 0, vas: 6, responsibility: null, isOverLimit: true, isPreliminary: true, isCurrent: true, numbers: 1, holderEpf: 'EPF-1', holderName: 'Employee One', isOtherHolder: false }], currentActualBill: 150, average: null, changePercent: null, isAboveUsual: false, monthsOverLimit: 1, highestYear: 2026, highestMonth: 8, highestActualBill: 150, aboveUsualThresholdPercent: 30 }
const paged = (items: Array<Record<string, unknown>> = [row], totalPages = 1) => ({ items, pageNumber: 1, pageSize: 20, totalCount: items.length, totalPages })
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' } })

function renderPage() {
  return render(<MemoryRouter initialEntries={['/billing/batch-1/review']}><Routes><Route path="/billing/:batchId/review" element={<MonthlyBillReviewPage />} /></Routes></MemoryRouter>)
}

function mockReview(options: { rows?: Array<Record<string, unknown>>; status?: string; detailAfterSave?: { row: Record<string, unknown>; [key: string]: unknown }; assessmentError?: { status: number; detail: string }; totalPages?: number; unresolvedExceptionCount?: number; pageTwoGate?: Promise<void>; bulkResult?: { successCount: number; failureCount: number; items: Array<{ monthlyBillId: string; success: boolean; error: string | null }> } } = {}) {
  let saved = false
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
    const url = String(input)
    if (url.includes('/api/factories') || url.includes('/api/departments') || url.includes('/api/categories')) return json({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })
    if (url.endsWith('/excel')) return new Response(new Blob(['xlsx']), { status: 200, headers: { 'Content-Disposition': 'attachment; filename="report.xlsx"' } })
    if (url.endsWith('/pdf')) return new Response(new Blob(['%PDF']), { status: 200, headers: { 'Content-Disposition': 'attachment; filename="report.pdf"' } })
    if (url.endsWith('/bulk-assessment')) return json(options.bulkResult ?? { successCount: 1, failureCount: 0, items: [{ monthlyBillId: 'bill-1', success: true, error: null }] })
    if (url.endsWith('/assessment')) {
      if (options.assessmentError) return json({ title: 'Assessment failed', detail: options.assessmentError.detail }, options.assessmentError.status)
      saved = true
      return json({ monthlyBillId: 'bill-1', responsibility: 'ByUser', actualBill: 150, availableEntitlement: 110, variance: -40, calculatedExcess: 40, finalDeduction: 40, assessedAt: '2026-09-09T05:00:00Z', assessedBy: 'dev-user', deductionOverrideAmount: null, deductionOverrideReason: null, deductionOverrideBy: null, deductionOverrideAt: null })
    }
    if (url.includes('/rows/bill-1/trend')) return json(trend)
    if (url.includes('/rows/bill-1')) return json(saved && options.detailAfterSave ? options.detailAfterSave : detail)
    if (url.includes('/rows?')) {
      if (options.pageTwoGate && url.includes('pageNumber=2')) await options.pageTwoGate
      return json(paged(saved && options.detailAfterSave ? [options.detailAfterSave.row] : options.rows ?? [row], options.totalPages ?? 1))
    }
    return json({ ...summary, unresolvedExceptionCount: options.unresolvedExceptionCount ?? 0, batchStatus: options.status ?? 'Validated', assessedCount: saved ? 1 : 0, unassessedCount: saved ? 0 : 1, totalFinalDeduction: saved ? 40 : 0 })
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
    for (const label of ['Total Accounts', 'Total Actual Bill', 'Total Calculated Excess', 'Total Final Deduction', 'Company Responsibility Amount', 'Exception Count', 'Unmatched Count', 'Assigned Count', 'Unassigned Count']) expect(screen.getByText(label)).toBeTruthy()
    expect(screen.getByText('Billing Period: August 2026', { exact: false })).toBeTruthy()
    expect(screen.getByText('Sam')).toBeTruthy()
    expect(screen.getAllByText('Unassessed').length).toBeGreaterThan(0)
  })

  it('sends filters and sorting to the server and fetches rows 100 at a time', async () => {
    const fetchMock = mockReview(); renderPage()
    await screen.findByText('Employee One')

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Responsibility' }))
    fireEvent.click(screen.getByRole('option', { name: 'By User' }))
    fireEvent.change(screen.getByLabelText('Search mobile / EPF / employee / calling name'), { target: { value: 'Sam' } })
    fireEvent.click(screen.getByText('EPF'))

    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) => {
      const url = String(input)
      return url.includes('/rows?') && url.includes('responsibility=ByUser') && url.includes('search=Sam') && url.includes('sortBy=epf') && url.includes('pageNumber=1') && url.includes('pageSize=100')
    })).toBe(true))
  })

  it('loads every page of rows into one scrolling table with no page buttons', async () => {
    const second = { ...row, id: 'bill-2', mobileNumber: '768791862', employeeName: 'Employee Two' }
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/api/factories') || url.includes('/api/departments') || url.includes('/api/categories')) return json({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })
      if (url.includes('/rows?')) return json(url.includes('pageNumber=2')
        ? { items: [second], pageNumber: 2, pageSize: 100, totalCount: 101, totalPages: 2 }
        : { items: [row], pageNumber: 1, pageSize: 100, totalCount: 101, totalPages: 2 })
      return json({ ...summary, unresolvedExceptionCount: 0 })
    })
    renderPage()

    expect(await screen.findByText('Employee Two')).toBeTruthy()
    expect(screen.getByText('Employee One')).toBeTruthy()
    expect(fetchMock.mock.calls.filter(([input]) => String(input).includes('/rows?')).length).toBeGreaterThanOrEqual(2)
    expect(screen.queryByRole('navigation', { name: /pagination/i })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Go to page 2' })).toBeNull()
    const scrollArea = screen.getByLabelText('Monthly bill rows scroll area')
    expect(getComputedStyle(scrollArea).overflow).toBe('auto')
    expect(scrollArea.contains(screen.getByRole('table', { name: 'Monthly bill review rows' }))).toBe(true)
  })

  it('does not show the unused per-bill Status column or filter', async () => {
    mockReview(); renderPage()
    await screen.findByText('Employee One')

    expect(screen.queryByRole('columnheader', { name: 'Status' })).toBeNull()
    expect(screen.queryByRole('combobox', { name: 'Status' })).toBeNull()
    expect(screen.queryByText('Pending')).toBeNull()
  })

  it('shows the amount the company absorbs in Final Deduction for By Company rows', async () => {
    const companyRow = { ...row, responsibility: 'ByCompany', finalDeduction: 0, isAssessed: true }
    const userRow = { ...row, id: 'bill-2', mobileNumber: '768791862', employeeName: 'Employee Two', responsibility: 'ByUser', finalDeduction: 25, isAssessed: true }
    mockReview({ rows: [companyRow, userRow] }); renderPage()

    const companyLine = (await screen.findByText('Employee One')).closest('tr')!
    const userLine = screen.getByText('Employee Two').closest('tr')!

    expect(within(companyLine).getByText(/\(Company\)/)).toBeTruthy()
    expect(companyLine.textContent).toContain('40.00 (Company)')
    expect(userLine.textContent).not.toContain('(Company)')
    expect(userLine.textContent).toContain('25.00')
  })

  it('highlights rows that have a Calculated Excess', async () => {
    const noExcess = { ...row, id: 'bill-3', mobileNumber: '768791863', employeeName: 'Employee Three', calculatedExcess: 0, hasException: false }
    mockReview({ rows: [row, noExcess] }); renderPage()

    const withExcess = (await screen.findByText('Employee One')).closest('tr')!
    const without = screen.getByText('Employee Three').closest('tr')!

    expect(getComputedStyle(withExcess).backgroundColor).toBe('rgba(255, 201, 40, 0.22)')
    expect(getComputedStyle(without).backgroundColor).not.toBe('rgba(255, 201, 40, 0.22)')
    const excessColumn = screen.getAllByRole('columnheader').findIndex(header => header.textContent?.includes('Calculated Excess'))
    expect(getComputedStyle(withExcess.cells[excessColumn]).fontWeight).toBe('700')
    expect(getComputedStyle(without.cells[excessColumn]).fontWeight).not.toBe('700')
  })

  it('filters rows by a Calculated Excess amount range', async () => {
    const fetchMock = mockReview(); renderPage()
    await screen.findByText('Employee One')

    fireEvent.change(screen.getByLabelText('Min Deduction'), { target: { value: '100' } })
    fireEvent.change(screen.getByLabelText('Max Deduction'), { target: { value: '500' } })

    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) => {
      const url = String(input)
      return url.includes('/rows?') && url.includes('calculatedExcessMin=100') && url.includes('calculatedExcessMax=500')
    })).toBe(true))
  })

  it('shows only numbers with an excess at the press of a button, and toggles back', async () => {
    const fetchMock = mockReview(); renderPage()
    await screen.findByText('Employee One')

    fireEvent.change(screen.getByLabelText('Max Deduction'), { target: { value: '500' } })
    fireEvent.click(screen.getByRole('button', { name: 'Numbers with Deduction' }))

    expect((screen.getByLabelText('Min Deduction') as HTMLInputElement).value).toBe('0.01')
    expect((screen.getByLabelText('Max Deduction') as HTMLInputElement).value).toBe('')
    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) => {
      const url = String(input)
      return url.includes('/rows?') && url.includes('calculatedExcessMin=0.01') && !url.includes('calculatedExcessMax=')
    })).toBe(true))

    fireEvent.click(screen.getByRole('button', { name: 'Showing Numbers with Deduction' }))
    expect((screen.getByLabelText('Min Deduction') as HTMLInputElement).value).toBe('')
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
    expect(screen.getByText('Assigned Count').parentElement?.textContent).toContain('1')
    const assessmentCall = fetchMock.mock.calls.find(([input]) => String(input).endsWith('/assessment'))
    expect(JSON.parse(String(assessmentCall?.[1]?.body))).toEqual({ responsibility: 'ByUser', finalDeduction: 40, reason: null })
  })

  it('shows the saved reason in the Calculation section straight after saving', async () => {
    const assessedDetail = { ...detail, row: { ...row, responsibility: 'ByUser', finalDeduction: 40, isAssessed: true, remark: 'Personal roaming in Dubai' }, assessedBy: 'dev-user', assessedAt: '2026-09-09T05:00:00Z' }
    const fetchMock = mockReview({ detailAfterSave: assessedDetail }); renderPage()
    const form = await openDrawer()
    const calculation = () => screen.getByText('Calculation').parentElement!
    expect(within(calculation()).getByText('Reason:').parentElement?.textContent).toBe('Reason: None')
    expect(within(screen.getByText('Assessment').parentElement!).queryByText('Remark:')).toBeNull()

    fireEvent.mouseDown(within(form).getByRole('combobox', { name: 'Responsibility' }))
    fireEvent.click(screen.getByRole('option', { name: 'By User' }))
    fireEvent.change(within(form).getByLabelText('Reason / Remark'), { target: { value: 'Personal roaming in Dubai' } })
    fireEvent.click(within(form).getByRole('button', { name: 'Save Assessment' }))

    await waitFor(() => expect(within(calculation()).getByText('Reason:').parentElement?.textContent).toBe('Reason: Personal roaming in Dubai'))
    const assessmentCall = fetchMock.mock.calls.find(([input]) => String(input).endsWith('/assessment'))
    expect(JSON.parse(String(assessmentCall?.[1]?.body)).reason).toBe('Personal roaming in Dubai')
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

  it('selects every row via the header checkbox', async () => {
    mockReview(); renderPage()
    await screen.findByText('Employee One')

    fireEvent.click(screen.getByRole('checkbox', { name: 'Select all rows' }))

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

  it('warns before downloading the Excel report when exceptions are unresolved', async () => {
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:report'); vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined)
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
    const fetchMock = mockReview({ status: 'Completed', unresolvedExceptionCount: 3 }); renderPage()
    await screen.findByText('Employee One')
    const excelCalls = () => fetchMock.mock.calls.filter(([input]) => String(input).endsWith('/excel')).length

    fireEvent.click(await screen.findByRole('button', { name: 'Download Excel Report' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/3 unresolved exceptions/)).toBeTruthy()
    expect(excelCalls()).toBe(0)

    fireEvent.click(within(dialog).getByRole('button', { name: 'Cancel' }))
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
    expect(excelCalls()).toBe(0)

    fireEvent.click(screen.getByRole('button', { name: 'Download Excel Report' }))
    fireEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Download anyway' }))
    await waitFor(() => expect(excelCalls()).toBe(1))
  })

  it('downloads the PDF report, warning first when exceptions are unresolved', async () => {
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:report'); vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined)
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
    const fetchMock = mockReview({ status: 'Completed', unresolvedExceptionCount: 2 }); renderPage()
    await screen.findByText('Employee One')
    const calls = (suffix: string) => fetchMock.mock.calls.filter(([input]) => String(input).endsWith(suffix)).length

    fireEvent.click(await screen.findByRole('button', { name: 'Download PDF Report' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText(/2 unresolved exceptions/)).toBeTruthy()
    expect(calls('/pdf')).toBe(0)

    fireEvent.click(within(dialog).getByRole('button', { name: 'Download anyway' }))
    await waitFor(() => expect(calls('/pdf')).toBe(1))
    expect(calls('/excel')).toBe(0)
  })

  it('downloads the PDF report straight away when no exceptions are unresolved', async () => {
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:report'); vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined)
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
    const fetchMock = mockReview({ status: 'Completed' }); renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Download PDF Report' }))

    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) => String(input).endsWith('/pdf'))).toBe(true))
    expect(screen.queryByRole('dialog')).toBeNull()
  })

  it('downloads the Excel report without a warning when no exceptions are unresolved', async () => {
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:report'); vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined)
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
    const fetchMock = mockReview({ status: 'Completed', unresolvedExceptionCount: 0 }); renderPage()
    await screen.findByText('Employee One')

    fireEvent.click(await screen.findByRole('button', { name: 'Download Excel Report' }))

    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) => String(input).endsWith('/excel'))).toBe(true))
    expect(screen.queryByText(/unresolved exception/)).toBeNull()
  })

  it('disables bulk selection for a locked batch', async () => {
    mockReview({ status: 'Locked' }); renderPage()
    await screen.findByText('Employee One')

    expect(screen.getByRole('checkbox', { name: 'Select all rows' })).toHaveProperty('disabled', true)
    expect(screen.getByRole('checkbox', { name: 'Select 768791861' })).toHaveProperty('disabled', true)
  })
})
