import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

import { MonthlyBillReportPage } from './MonthlyBillReportPage'

const batchItems = [
  { id: 'completed', providerId: 'p1', provider: 'Dialog', corporateCode: 'CORP', billingYear: 2026, billingMonth: 8, calculatedGrandTotal: 100, validationLevel: 'StructuralOnly', status: 'Completed', originalFileName: 'aug.pdf', uploadedBy: 'dev-user', uploadedAt: '2026-09-01T00:00:00Z' },
  { id: 'locked', providerId: 'p1', provider: 'Dialog', corporateCode: 'CORP', billingYear: 2026, billingMonth: 7, calculatedGrandTotal: 90, validationLevel: 'StructuralOnly', status: 'Locked', originalFileName: 'jul.pdf', uploadedBy: 'dev-user', uploadedAt: '2026-08-01T00:00:00Z' },
  { id: 'review', providerId: 'p1', provider: 'Dialog', corporateCode: 'CORP', billingYear: 2026, billingMonth: 6, calculatedGrandTotal: 80, validationLevel: 'StructuralOnly', status: 'FinanceApproval', originalFileName: 'jun.pdf', uploadedBy: 'dev-user', uploadedAt: '2026-07-01T00:00:00Z' },
]

const totals: Record<string, { totalActualBill: number; totalFinalDeduction: number }> = {
  completed: { totalActualBill: 100, totalFinalDeduction: 12.5 },
  locked: { totalActualBill: 90, totalFinalDeduction: 9 },
  review: { totalActualBill: 80, totalFinalDeduction: 0 },
}

