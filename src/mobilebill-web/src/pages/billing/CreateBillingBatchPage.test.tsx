import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { CreateBillingBatchPage } from './CreateBillingBatchPage'

const response = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })

describe('CreateBillingBatchPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('uses active provider master data and navigates to processing after create', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (_input, init) => init?.method === 'POST'
      ? response({ id: 'batch-1' })
      : response({ items: [{ id: 'provider-1', name: 'Telecom', isActive: true }], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 }))
    render(<MemoryRouter initialEntries={['/billing/new']}><Routes><Route path="/billing/new" element={<CreateBillingBatchPage />} /><Route path="/billing/:batchId/process" element={<div>Processing destination</div>} /></Routes></MemoryRouter>)

    fireEvent.mouseDown(await screen.findByRole('combobox', { name: /Provider/ }))
    fireEvent.click(screen.getByRole('option', { name: 'Telecom' }))
    fireEvent.change(screen.getByRole('textbox', { name: /Corporate Code/ }), { target: { value: ' CORP ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Create Batch' }))

    expect(await screen.findByText('Processing destination')).toBeTruthy()
    const createCall = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(createCall).toBeTruthy()
    const request = JSON.parse(String(createCall?.[1]?.body)) as Record<string, unknown>
    expect(request.corporateCode).toBe('CORP')
    expect(Object.keys(request).sort()).toEqual(['billingMonth', 'billingYear', 'corporateCode', 'providerId'])
    await waitFor(() => expect(String(fetchMock.mock.calls[0][0])).toContain('isActive=true'))
  })
  it('pre-fills the usual corporate code and sends it when left unchanged', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (_input, init) => init?.method === 'POST'
      ? response({ id: 'batch-1' })
      : response({ items: [{ id: 'provider-1', name: 'Telecom', isActive: true }], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 }))
    render(<MemoryRouter initialEntries={['/billing/new']}><Routes><Route path="/billing/new" element={<CreateBillingBatchPage />} /><Route path="/billing/:batchId/process" element={<div>Processing destination</div>} /></Routes></MemoryRouter>)

    expect((screen.getByRole('textbox', { name: /Corporate Code/ }) as HTMLInputElement).value).toBe('PR48799679')
    fireEvent.mouseDown(await screen.findByRole('combobox', { name: /Provider/ }))
    fireEvent.click(screen.getByRole('option', { name: 'Telecom' }))
    fireEvent.click(screen.getByRole('button', { name: 'Create Batch' }))

    expect(await screen.findByText('Processing destination')).toBeTruthy()
    const createCall = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')
    expect(JSON.parse(String(createCall?.[1]?.body)).corporateCode).toBe('PR48799679')
  })


  it('pre-selects Dialog as the provider but still allows another provider', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (_input, init) => init?.method === 'POST'
      ? response({ id: 'batch-1' })
      : response({ items: [{ id: 'provider-m', code: 'MOBITEL', name: 'Mobitel', isActive: true }, { id: 'provider-d', code: 'DIALOG', name: 'Dialog', isActive: true }], pageNumber: 1, pageSize: 100, totalCount: 2, totalPages: 1 }))
    render(<MemoryRouter initialEntries={['/billing/new']}><Routes><Route path="/billing/new" element={<CreateBillingBatchPage />} /><Route path="/billing/:batchId/process" element={<div>Processing destination</div>} /></Routes></MemoryRouter>)

    await waitFor(() => expect(screen.getByRole('combobox', { name: /Provider/ }).textContent).toBe('Dialog'))
    fireEvent.click(screen.getByRole('button', { name: 'Create Batch' }))
    expect(await screen.findByText('Processing destination')).toBeTruthy()
    expect(JSON.parse(String(fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')?.[1]?.body)).providerId).toBe('provider-d')
  })
})
