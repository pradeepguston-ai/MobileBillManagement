import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { MonthlyBillReportViewPage } from './MonthlyBillReportViewPage'

const summary = { batchId: 'batch-1', billingYear: 2026, billingMonth: 8, provider: 'Dialog', corporateCode: 'CORP', batchStatus: 'Completed', validationLevel: 'StructuralOnly', totalAccounts: 1, totalActualBill: 150, totalCalculatedExcess: 40, totalFinalDeduction: 40, companyResponsibilityAmount: 0, exceptionCount: 0, unmatchedCount: 0, assessedCount: 1, unassessedCount: 0, unresolvedExceptionCount: 0, approvalHistory: [] }
const row = { id: 'bill-1', mobileNumber: '740052872', employeeEpf: '211876', employeeName: 'Employee One', nameWithInitials: 'B.A.C.S. BALASOORIYA', callingName: 'CHAMATH', category: 'EXECUTIVE', designation: 'FINANCE EXECUTIVE', factory: 'CONCORD APPAREL (PVT) LTD', department: 'ACCOUNTS', creditLimit: 100, monthlyRental: 10, availableEntitlement: 110, actualBill: 150, variance: -40, calculatedExcess: 40, responsibility: 'ByUser', finalDeduction: 40, remark: null, status: 'Assessed', hasException: false, isAssessed: true }
const charges = { previousDue: 0, payments: 0, totalUsage: 0, idd: 0, roaming: 0, vas: 0, discounts: 0, billAdjustments: 0, commitmentCharges: 0, latePaymentCharges: 0, addToBill: 0, instalmentPlans: 0, governmentTaxesLevies: 0, vat: 0, chargesForBillPeriod: 150, totalDueAmount: 150 }
const detail = { row, entitlementEffectiveFrom: '2026-08-01', entitlementEffectiveTo: null, charges, exceptions: [], approvalHistory: [], auditHistory: [], assessedBy: 'dev-user', assessedAt: '2026-09-09T05:00:00Z', deductionOverrideReason: null }
const page = (items: unknown[]) => ({ items, pageNumber: 1, pageSize: 100, totalCount: items.length, totalPages: items.length ? 1 : 0 })
const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })

function mockReport(status = 'Completed') {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
    const url = String(input)
    if (url.includes('/api/categories')) return json(page([{ id: 'c1', code: 'CAT001', name: 'EXECUTIVE' }, { id: 'c2', code: 'CAT002', name: 'GENERAL EMPLOYEE' }]))
    if (url.includes('/api/factories') || url.includes('/api/departments') || url.includes('/api/sections')) return json(page([]))
    if (url.includes('/trend')) return json({ monthlyBillId: 'bill-1', mobileNumber: row.mobileNumber, employeeEpf: row.employeeEpf, employeeName: row.employeeName, scope: 'ThisNumber', months: 12, points: [], currentActualBill: 150, average: null, changePercent: null, isAboveUsual: false, monthsOverLimit: 0, highestYear: null, highestMonth: null, highestActualBill: null, aboveUsualThresholdPercent: 30 })
    if (url.includes('/rows/bill-1')) return json(detail)
    if (url.includes('/rows?')) return json(page([row]))
    if (url.includes('/deduction-excel')) return new Response(new Blob(['xlsx']), { status: 200, headers: { 'Content-Disposition': 'attachment; filename="Mobile_Bill_Deduction_2026_08.xlsx"' } })
    return json({ ...summary, batchStatus: status })
  })
}

const renderPage = () => render(<MemoryRouter initialEntries={['/reports/monthly-bill/batch-1']}><Routes><Route path="/reports/monthly-bill/:batchId" element={<MonthlyBillReportViewPage />} /></Routes></MemoryRouter>)

