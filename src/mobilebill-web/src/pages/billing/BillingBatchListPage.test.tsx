import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { BillingBatchListPage } from './BillingBatchListPage'

describe('BillingBatchListPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('shows batch metadata, processing route, review continuation, and eligible report action', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ items: [{ id: 'batch-1', providerId: 'provider-1', provider: 'Telecom', corporateCode: 'CORP', billingYear: 2026, billingMonth: 8, calculatedGrandTotal: 390096.74, validationLevel: 'StructuralOnly', status: 'Completed', originalFileName: 'sample-bill.pdf', uploadedBy: 'dev-user', uploadedAt: '2026-09-09T01:00:00Z' }], pageNumber: 1, pageSize: 20, totalCount: 1, totalPages: 1 }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    render(<MemoryRouter><BillingBatchListPage /></MemoryRouter>)

    expect(await screen.findByText('August 2026')).toBeTruthy()
    expect(screen.getByText('sample-bill.pdf')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Open' }).getAttribute('href')).toBe('/billing/batch-1/process')
    expect(screen.getByRole('link', { name: 'Continue Processing' }).getAttribute('href')).toBe('/billing/batch-1/review')
    expect(screen.getByRole('button', { name: 'Download Report' })).toBeTruthy()
  })
})
