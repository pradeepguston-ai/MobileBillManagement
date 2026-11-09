import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const auth = vi.hoisted(() => ({ user: { id: 'u1', email: 'cfo@example.com', displayName: 'CFO', role: 'Cfo', status: 'Active' } as { id: string; role: string } | null }))
vi.mock('../../auth/AuthContext', () => ({ useAuth: () => ({ user: auth.user }) }))

import { PendingApprovalsAlert } from '../insights/PendingApprovalsAlert'
import { PendingApprovalsBell } from './PendingApprovalsBell'

const october = { batchId: 'b1', billingYear: 2026, billingMonth: 10, provider: 'Dialog Axiata', corporateCode: 'C', status: 'FinanceApproval', step: 'Finance approval', numberCount: 245, waitingSince: '2026-11-03T06:25:00Z', lastActionBy: 'Kumara Mohottige', lastComment: 'HR approved' }
const september = { ...october, batchId: 'b0', billingMonth: 9, waitingSince: '2026-10-03T06:25:00Z' }
const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })

function renderBell() {
  return render(<MemoryRouter initialEntries={['/']}><Routes>
    <Route path="/" element={<PendingApprovalsBell />} />
    <Route path="/billing/:batchId/review" element={<div>Review page</div>} />
  </Routes></MemoryRouter>)
}

describe('Pending approvals bell', () => {
  beforeEach(() => { sessionStorage.clear(); auth.user = { id: 'u1', role: 'Cfo' } })
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('shows how many batches wait for the approver and opens one from the list', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => json([september, october]))
    renderBell()

    const bell = await screen.findByRole('button', { name: 'Pending approvals: 2' })
    fireEvent.click(bell)
    const list = await screen.findByRole('list', { name: 'Pending approvals' })
    expect(within(list).getAllByRole('listitem')).toHaveLength(2)
    expect(within(list).getByText('October 2026 · Dialog Axiata')).toBeTruthy()
    expect(within(list).getAllByText(/Waiting for Finance approval · 245 numbers/)).toHaveLength(2)
    expect(within(list).getAllByText(/Kumara Mohottige, .*: “HR approved”/).length).toBe(2)

    fireEvent.click(within(list).getByText('October 2026 · Dialog Axiata'))
    expect(await screen.findByText('Review page')).toBeTruthy()
  })

  it('pops up a batch that has just arrived once, and not again after a refresh', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => json([october]))
    const first = renderBell()

    expect(await screen.findByText('The October 2026 bill is waiting for your Finance approval.')).toBeTruthy()
    first.unmount()

    renderBell()
    await screen.findByRole('button', { name: 'Pending approvals: 1' })
    expect(screen.queryByText('The October 2026 bill is waiting for your Finance approval.')).toBeNull()
  })

  it('opens the batch from the pop-up', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => json([october]))
    renderBell()

    fireEvent.click(within(await screen.findByRole('alert')).getByRole('button', { name: 'Open' }))

    expect(await screen.findByText('Review page')).toBeTruthy()
  })

  it('says nothing is waiting when the list is empty', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => json([]))
    renderBell()

    fireEvent.click(await screen.findByRole('button', { name: 'Pending approvals: 0' }))

    expect(await screen.findByText('No billing batches are waiting for your approval.')).toBeTruthy()
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('is not shown to roles that approve nothing', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async () => json([october]))
    auth.user = { id: 'u2', role: 'ITEngineer' }
    renderBell()

    await new Promise(resolve => setTimeout(resolve, 20))
    expect(screen.queryByRole('button', { name: /Pending approvals/ })).toBeNull()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})

describe('Pending approvals dashboard reminder', () => {
  beforeEach(() => { auth.user = { id: 'u1', role: 'Cfo' } })
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('names the oldest waiting batch and links to it', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => json([september, october]))
    render(<MemoryRouter><PendingApprovalsAlert /></MemoryRouter>)

    expect(await screen.findByText('2 billing batches are waiting for your approval. The oldest is September 2026 (Finance approval).')).toBeTruthy()
    await waitFor(() => expect(screen.getByRole('link', { name: 'Review oldest' }).getAttribute('href')).toBe('/billing/b0/review'))
  })
})