describe('MonthlyBillReportViewPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('shows only the filters and the bill rows, without the review workflow', async () => {
    mockReport(); renderPage()

    expect(await screen.findByText('B.A.C.S. BALASOORIYA')).toBeTruthy()
    expect(await screen.findByRole('heading', { name: 'August 2026 · Dialog · CORP' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Monthly Bill Report' }).getAttribute('href')).toBe('/reports/monthly-bill')
    for (const label of ['Factory', 'Department', 'Section', 'Category', 'Responsibility']) expect(screen.getByRole('combobox', { name: label })).toBeTruthy()
    // The report has no Exception filter (the review screen keeps it).
    expect(screen.queryByRole('combobox', { name: 'Exception' })).toBeNull()
    expect(screen.getByLabelText('Min Deduction')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Numbers with Deduction' })).toBeTruthy()
    // None of the review screen's workflow, KPIs, approvals or bulk assignment.
    for (const text of ['Monthly Bill Review', 'Total Accounts', 'Approval History', 'Submit for IT Review']) expect(screen.queryByText(text)).toBeNull()
    expect(screen.getByRole('checkbox', { name: 'Select all rows' })).toHaveProperty('disabled', true)
  })

  it('filters by several ticked categories and opens a row read-only', async () => {
    const fetchMock = mockReport(); renderPage()
    await screen.findByText('B.A.C.S. BALASOORIYA')
    const listed = (code: string) => fetchMock.mock.calls.some(([url]) => String(url).includes('/rows?') && new URL(String(url)).searchParams.get('categoryCode') === code)

    const category = screen.getByRole('combobox', { name: 'Category' })
    expect(category.textContent).toBe('All categories')
    fireEvent.mouseDown(category)
    fireEvent.click(await screen.findByRole('option', { name: 'EXECUTIVE' }))
    fireEvent.click(screen.getByRole('option', { name: 'GENERAL EMPLOYEE' }))
    await waitFor(() => expect(listed('CAT001,CAT002')).toBe(true))
    expect(screen.getByRole('option', { name: 'EXECUTIVE' }).querySelector('input')).toHaveProperty('checked', true)
    expect(screen.getByRole('option', { name: 'All categories' }).querySelector('input')).toHaveProperty('checked', false)
    expect(category.textContent).toBe('EXECUTIVE, GENERAL EMPLOYEE')

    // Ticking All categories clears the others.
    fireEvent.click(screen.getByRole('option', { name: 'All categories' }))
    await waitFor(() => expect(category.textContent).toBe('All categories'))
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'Escape' })

    fireEvent.click(await screen.findByText('B.A.C.S. BALASOORIYA'))
    expect(await screen.findByText('Your role can view assessments but not change them.')).toBeTruthy()
    expect(screen.queryByRole('form', { name: 'Monthly bill assessment' })).toBeNull()
  })

  it('downloads the deduction Excel with the filters shown on the screen', async () => {
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:deduction') })
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() })
    let downloadedName = ''
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) { downloadedName = this.download })
    const fetchMock = mockReport(); renderPage()
    await screen.findByText('B.A.C.S. BALASOORIYA')

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Category' }))
    fireEvent.click(await screen.findByRole('option', { name: 'EXECUTIVE' }))
    fireEvent.click(screen.getByRole('option', { name: 'GENERAL EMPLOYEE' }))
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'Escape' })
    fireEvent.click(screen.getByRole('button', { name: 'Numbers with Deduction' }))
    const download = await screen.findByRole('button', { name: 'Download Deduction Excel' })
    await waitFor(() => expect(download).toHaveProperty('disabled', false))
    fireEvent.click(download)

    await waitFor(() => expect(downloadedName).toBe('Mobile_Bill_Deduction_2026_08.xlsx'))
    const url = new URL(String(fetchMock.mock.calls.find(([input]) => String(input).includes('/deduction-excel'))![0]))
    expect(url.pathname).toBe('/api/reports/billing/batch-1/deduction-excel')
    expect(url.searchParams.get('categoryCode')).toBe('CAT001,CAT002')
    expect(url.searchParams.get('calculatedExcessMin')).toBe('0.01')
    expect(url.searchParams.has('sortBy')).toBe(false)
  })

  it('keeps the deduction Excel disabled until the batch is Completed or Locked', async () => {
    mockReport('FinanceApproval'); renderPage()
    await screen.findByText('B.A.C.S. BALASOORIYA')
    await screen.findByRole('heading', { name: 'August 2026 · Dialog · CORP' })
    expect(screen.getByRole('button', { name: 'Download Deduction Excel' })).toHaveProperty('disabled', true)
  })
})