function json(body: unknown, status = 200, headers: Record<string, string> = {}) { return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json', ...headers } }) }

describe('MonthlyBillReportPage', () => {
  beforeEach(() => {
    Object.defineProperty(URL, 'createObjectURL', { configurable: true, value: vi.fn(() => 'blob:report') })
    Object.defineProperty(URL, 'revokeObjectURL', { configurable: true, value: vi.fn() })
  })
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('renders server totals and enables downloads only for Completed and Locked batches', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/api/bill-batches?')) return json({ items: batchItems, pageNumber: 1, pageSize: 20, totalCount: 3, totalPages: 1 })
      const id = Object.keys(totals).find(value => url.endsWith(`/api/bill-batches/${value}/review`))!
      return json({ batchId: id, billingYear: 2026, billingMonth: 8, provider: 'Dialog', corporateCode: 'CORP', batchStatus: batchItems.find(item => item.id === id)!.status, validationLevel: 'StructuralOnly', totalAccounts: 1, ...totals[id], totalCalculatedExcess: 0, companyResponsibilityAmount: 0, exceptionCount: 0, unmatchedCount: 0, assessedCount: 1, unassessedCount: 0, approvalHistory: [] })
    })
    render(<MemoryRouter><MonthlyBillReportPage /></MemoryRouter>)

    expect(await screen.findByText('LKR 100.00')).toBeTruthy()
    expect(screen.getByText('LKR 12.50')).toBeTruthy()
    const buttons = screen.getAllByRole('button', { name: 'Download Excel' })
    expect(buttons).toHaveLength(3)
    expect((buttons[0] as HTMLButtonElement).disabled).toBe(false)
    expect((buttons[1] as HTMLButtonElement).disabled).toBe(false)
    expect((buttons[2] as HTMLButtonElement).disabled).toBe(true)
  })

  it('uses the server filename for an eligible report download', async () => {
    let downloadedName = ''
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) { downloadedName = this.download })
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/api/bill-batches?')) return json({ items: [batchItems[0]], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 })
      if (url.endsWith('/excel')) return new Response('xlsx', { headers: { 'Content-Disposition': 'attachment; filename="August-2026-Mobile-Bill.xlsx"' } })
      return json({ batchId: 'completed', billingYear: 2026, billingMonth: 8, provider: 'Dialog', corporateCode: 'CORP', batchStatus: 'Completed', validationLevel: 'StructuralOnly', totalAccounts: 1, totalActualBill: 100, totalCalculatedExcess: 0, totalFinalDeduction: 12.5, companyResponsibilityAmount: 0, exceptionCount: 0, unmatchedCount: 0, assessedCount: 1, unassessedCount: 0, approvalHistory: [] })
    })
    render(<MemoryRouter><MonthlyBillReportPage /></MemoryRouter>)
    fireEvent.click(await screen.findByRole('button', { name: 'Download Excel' }))

    await waitFor(() => expect(downloadedName).toBe('August-2026-Mobile-Bill.xlsx'))
  })

  it('downloads the PDF report with the server filename', async () => {
    let downloadedName = ''
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) { downloadedName = this.download })
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/api/bill-batches?')) return json({ items: [batchItems[0]], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 })
      if (url.endsWith('/pdf')) return new Response('%PDF', { headers: { 'Content-Disposition': 'attachment; filename="Mobile_Bill_Report_2026_08.pdf"' } })
      return json({ batchId: 'completed', billingYear: 2026, billingMonth: 8, provider: 'Dialog', corporateCode: 'CORP', batchStatus: 'Completed', validationLevel: 'StructuralOnly', totalAccounts: 1, totalActualBill: 100, totalCalculatedExcess: 0, totalFinalDeduction: 12.5, companyResponsibilityAmount: 0, exceptionCount: 0, unmatchedCount: 0, assessedCount: 1, unassessedCount: 0, approvalHistory: [] })
    })
    render(<MemoryRouter><MonthlyBillReportPage /></MemoryRouter>)
    fireEvent.click(await screen.findByRole('button', { name: 'Download PDF' }))

    await waitFor(() => expect(downloadedName).toBe('Mobile_Bill_Report_2026_08.pdf'))
  })

  it('downloads reports filtered by factory and category, and the full report by default', async () => {
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/api/bill-batches?')) return json({ items: [batchItems[0]], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 })
      if (url.includes('/api/factories')) return json({ items: [{ id: 'f1', code: 'CAL', name: 'Concord Apparel', isActive: true }, { id: 'f2', code: 'CFL', name: 'Concord Footware', isActive: true }], pageNumber: 1, pageSize: 100, totalCount: 2, totalPages: 1 })
      if (url.includes('/api/categories')) return json({ items: [{ id: 'c1', code: 'MGR', name: 'Manager & Executive', isActive: true }, { id: 'c2', code: 'OTH', name: 'Other Category', isActive: true }], pageNumber: 1, pageSize: 100, totalCount: 2, totalPages: 1 })
      if (url.includes('/excel') || url.includes('/pdf')) return new Response('file', { headers: { 'Content-Disposition': 'attachment; filename="report"' } })
      return json({ batchId: 'completed', billingYear: 2026, billingMonth: 8, provider: 'Dialog', corporateCode: 'CORP', batchStatus: 'Completed', validationLevel: 'StructuralOnly', totalAccounts: 1, totalActualBill: 100, totalCalculatedExcess: 0, totalFinalDeduction: 12.5, companyResponsibilityAmount: 0, exceptionCount: 0, unmatchedCount: 0, assessedCount: 1, unassessedCount: 0, approvalHistory: [] })
    })
    render(<MemoryRouter><MonthlyBillReportPage /></MemoryRouter>)
    const reportCalls = () => fetchMock.mock.calls.map(([input]) => String(input)).filter(url => url.includes('/api/reports/'))

    fireEvent.click(await screen.findByRole('button', { name: 'Download Excel' }))
    await waitFor(() => expect(reportCalls()).toHaveLength(1))
    expect(reportCalls()[0]).toMatch(/\/excel$/)

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Report factory' }))
    fireEvent.click(await screen.findByRole('option', { name: 'Concord Apparel' }))
    fireEvent.click(screen.getByRole('option', { name: 'Concord Footware' }))
    expect(screen.getByRole('option', { name: 'Concord Footware' }).querySelector('input')?.checked).toBe(true)
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'Escape' })
    fireEvent.click(screen.getByRole('button', { name: 'Download PDF' }))
    await waitFor(() => expect(reportCalls()).toHaveLength(2))
    expect(reportCalls()[1]).toMatch(/\/pdf\?factoryCode=CAL&factoryCode=CFL$/)

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Report category' }))
    fireEvent.click(await screen.findByRole('option', { name: 'Manager & Executive' }))
    fireEvent.click(screen.getByRole('option', { name: 'Other Category' }))
    expect(screen.getByRole('option', { name: 'Other Category' }).querySelector('input')?.checked).toBe(true)
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'Escape' })
    fireEvent.click(screen.getByRole('button', { name: 'Download Excel' }))
    await waitFor(() => expect(reportCalls()).toHaveLength(3))
    expect(reportCalls()[2]).toMatch(/\/excel\?factoryCode=CAL&factoryCode=CFL&categoryCode=MGR&categoryCode=OTH$/)
  })

  it('displays an export reconciliation ProblemDetails message', async () => {
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined)
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/api/bill-batches?')) return json({ items: [batchItems[0]], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 })
      if (url.endsWith('/excel')) return json({ status: 409, title: 'Report unavailable', detail: 'The report totals do not reconcile.' }, 409)
      return json({ batchId: 'completed', billingYear: 2026, billingMonth: 8, provider: 'Dialog', corporateCode: 'CORP', batchStatus: 'Completed', validationLevel: 'StructuralOnly', totalAccounts: 1, totalActualBill: 100, totalCalculatedExcess: 0, totalFinalDeduction: 12.5, companyResponsibilityAmount: 0, exceptionCount: 0, unmatchedCount: 0, assessedCount: 1, unassessedCount: 0, approvalHistory: [] })
    })
    render(<MemoryRouter><MonthlyBillReportPage /></MemoryRouter>)
    fireEvent.click(await screen.findByRole('button', { name: 'Download Excel' }))

    expect(await screen.findByText('The report totals do not reconcile.')).toBeTruthy()
  })

  it('shows an empty state when no billing batches exist', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }))
    render(<MemoryRouter><MonthlyBillReportPage /></MemoryRouter>)
    expect(await screen.findByText('No billing batches are available for reporting.')).toBeTruthy()
  })
})
