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
})
