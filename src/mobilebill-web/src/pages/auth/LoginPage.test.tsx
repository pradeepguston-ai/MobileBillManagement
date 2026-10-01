import { cleanup, fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { AuthProvider } from '../../auth/AuthContext'
import { LoginPage } from './LoginPage'

const response = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })

describe('LoginPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks(); localStorage.clear() })

  it('signs in and navigates to the dashboard on success', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(response({
      token: 'jwt-token',
      expiresAtUtc: '2030-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'engineer@example.com', displayName: 'Engineer One', role: 'ITEngineer', status: 'Active' },
    }))
    render(
      <MemoryRouter initialEntries={['/login']}>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/" element={<div>Dashboard destination</div>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    )

    fireEvent.change(screen.getByLabelText(/Email/), { target: { value: 'engineer@example.com' } })
    fireEvent.change(screen.getByLabelText(/Password/), { target: { value: 'correct-horse-battery' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Dashboard destination')).toBeTruthy()
  })

  it('shows the server error message when login is rejected', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(response({ status: 401, title: 'The email or password is incorrect.' }, 401))
    render(
      <MemoryRouter initialEntries={['/login']}>
        <AuthProvider>
          <Routes><Route path="/login" element={<LoginPage />} /></Routes>
        </AuthProvider>
      </MemoryRouter>,
    )

    fireEvent.change(screen.getByLabelText(/Email/), { target: { value: 'engineer@example.com' } })
    fireEvent.change(screen.getByLabelText(/Password/), { target: { value: 'wrong-password' } })
    fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('The email or password is incorrect.')).toBeTruthy()
  })
})
