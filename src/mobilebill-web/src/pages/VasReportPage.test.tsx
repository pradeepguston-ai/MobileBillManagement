import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { VasReportPage } from './VasReportPage'

const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
const batches = [
  { batchId: 'sep', billingYear: 2026, billingMonth: 9, provider: 'Dialog', batchStatus: 'ITReview', isPreliminary: true },
  { batchId: 'aug', billingYear: 2026, billingMonth: 8, provider: 'Dialog', batchStatus: 'Locked', isPreliminary: false },
]
const report = {
  batchId: 'sep', billingYear: 2026, billingMonth: 9, provider: 'Dialog', batchStatus: 'ITReview', isPreliminary: true,
  users: 2, totalVas: 350, repeatUsers: 1, previousUsers: 3, previousTotalVas: 400,
  byFactory: [{ name: 'Factory A', users: 1, totalVas: 300 }, { name: 'Not matched', users: 1, totalVas: 50 }],
  byDepartment: [{ name: 'IT', users: 1, totalVas: 300 }],
  rows: [
    { mobileNumber: '0771', epf: 'EPF-1', employeeName: 'Kasun', callingName: null, factory: 'Factory A', department: 'IT', section: null, category: 'Staff', vas: 300, actualBill: 1000, vasShareOfBill: 30, responsibility: 'ByUser', finalDeduction: 120, monthsWithVas: 3, monthsConsidered: 3, isRepeat: true, isPooled: false, isMatched: true },
    { mobileNumber: '0774', epf: null, employeeName: null, callingName: null, factory: null, department: null, section: null, category: null, vas: 50, actualBill: 100, vasShareOfBill: 50, responsibility: null, finalDeduction: null, monthsWithVas: 1, monthsConsidered: 3, isRepeat: false, isPooled: false, isMatched: false },
  ],
}

describe('VasReportPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('shows the latest matched batch with totals, changes, repeat users and unmatched lines', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/api/reports/vas/batches')) return json(batches)
      if (url.includes('/api/reports/vas/sep')) return json(report)
      return json({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })
    })
    render(<VasReportPage />)

    const table = await screen.findByRole('table', { name: 'Numbers with VAS' })
    expect(within(table).getByText('Kasun')).toBeTruthy()
    expect(within(table).getByText('3 of 3')).toBeTruthy()
    expect(within(table).getByText('Repeat')).toBeTruthy()
    expect(within(table).getByText('30.0%')).toBeTruthy()
    expect(within(table).getByText('Not matched')).toBeTruthy()
    expect(screen.getByText(/Preliminary: this batch is still being approved/)).toBeTruthy()
    expect(screen.getByText('−1 vs last month')).toBeTruthy()
  })

  it('sends the repeat-only and minimum filters to the server', async () => {
    const requested: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      requested.push(url)
      if (url.includes('/api/reports/vas/batches')) return json(batches)
      if (url.includes('/api/reports/vas/sep')) return json(report)
      return json({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })
    })
    render(<VasReportPage />)
    await screen.findByRole('table', { name: 'Numbers with VAS' })

    fireEvent.click(screen.getByRole('switch', { name: 'Repeat users only' }))
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Minimum VAS' }), { target: { value: '100' } })

    await waitFor(() => expect(requested.some(url => url.includes('/api/reports/vas/sep?') && url.includes('minimumVas=100') && url.includes('repeatOnly=true'))).toBe(true))
  })
})
