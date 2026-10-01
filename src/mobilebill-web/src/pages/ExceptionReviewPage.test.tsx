import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ExceptionReviewPage } from './ExceptionReviewPage'

const batch = { batchId: 'batch-1', billingYear: 2026, billingMonth: 8, provider: 'Telecom', corporateCode: 'CORP', batchStatus: 'Validated', validationLevel: 'StructuralOnly', totalAccounts: 1, totalActualBill: 100, totalCalculatedExcess: 10, totalFinalDeduction: 0, companyResponsibilityAmount: 0, exceptionCount: 1, unmatchedCount: 1, assessedCount: 0, unassessedCount: 1 }
const exception = { id: 'exception-1', billBatchId: 'batch-1', billLineId: 'line-1', mobileNumber: '768791861', exceptionType: 'MOBILE_NOT_FOUND', severity: 'Blocking', status: 'Open', description: 'No allocation matched.', resolution: null, resolvedBy: null, resolvedAt: null, allocationMatchMethod: null, entitlementMatchMethod: null }
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' } })

function mockBatchExceptions(status = 'Validated', candidates: unknown[] = []) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
    const url = String(input)
    if (url.endsWith('/review')) return json({ ...batch, batchStatus: status })
    if (url.endsWith('/exceptions/summary')) return json({ billBatchId: 'batch-1', totalCount: 3, unresolvedCount: 2, resolvedCount: 1 })
    if (url.includes('/mobile-account-candidates')) return json(candidates)
    return json({ items: [exception], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 })
  })
}

function renderBatchPage() {
  return render(<MemoryRouter initialEntries={['/billing/batch-1/exceptions']}><Routes><Route path="/billing/:batchId/exceptions" element={<ExceptionReviewPage />} /></Routes></MemoryRouter>)
}

