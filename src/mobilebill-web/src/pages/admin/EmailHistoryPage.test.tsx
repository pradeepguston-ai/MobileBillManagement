import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { EmailHistoryPage } from './EmailHistoryPage'

const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
const automatic = { id: 'd1', ruleId: 'r1', ruleName: 'CAPL - Staff', recipients: 'hr@concord.lk; manager@concord.lk', status: 'Sent', attempts: 1, requestedAt: '2026-11-09T04:00:00Z', requestedBy: 'Automatic on lock (Pratheep Samithamby)', sentAt: '2026-11-09T04:00:05Z', lastError: null, billBatchId: 'b1', billingYear: 2026, billingMonth: 10 }
const failed = { ...automatic, id: 'd2', ruleName: 'CKL - Workers', recipients: 'ckl@concord.lk', status: 'Failed', requestedBy: 'Pradeep Dissanayaka', sentAt: null, lastError: 'Microsoft Graph refused the email (HTTP 400 ErrorInvalidRecipients).', billingMonth: 9 }
const page = (items: unknown[], totalCount = items.length) => json({ items, pageNumber: 1, pageSize: 20, totalCount, totalPages: Math.ceil(totalCount / 20) })

describe('Email History', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('lists report emails from every batch with their period, status and who sent them', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => page([automatic, failed]))
    render(<MemoryRouter><EmailHistoryPage /></MemoryRouter>)

    const table = await screen.findByRole('table', { name: 'Email history' })
    const [, first, second] = within(table).getAllByRole('row')
    expect(within(first).getByText('October 2026')).toBeTruthy()
    expect(within(second).getByText('September 2026')).toBeTruthy()
    expect(within(first).getByText('Automatic on lock (Pratheep Samithamby)')).toBeTruthy()
    expect(within(first).getByText('Sent')).toBeTruthy()
    expect(within(second).getByText('Failed')).toBeTruthy()
    expect(within(second).getByText(/ErrorInvalidRecipients/)).toBeTruthy()
    expect(screen.getByText('2 emails')).toBeTruthy()
  })

  it('filters by status and search, and resends an email', async () => {
    const requested: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input)
      requested.push(`${init?.method ?? 'GET'} ${url}`)
      if (init?.method === 'POST') return json({ ...failed, status: 'Pending' })
      return page([failed])
    })
    render(<MemoryRouter><EmailHistoryPage /></MemoryRouter>)
    await screen.findByRole('table', { name: 'Email history' })

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Status' }))
    fireEvent.click(await screen.findByRole('option', { name: 'Failed' }))
    fireEvent.change(screen.getByRole('textbox', { name: 'Search' }), { target: { value: 'ckl@' } })
    await waitFor(() => expect(requested.some(item => item.includes('status=Failed') && item.includes('search=ckl%40'))).toBe(true))

    fireEvent.click(screen.getByRole('button', { name: 'Resend' }))
    await waitFor(() => expect(requested).toContainEqual(expect.stringMatching(/^POST .*\/api\/report-distribution\/deliveries\/d2\/resend$/)))
  })

  it('says when nothing has been sent yet', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => page([]))
    render(<MemoryRouter><EmailHistoryPage /></MemoryRouter>)

    expect(await screen.findByText('No report emails have been sent yet.')).toBeTruthy()
  })
})
