import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { BillBatch } from '../../api/billingApi'
import { BillingProcessingPage } from './BillingProcessingPage'

const batch = (status: string, overrides: Partial<BillBatch> = {}): BillBatch => ({
  id: 'batch-1', providerId: 'provider-1', providerName: 'Telecom', corporateCode: 'CORP', billingYear: 2026, billingMonth: 8, status,
  originalFileName: null, fileHash: null, uploadedBy: null, uploadedAt: null, statedGrandTotal: null, calculatedGrandTotal: null, difference: null,
  grandTotalSource: 'None', validationLevel: 'None', validationWarning: null, totalCandidates: 0, successfulCount: 0, failedCount: 0, warnings: [], ...overrides,
})
const response = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
const renderPage = () => render(<MemoryRouter initialEntries={['/billing/batch-1/process']}><Routes><Route path="/billing/:batchId/process" element={<BillingProcessingPage />} /></Routes></MemoryRouter>)

describe('BillingProcessingPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('continues upload, parse and validation using server responses', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input)
      if (!init?.method) return response(batch('Draft'))
      if (url.endsWith('/upload')) return response(batch('Uploaded', { originalFileName: 'sample-bill.pdf', uploadedBy: 'dev-user' }))
      if (url.endsWith('/parse')) return response(batch('Parsed', { totalCandidates: 100, successfulCount: 99, failedCount: 1, calculatedGrandTotal: 390096.74 }))
      return response(batch('Validated', { totalCandidates: 100, successfulCount: 99, failedCount: 1, calculatedGrandTotal: 390096.74, validationLevel: 'StructuralOnly', validationWarning: 'Source PDF grand total could not be independently extracted.' }))
    })
    renderPage()

    const file = new File(['%PDF-1.7'], 'sample-bill.pdf', { type: 'application/pdf' })
    fireEvent.change(await screen.findByLabelText('Choose PDF file'), { target: { files: [file] } })
    fireEvent.click(screen.getByRole('button', { name: 'Upload PDF' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Parse Bill' }))
    expect(await screen.findByText('390,096.74', { exact: false })).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Validate Bill' }))

    expect(await screen.findByText('Source PDF grand total could not be independently extracted.')).toBeTruthy()
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/upload'))).toBe(true)
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/parse'))).toBe(true)
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/validate'))).toBe(true)
  })

  it('reconstructs persisted parse counts without parsing again', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(response(batch('Parsed', { totalCandidates: 142, successfulCount: 141, failedCount: 1, calculatedGrandTotal: 390096.74, validationLevel: 'StructuralOnly', warnings: ['One candidate requires review.'] })))
    renderPage()

    expect(await screen.findByText('142')).toBeTruthy()
    expect(screen.getByText('141')).toBeTruthy()
    expect(screen.getByText('One candidate requires review.')).toBeTruthy()
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('shows server problem details for failed processing actions', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (_input, init) => init?.method
      ? new Response(JSON.stringify({ title: 'Invalid transition', detail: 'Only uploaded batches can be parsed.' }), { status: 409, headers: { 'Content-Type': 'application/json' } })
      : response(batch('Uploaded')))
    renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Parse Bill' }))
    expect(await screen.findByText('Only uploaded batches can be parsed.')).toBeTruthy()
  })

  it('shows valid batch navigation for a review-ready batch', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(response(batch('Validated')))
    renderPage()

    expect((await screen.findByRole('link', { name: 'Extracted Lines' })).getAttribute('href')).toBe('/billing/batch-1/lines')
    expect(screen.getByRole('link', { name: 'Exceptions' }).getAttribute('href')).toBe('/billing/batch-1/exceptions')
    expect(screen.getByRole('link', { name: 'Review' }).getAttribute('href')).toBe('/billing/batch-1/review')
  })

  it('matches bill lines to employees before opening review for a validated batch', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input)
      if (init?.method === 'POST' && url.endsWith('/match')) return response({ billBatchId: 'batch-1', monthlyBillsCreated: 259, exceptionsCreated: 0 })
      return response(batch('Validated'))
    })
    render(<MemoryRouter initialEntries={['/billing/batch-1/process']}><Routes><Route path="/billing/:batchId/process" element={<BillingProcessingPage />} /><Route path="/billing/:batchId/review" element={<div>Review destination</div>} /></Routes></MemoryRouter>)

    fireEvent.click(await screen.findByRole('button', { name: 'Match Records & Open Review' }))

    expect(await screen.findByText('Review destination')).toBeTruthy()
    expect(fetchMock.mock.calls.some(([url, init]) => String(url).endsWith('/match') && init?.method === 'POST')).toBe(true)
  })

  it('treats an already-matched conflict as success and still opens review', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input)
      if (init?.method === 'POST' && url.endsWith('/match'))
        return new Response(JSON.stringify({ title: 'This batch has already been matched. Matching cannot be repeated.' }), { status: 409, headers: { 'Content-Type': 'application/json' } })
      return response(batch('Validated'))
    })
    render(<MemoryRouter initialEntries={['/billing/batch-1/process']}><Routes><Route path="/billing/:batchId/process" element={<BillingProcessingPage />} /><Route path="/billing/:batchId/review" element={<div>Review destination</div>} /></Routes></MemoryRouter>)

    fireEvent.click(await screen.findByRole('button', { name: 'Match Records & Open Review' }))

    expect(await screen.findByText('Review destination')).toBeTruthy()
  })
})