describe('ExceptionReviewPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('loads exceptions by selecting a batch from the searchable dropdown', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/api/bill-batches?')) return json({ items: [{ id: 'batch-1', providerId: 'provider-1', provider: 'Telecom', corporateCode: 'CORP', billingYear: 2026, billingMonth: 8, calculatedGrandTotal: 100, validationLevel: 'StructuralOnly', status: 'Validated', originalFileName: null, uploadedBy: null, uploadedAt: null }], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 })
      if (url.endsWith('/review')) return json(batch)
      if (url.endsWith('/exceptions/summary')) return json({ billBatchId: 'batch-1', totalCount: 1, unresolvedCount: 1, resolvedCount: 0 })
      return json({ items: [exception], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 })
    })
    render(<MemoryRouter><ExceptionReviewPage /></MemoryRouter>)

    const input = await screen.findByRole('combobox', { name: 'Bill batch' })
    fireEvent.change(input, { target: { value: 'Telecom' } })
    fireEvent.click(await screen.findByRole('option', { name: /Telecom · CORP · August 2026 · Validated/ }))

    expect(await screen.findByText('768791861')).toBeTruthy()
    expect(fetchMock.mock.calls.some(([requestInput]) => String(requestInput).includes('/api/bill-batches/batch-1/exceptions'))).toBe(true)
  })

  it('pages and filters exceptions after choosing a batch from the dropdown', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/api/bill-batches?')) return json({ items: [{ id: 'batch-1', providerId: 'provider-1', provider: 'Telecom', corporateCode: 'CORP', billingYear: 2026, billingMonth: 8, calculatedGrandTotal: 100, validationLevel: 'StructuralOnly', status: 'Validated', originalFileName: null, uploadedBy: null, uploadedAt: null }], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 })
      if (url.endsWith('/review')) return json(batch)
      if (url.endsWith('/exceptions/summary')) return json({ billBatchId: 'batch-1', totalCount: 30, unresolvedCount: 30, resolvedCount: 0 })
      return json({ items: [exception], pageNumber: 1, pageSize: 20, totalCount: 30, totalPages: 2 })
    })
    render(<MemoryRouter><ExceptionReviewPage /></MemoryRouter>)

    fireEvent.change(await screen.findByRole('combobox', { name: 'Bill batch' }), { target: { value: 'Telecom' } })
    fireEvent.click(await screen.findByRole('option', { name: /Telecom · CORP/ }))
    await screen.findByText('768791861')

    fireEvent.click(screen.getByRole('button', { name: 'Go to page 2' }))

    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) => String(input).includes('/exceptions?') && String(input).includes('pageNumber=2'))).toBe(true))
  })

  it('shows batch context and whole-batch unresolved and resolved counts', async () => {
    mockBatchExceptions()
    renderBatchPage()

    expect(await screen.findByText('August 2026')).toBeTruthy()
    expect(screen.getByText('Telecom')).toBeTruthy()
    expect(screen.getByText('CORP')).toBeTruthy()
    expect(screen.getByText('Validated')).toBeTruthy()
    expect(screen.getByText('3', { selector: '[data-kpi="total"]' })).toBeTruthy()
    expect(screen.getByText('2', { selector: '[data-kpi="unresolved"]' })).toBeTruthy()
    expect(screen.getByText('1', { selector: '[data-kpi="resolved"]' })).toBeTruthy()
    expect(screen.getByText('768791861')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Back to Bill Review' }).getAttribute('href')).toBe('/billing/batch-1/review')
    expect(screen.getByRole('link', { name: 'Processing' }).getAttribute('href')).toBe('/billing/batch-1/process')
    expect(screen.getByRole('link', { name: 'Extracted Lines' }).getAttribute('href')).toBe('/billing/batch-1/lines')
  })

  it('filters exception rows by type and resolution without changing summary requests', async () => {
    const fetchMock = mockBatchExceptions()
    renderBatchPage()
    await screen.findByText('768791861')

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Exception Type' }))
    fireEvent.click(screen.getByRole('option', { name: 'ZERO_BILL' }))
    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Resolution' }))
    fireEvent.click(screen.getByRole('option', { name: 'Resolved' }))

    await waitFor(() => expect(fetchMock.mock.calls.some(([input]) => {
      const url = String(input)
      return url.includes('/exceptions?') && url.includes('exceptionType=ZERO_BILL') && url.includes('resolution=Resolved')
    })).toBe(true))
    expect(fetchMock.mock.calls.filter(([input]) => String(input).endsWith('/exceptions/summary')).length).toBe(1)
  })

  it('explains that no historical allocation exists instead of showing an empty dropdown', async () => {
    mockBatchExceptions('Validated', [])
    renderBatchPage()
    await screen.findByText('768791861')

    fireEvent.click(screen.getByRole('button', { name: 'Resolve allocation' }))

    expect(await screen.findByText(/No historical allocation exists for mobile number 768791861/)).toBeTruthy()
    expect(screen.queryByRole('combobox', { name: 'Historical allocation' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Resolve' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Close' })).toBeTruthy()
  })

  it('shows the historical allocation dropdown when a candidate exists', async () => {
    mockBatchExceptions('Validated', [{ mobileAccountId: 'account-1', employeeId: 'employee-1', mobileNumber: '768791861', employeeEpf: 'EPF-1', employeeName: 'Former Owner', isActive: false }])
    renderBatchPage()
    await screen.findByText('768791861')

    fireEvent.click(screen.getByRole('button', { name: 'Resolve allocation' }))

    expect(await screen.findByRole('combobox', { name: 'Historical allocation' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Resolve' })).toBeTruthy()
  })

  it('keeps a locked batch readable but removes exception resolution actions', async () => {
    mockBatchExceptions('Locked')
    renderBatchPage()

    expect(await screen.findByText('768791861')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Resolve allocation' })).toBeNull()
  })

  it('displays API problem details for a missing batch', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => json({ title: 'Not Found', detail: 'Bill batch was not found.' }, 404))
    renderBatchPage()

    expect(await screen.findByText('Bill batch was not found.')).toBeTruthy()
  })
})
