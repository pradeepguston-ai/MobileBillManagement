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
})
