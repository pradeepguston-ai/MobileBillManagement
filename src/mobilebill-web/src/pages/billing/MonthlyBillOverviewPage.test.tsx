import { cleanup, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { DashboardPage } from './DashboardPage'

const batches = [
  { id: 'newest', providerId: 'provider-1', provider: 'Dialog', corporateCode: 'CORP', billingYear: 2026, billingMonth: 8, calculatedGrandTotal: 390096.74, validationLevel: 'StructuralOnly', status: 'HRApproval', originalFileName: 'august.pdf', uploadedBy: 'dev-user', uploadedAt: '2026-09-01T01:00:00Z' },
  { id: 'older', providerId: 'provider-1', provider: 'Dialog', corporateCode: 'CORP', billingYear: 2026, billingMonth: 7, calculatedGrandTotal: 350000, validationLevel: 'StructuralOnly', status: 'Completed', originalFileName: 'july.pdf', uploadedBy: 'dev-user', uploadedAt: '2026-08-01T01:00:00Z' },
]

const latestSummary = { batchId: 'newest', billingYear: 2026, billingMonth: 8, provider: 'Dialog', corporateCode: 'CORP', batchStatus: 'HRApproval', validationLevel: 'StructuralOnly', totalAccounts: 250, totalActualBill: 390096.74, totalCalculatedExcess: 9000, totalFinalDeduction: 7250.5, companyResponsibilityAmount: 1750, exceptionCount: 3, unmatchedCount: 1, assessedCount: 241, unassessedCount: 9, approvalHistory: [] }

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' } })
}

describe('DashboardPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('uses the server latest-period ordering and renders API-provided KPIs and workflow', async () => {
    const requested: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input); requested.push(url)
      if (url.includes('/api/bill-batches?')) return json({ items: batches, pageNumber: 1, pageSize: 5, totalCount: 2, totalPages: 1 })
      if (url.endsWith('/api/bill-batches/newest/review')) return json(latestSummary)
      return json({ detail: 'Unexpected request' }, 500)
    })

    render(<MemoryRouter><DashboardPage /></MemoryRouter>)

    expect(await screen.findByRole('heading', { name: 'August 2026' })).toBeTruthy()
    expect(screen.getAllByText('LKR 390,096.74')).toHaveLength(2)
    expect(screen.getByText('LKR 7,250.50')).toBeTruthy()
    expect(screen.getByText('Exception Count').parentElement?.textContent).toContain('3')
    expect(screen.getByText('Unassessed Count').parentElement?.textContent).toContain('9')
    expect(screen.getByText('Current stage: HR Approval')).toBeTruthy()
    expect(screen.getByText('July 2026')).toBeTruthy()
    expect(screen.getAllByRole('link', { name: 'Open' })[0].getAttribute('href')).toBe('/billing/newest/review')
    expect(requested[0]).toContain('sortBy=billingYear')
    expect(requested[0]).toContain('sortDirection=desc')
    expect(requested.some(url => url.endsWith('/api/bill-batches/newest/review'))).toBe(true)
  })

  it('shows an actionable empty state when no batches exist', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json({ items: [], pageNumber: 1, pageSize: 5, totalCount: 0, totalPages: 0 }))
    render(<MemoryRouter><DashboardPage /></MemoryRouter>)

    expect(await screen.findByText('No billing batches available yet.')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Create Billing Batch' }).getAttribute('href')).toBe('/billing/new')
  })

  it('keeps recent batches visible when the latest summary is unavailable', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => String(input).includes('/api/bill-batches?')
      ? json({ items: batches, pageNumber: 1, pageSize: 5, totalCount: 2, totalPages: 1 })
      : json({ detail: 'Review summary is temporarily unavailable.' }, 503))
    render(<MemoryRouter><DashboardPage /></MemoryRouter>)

    expect(await screen.findByText('Review summary is temporarily unavailable.')).toBeTruthy()
    expect(screen.getAllByText('August 2026')).toHaveLength(2)
    expect(screen.getByText('July 2026')).toBeTruthy()
    await waitFor(() => expect(screen.queryByText('Loading dashboard…')).toBeNull())
  })
})
