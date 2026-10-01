import { cleanup, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { MonthlyBillOverviewPage } from './MonthlyBillOverviewPage'

const batches = [
  { id: 'newest', providerId: 'provider-1', provider: 'Dialog', corporateCode: 'CORP', billingYear: 2026, billingMonth: 8, calculatedGrandTotal: 390096.74, validationLevel: 'StructuralOnly', status: 'HRApproval', originalFileName: 'august.pdf', uploadedBy: 'dev-user', uploadedAt: '2026-09-01T01:00:00Z' },
  { id: 'older', providerId: 'provider-1', provider: 'Dialog', corporateCode: 'CORP', billingYear: 2026, billingMonth: 7, calculatedGrandTotal: 350000, validationLevel: 'StructuralOnly', status: 'Completed', originalFileName: 'july.pdf', uploadedBy: 'dev-user', uploadedAt: '2026-08-01T01:00:00Z' },
]

const latestSummary = { batchId: 'newest', billingYear: 2026, billingMonth: 8, provider: 'Dialog', corporateCode: 'CORP', batchStatus: 'HRApproval', validationLevel: 'StructuralOnly', totalAccounts: 250, totalActualBill: 390096.74, totalCalculatedExcess: 9000, totalFinalDeduction: 7250.5, companyResponsibilityAmount: 1750, exceptionCount: 3, unmatchedCount: 1, assessedCount: 241, unassessedCount: 9, approvalHistory: [] }

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' } })
}

describe('MonthlyBillOverviewPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('uses the server latest-period ordering and renders API-provided KPIs and workflow', async () => {
    const requested: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input); requested.push(url)
      if (url.includes('/api/bill-batches?')) return json({ items: batches, pageNumber: 1, pageSize: 5, totalCount: 2, totalPages: 1 })
      if (url.endsWith('/api/bill-batches/newest/review')) return json(latestSummary)
      return json({ detail: 'Unexpected request' }, 500)
    })

    render(<MemoryRouter><MonthlyBillOverviewPage /></MemoryRouter>)

    expect(await screen.findByRole('heading', { name: 'August 2026' })).toBeTruthy()
    expect(screen.getAllByText('LKR 390,096.74')).toHaveLength(2)
    expect(screen.getByText('LKR 7,250.50')).toBeTruthy()
    expect(screen.getByText('Company Responsibility Amount').parentElement?.textContent).toContain('1,750.00')
    expect(screen.queryByText('Exception Count')).toBeNull()
    expect(screen.getByText('Total Calculated Excess').parentElement?.textContent).toContain('9,000.00')
    expect(screen.queryByText('Unassessed Count')).toBeNull()
    expect(screen.getAllByText(/^(Total Actual Bill|Total Calculated Excess|Final User Deduction|Company Responsibility Amount)$/).map(node => node.textContent)).toEqual(['Total Actual Bill', 'Total Calculated Excess', 'Final User Deduction', 'Company Responsibility Amount'])
    expect(screen.getByText('Current stage: HR Approval')).toBeTruthy()
    expect(screen.getByText('July 2026')).toBeTruthy()
    expect(screen.getAllByRole('link', { name: 'Open' })[0].getAttribute('href')).toBe('/billing/newest/review')
    expect(requested[0]).toContain('sortBy=billingYear')
    expect(requested[0]).toContain('sortDirection=desc')
    expect(requested.some(url => url.endsWith('/api/bill-batches/newest/review'))).toBe(true)
  })

  it('shows the latest batch approval history inside the latest batch card above Open', async () => {
    const approval = { id: 'a1', workflowStage: 'ITReview', action: 'Approve', workflowRole: 'ITReviewer', userRole: 'HeadOfIt', userId: 'u1', displayName: 'Head User', timestamp: '2026-09-10T04:00:00Z', comment: 'Looks right', previousStatus: 'ITReview', newStatus: 'HRApproval' }
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/api/bill-batches?')) return json({ items: batches, pageNumber: 1, pageSize: 5, totalCount: 2, totalPages: 1 })
      if (url.endsWith('/api/bill-batches/newest/review')) return json({ ...latestSummary, approvalHistory: [approval] })
      return json({ detail: 'Unexpected request' }, 500)
    })

    render(<MemoryRouter><MonthlyBillOverviewPage /></MemoryRouter>)

    const history = await screen.findByRole('table', { name: 'Approval history' })
    expect(within(history).getByText('Head User')).toBeTruthy()
    expect(within(history).getByText('Head of IT')).toBeTruthy()
    expect(within(history).getByText('Looks right')).toBeTruthy()
    // Inside the Current / Latest Batch card, just above its Open button.
    const latestCard = screen.getByRole('heading', { name: 'August 2026' }).closest('.MuiCard-root')!
    expect(latestCard.contains(history)).toBe(true)
    const openButton = within(latestCard as HTMLElement).getByRole('link', { name: 'Open' })
    expect(history.compareDocumentPosition(openButton) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('shows an actionable empty state when no batches exist', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(json({ items: [], pageNumber: 1, pageSize: 5, totalCount: 0, totalPages: 0 }))
    render(<MemoryRouter><MonthlyBillOverviewPage /></MemoryRouter>)

    expect(await screen.findByText('No billing batches available yet.')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Create Billing Batch' }).getAttribute('href')).toBe('/billing/new')
  })

  it('keeps recent batches visible when the latest summary is unavailable', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => String(input).includes('/api/bill-batches?')
      ? json({ items: batches, pageNumber: 1, pageSize: 5, totalCount: 2, totalPages: 1 })
      : json({ detail: 'Review summary is temporarily unavailable.' }, 503))
    render(<MemoryRouter><MonthlyBillOverviewPage /></MemoryRouter>)

    expect(await screen.findByText('Review summary is temporarily unavailable.')).toBeTruthy()
    expect(screen.getAllByText('August 2026')).toHaveLength(2)
    expect(screen.getByText('July 2026')).toBeTruthy()
    await waitFor(() => expect(screen.queryByText('Loading dashboard…')).toBeNull())
  })
})
