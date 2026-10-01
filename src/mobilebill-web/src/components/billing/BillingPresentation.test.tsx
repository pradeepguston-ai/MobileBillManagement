import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it } from 'vitest'

import { ApprovalTimeline } from './ApprovalTimeline'
import { AuditTimeline } from './AuditTimeline'
import { BatchContextNavigation } from './BatchContextNavigation'
import { BillChargeBreakdown } from './BillChargeBreakdown'

describe('shared billing presentation', () => {
  afterEach(cleanup)

  it('presents every persisted PDF charge without deriving values', () => {
    render(<BillChargeBreakdown charges={{ previousDue: 1, payments: -2, totalUsage: 3, idd: 4, roaming: 5, vas: 6, discounts: -7, billAdjustments: 8, commitmentCharges: 9, latePaymentCharges: 10, addToBill: 11, instalmentPlans: 12, governmentTaxesLevies: 13, vat: 14, chargesForBillPeriod: 15, totalDueAmount: 16 }} />)

    expect(screen.getByText('Previous Due').parentElement?.textContent?.replaceAll('\u00a0', ' ')).toContain('LKR 1.00')
    expect(screen.getByText('Payments').parentElement?.textContent?.replaceAll('\u00a0', ' ')).toContain('-LKR 2.00')
    expect(screen.getByText('Total Due').parentElement?.textContent?.replaceAll('\u00a0', ' ')).toContain('LKR 16.00')
  })

  it('renders actual approval entries and explicit empty timeline states', () => {
    const approval = { id: 'a1', workflowStage: 'ITReview', action: 'Approve', workflowRole: 'ITReviewer', userId: 'u1', displayName: 'IT User', timestamp: '2026-09-10T04:00:00Z', comment: 'Checked', previousStatus: 'ITReview', newStatus: 'HRApproval' }
    const view = render(<ApprovalTimeline history={[approval]} />)
    expect(screen.getByText('IT User')).toBeTruthy()
    expect(screen.getByText('Checked')).toBeTruthy()

    view.rerender(<ApprovalTimeline history={[]} />)
    expect(screen.getByText('No approval history yet')).toBeTruthy()
    view.rerender(<AuditTimeline entries={[]} />)
    expect(screen.getByText('No audit history yet')).toBeTruthy()
  })

  it('formats raw ISO timestamps in audit entries as readable local date and time', () => {
    render(<AuditTimeline entries={['Assessed by Nimal Perera at 2026-09-25T04:46:14.0402859+00:00']} />)

    const text = screen.getByText(/Assessed by Nimal Perera at /).textContent ?? ''
    expect(text).not.toContain('T04:46')
    expect(text).not.toContain('+00:00')
    expect(text).toContain('2026')
  })

  it('shows the full batch journey for a completed batch', () => {
    render(<MemoryRouter><BatchContextNavigation batchId="batch-1" status="Completed" /></MemoryRouter>)

    expect(screen.getByRole('link', { name: 'Processing' }).getAttribute('href')).toBe('/billing/batch-1/process')
    expect(screen.getByRole('link', { name: 'Extracted Lines' }).getAttribute('href')).toBe('/billing/batch-1/lines')
    expect(screen.getByRole('link', { name: 'Exceptions' }).getAttribute('href')).toBe('/billing/batch-1/exceptions')
    expect(screen.getByRole('link', { name: 'Review' }).getAttribute('href')).toBe('/billing/batch-1/review')
    expect(screen.getByRole('link', { name: 'Report' }).getAttribute('href')).toBe('/reports/monthly-bill')
  })
})
