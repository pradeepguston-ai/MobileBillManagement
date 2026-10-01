import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { RegisterPage } from './RegisterPage'

const response = (body: unknown, status = 201) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

describe('RegisterPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('submits the requested role and navigates to the pending-approval page', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValue(response({
      id: 'user-1', email: 'engineer@example.com', displayName: 'Engineer One', role: 'ITEngineer', status: 'PendingActivation',
    }))
    render(
      <MemoryRouter initialEntries={['/register']}>
        <Routes>
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/pending-approval" element={<div>Pending approval destination</div>} />
        </Routes>
      </MemoryRouter>,
    )

    fireEvent.change(screen.getByLabelText(/Full Name/), { target: { value: 'Engineer One' } })
    fireEvent.change(screen.getByLabelText(/Email/), { target: { value: 'engineer@example.com' } })
    fireEvent.change(screen.getByLabelText(/Password/), { target: { value: 'correct-horse-battery' } })
    fireEvent.click(screen.getByRole('button', { name: 'Register' }))

    expect(await screen.findByText('Pending approval destination')).toBeTruthy()
    const [, init] = fetchMock.mock.calls[0]
    const body = JSON.parse(String(init?.body)) as Record<string, unknown>
    expect(body.requestedRole).toBe('ITEngineer')
    expect(body.email).toBe('engineer@example.com')
  })

  it('rejects a password shorter than 8 characters without calling the API', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch')
    render(<MemoryRouter initialEntries={['/register']}><Routes><Route path="/register" element={<RegisterPage />} /></Routes></MemoryRouter>)

    fireEvent.change(screen.getByLabelText(/Full Name/), { target: { value: 'Engineer One' } })
    fireEvent.change(screen.getByLabelText(/Email/), { target: { value: 'engineer@example.com' } })
    fireEvent.change(screen.getByLabelText(/Password/), { target: { value: 'short' } })
    fireEvent.click(screen.getByRole('button', { name: 'Register' }))

    expect(await screen.findByText('Password must be at least 8 characters.')).toBeTruthy()
    await waitFor(() => expect(fetchMock).not.toHaveBeenCalled())
  })
})
