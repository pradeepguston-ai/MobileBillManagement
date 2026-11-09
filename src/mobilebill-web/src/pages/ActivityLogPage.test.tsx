import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ActivityLogPage } from './ActivityLogPage'

const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
const entries = [
  { id: 'a2', occurredAt: '2026-11-09T05:00:00Z', userId: 'u2', userName: 'Kumara Mohottige', role: 'GroupHrManager', area: 'Approvals', description: 'Approved the October 2026 bill batch (HR approval)' },
  { id: 'a1', occurredAt: '2026-11-09T04:00:00Z', userId: 'u1', userName: 'Pradeep Dissanayaka', role: 'ITEngineer', area: 'Billing', description: 'Uploaded the October 2026 bill PDF' },
]

describe('Activity Log', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('lists what each user did with their role and the area', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => String(input).includes('/filters')
      ? json({ users: [{ userId: 'u2', userName: 'Kumara Mohottige' }, { userId: 'u1', userName: 'Pradeep Dissanayaka' }], areas: ['Approvals', 'Billing'] })
      : json({ items: entries, pageNumber: 1, pageSize: 50, totalCount: 2, totalPages: 1 }))
    render(<ActivityLogPage />)

    const table = await screen.findByRole('table', { name: 'Activity log' })
    const [, first, second] = within(table).getAllByRole('row')
    expect(within(first).getByText('Kumara Mohottige')).toBeTruthy()
    expect(within(first).getByText('Group HR Manager')).toBeTruthy()
    expect(within(first).getByText('Approved the October 2026 bill batch (HR approval)')).toBeTruthy()
    expect(within(second).getByText('IT Engineer')).toBeTruthy()
    expect(within(second).getByText('Billing')).toBeTruthy()
    expect(screen.getByText('2 activities')).toBeTruthy()
  })

  it('filters by user, area, dates and search, and clears them', async () => {
    const requested: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/filters')) return json({ users: [{ userId: 'u1', userName: 'Pradeep Dissanayaka' }], areas: ['Billing'] })
      requested.push(url)
      return json({ items: entries.slice(1), pageNumber: 1, pageSize: 50, totalCount: 1, totalPages: 1 })
    })
    render(<ActivityLogPage />)
    await screen.findByRole('table', { name: 'Activity log' })

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'User' }))
    fireEvent.click(await screen.findByRole('option', { name: 'Pradeep Dissanayaka' }))
    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Area' }))
    fireEvent.click(await screen.findByRole('option', { name: 'Billing' }))
    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-11-01' } })
    fireEvent.change(screen.getByLabelText('To'), { target: { value: '2026-11-09' } })
    fireEvent.change(screen.getByRole('textbox', { name: 'Search' }), { target: { value: 'October' } })

    await waitFor(() => expect(requested.some(url => ['userId=u1', 'area=Billing', 'from=2026-11-01', 'to=2026-11-09', 'search=October'].every(part => url.includes(part)))).toBe(true))
    fireEvent.click(screen.getByRole('button', { name: 'Clear' }))
    await waitFor(() => expect(requested.at(-1)).not.toContain('userId='))
  })

  it('says when nothing has been recorded yet', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => String(input).includes('/filters')
      ? json({ users: [], areas: [] })
      : json({ items: [], pageNumber: 1, pageSize: 50, totalCount: 0, totalPages: 0 }))
    render(<ActivityLogPage />)

    expect(await screen.findByText('No activity has been recorded yet.')).toBeTruthy()
  })
})
