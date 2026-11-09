import { cleanup, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { BillingBatchListPage } from './BillingBatchListPage'

const batch = (id: string, billingMonth: number, status: string) => ({ id, providerId: 'provider-1', provider: 'Telecom', corporateCode: 'CORP', billingYear: 2026, billingMonth, calculatedGrandTotal: 390096.74, validationLevel: 'StructuralOnly', status, originalFileName: `${id}.pdf`, uploadedBy: 'dev-user', uploadedAt: '2026-09-09T01:00:00Z' })

describe('BillingBatchListPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('opens a validated batch on its bill review and an unfinished one on its processing page, with the downloads in line', async () => {
    const items = [batch('completed', 8, 'Completed'), batch('review', 9, 'HRApproval'), batch('draft', 10, 'Parsed')]
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ items, pageNumber: 1, pageSize: 20, totalCount: 3, totalPages: 1 }), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    render(<MemoryRouter><BillingBatchListPage /></MemoryRouter>)

    expect(await screen.findByText('August 2026')).toBeTruthy()
    expect(screen.getByText('completed.pdf')).toBeTruthy()
    expect(screen.queryByRole('link', { name: /Continue Processing/ })).toBeNull()

    const row = (period: string) => screen.getByText(period).closest('tr')!
    expect(within(row('August 2026')).getByRole('link', { name: 'Open' }).getAttribute('href')).toBe('/billing/completed/review')
    expect(within(row('September 2026')).getByRole('link', { name: 'Open' }).getAttribute('href')).toBe('/billing/review/review')
    expect(within(row('October 2026')).getByRole('link', { name: 'Open' }).getAttribute('href')).toBe('/billing/draft/process')

    // Every row has the same three actions; the downloads are only enabled for Completed and Locked batches.
    for (const period of ['August 2026', 'September 2026', 'October 2026']) {
      const actions = [...row(period).querySelectorAll('a, button')].map(element => element.textContent)
      expect(actions).toEqual(['Open', 'Download Excel', 'Download PDF'])
    }
    expect((within(row('August 2026')).getByRole('button', { name: 'Download Excel' }) as HTMLButtonElement).disabled).toBe(false)
    expect((within(row('September 2026')).getByRole('button', { name: 'Download PDF' }) as HTMLButtonElement).disabled).toBe(true)
  })
})
