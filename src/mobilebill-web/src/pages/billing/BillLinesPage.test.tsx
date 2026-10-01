import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { BillLinesPage } from './BillLinesPage'

const response = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })

describe('BillLinesPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('displays persisted PDF values and searches mobile numbers server-side', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async input => String(input).includes('/lines?')
      ? response({ items: [{ id: 'line-1', mobileNumber: '768791861', previousDueAmount: 0, payments: 0, totalUsageCharges: 0, idd: 0, roaming: 0, valueAddedServices: 0, discounts: 0, billAdjustmentsBalanceTransfers: 0, commitmentCharges: 0, latePaymentCharges: 0, addToBill: 0, instalmentPlans: 0, governmentTaxesAndLevies: 0, vat: 0, chargesForBillPeriod: 0, totalDueAmount: 0, extractionStatus: 'Successful', pageNumber: 12, extractionError: null, rawText: '768791861 ...' }], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 })
      : response({ billingYear: 2026, billingMonth: 8, status: 'Validated' }))
    render(<MemoryRouter initialEntries={['/billing/batch-1/lines']}><Routes><Route path="/billing/:batchId/lines" element={<BillLinesPage />} /></Routes></MemoryRouter>)

    expect(await screen.findByText('768791861')).toBeTruthy()
    expect(screen.getAllByText('0.00', { exact: false }).length).toBeGreaterThan(0)
    fireEvent.change(screen.getByLabelText('Search mobile'), { target: { value: '  768791861  ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Search' }))

    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).includes('search=768791861'))).toBe(true))
    expect(screen.getByRole('link', { name: 'Exceptions' }).getAttribute('href')).toBe('/billing/batch-1/exceptions')
    expect(screen.getByRole('link', { name: 'Review' }).getAttribute('href')).toBe('/billing/batch-1/review')
  })

  it('downloads the extracted lines as Excel, using the applied search', async () => {
    let downloadedName = ''
    vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:lines'); vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined)
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) { downloadedName = this.download })
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/lines/excel')) return new Response('xlsx', { headers: { 'Content-Disposition': 'attachment; filename="Extracted_Bill_Lines_2026_08.xlsx"' } })
      if (url.includes('/lines?')) return response({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 })
      return response({ billingYear: 2026, billingMonth: 8, status: 'Validated' })
    })
    render(<MemoryRouter initialEntries={['/billing/batch-1/lines']}><Routes><Route path="/billing/:batchId/lines" element={<BillLinesPage />} /></Routes></MemoryRouter>)

    fireEvent.click(await screen.findByRole('button', { name: 'Download Excel' }))
    await waitFor(() => expect(downloadedName).toBe('Extracted_Bill_Lines_2026_08.xlsx'))
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/api/bill-batches/batch-1/lines/excel'))).toBe(true)

    fireEvent.change(screen.getByLabelText('Search mobile'), { target: { value: '7687' } })
    fireEvent.click(screen.getByRole('button', { name: 'Search' }))
    fireEvent.click(screen.getByRole('button', { name: 'Download Excel' }))
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/lines/excel?search=7687'))).toBe(true))
  })

  it('loads every page of lines into one scrolling table with no page buttons', async () => {
    const line = (id: string, mobile: string) => ({ id, mobileNumber: mobile, pageNumber: 1, extractionStatus: 'Extracted', extractionError: null, previousDueAmount: 0, payments: 0, totalUsageCharges: 0, idd: 0, roaming: 0, valueAddedServices: 0, discounts: 0, billAdjustmentsBalanceTransfers: 0, commitmentCharges: 0, latePaymentCharges: 0, addToBill: 0, instalmentPlans: 0, governmentTaxesAndLevies: 0, vat: 0, chargesForBillPeriod: 0, totalDueAmount: 0, rawText: '' })
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/lines?')) return response(url.includes('pageNumber=2')
        ? { items: [line('l2', '768791862')], pageNumber: 2, pageSize: 100, totalCount: 101, totalPages: 2 }
        : { items: [line('l1', '768791861')], pageNumber: 1, pageSize: 100, totalCount: 101, totalPages: 2 })
      return response({ billingYear: 2026, billingMonth: 8, status: 'Validated' })
    })
    render(<MemoryRouter initialEntries={['/billing/batch-1/lines']}><Routes><Route path="/billing/:batchId/lines" element={<BillLinesPage />} /></Routes></MemoryRouter>)

    expect(await screen.findByText('768791862')).toBeTruthy()
    expect(screen.getByText('768791861')).toBeTruthy()
    expect(fetchMock.mock.calls.every(([url]) => !String(url).includes('/lines?') || String(url).includes('pageSize=100'))).toBe(true)
    expect(screen.queryByRole('button', { name: 'Go to page 2' })).toBeNull()
    const scrollArea = screen.getByLabelText('Extracted bill lines scroll area')
    expect(getComputedStyle(scrollArea).overflow).toBe('auto')
    expect(scrollArea.contains(screen.getByRole('table', { name: 'Extracted bill lines' }))).toBe(true)
  })
})
