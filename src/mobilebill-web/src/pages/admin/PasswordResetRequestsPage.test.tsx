import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { PasswordResetRequestsPage } from './PasswordResetRequestsPage'

const request = { id: 'reset-1', userId: 'user-1', email: 'engineer@example.com', displayName: 'Engineer One', role: 'ITEngineer', requestedAtUtc: '2026-09-25T04:00:00Z' }
const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })

describe('PasswordResetRequestsPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('lists pending requests and approves one', async () => {
    let approved = false
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input)
      if (url.endsWith('/approve') && init?.method === 'POST') { approved = true; return new Response(null, { status: 204 }) }
      return json(approved ? [] : [request])
    })
    render(<PasswordResetRequestsPage />)

    expect(await screen.findByText('Engineer One')).toBeTruthy()
    expect(screen.getByText('IT Engineer')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }))

    expect(await screen.findByText('No pending password reset requests.')).toBeTruthy()
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/api/users/password-resets/reset-1/approve'))).toBe(true)
  })

  it('rejects a request', async () => {
    let rejected = false
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input)
      if (url.endsWith('/reject') && init?.method === 'POST') { rejected = true; return new Response(null, { status: 204 }) }
      return json(rejected ? [] : [request])
    })
    render(<PasswordResetRequestsPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Reject' }))

    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/password-resets/reset-1/reject'))).toBe(true))
    expect(await screen.findByText('No pending password reset requests.')).toBeTruthy()
  })
})
