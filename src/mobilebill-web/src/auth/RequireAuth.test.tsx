import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { AuthProvider } from './AuthContext'
import { RequireAuth } from './RequireAuth'

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const newUser = { id: 'user-9', email: 'nimal@concord.lk', displayName: 'Nimal Perera', role: 'HrUser', status: 'Active', mustChangePassword: true }

function mockApi(changeResponse: () => Response) {
  localStorage.setItem('mobilebill.token', 'one-time-token')
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
    const url = String(input)
    if (url.includes('/api/auth/me')) return json(newUser)
    if (url.includes('/api/auth/change-password')) return changeResponse()
    return json([])
  })
}

const renderApp = () => render(
  <MemoryRouter><AuthProvider><RequireAuth><h1>Dashboard</h1></RequireAuth></AuthProvider></MemoryRouter>,
)

const fill = (current: string, password: string, confirm = password) => {
  fireEvent.change(screen.getByLabelText(/One-time password/), { target: { value: current } })
  fireEvent.change(screen.getByLabelText(/^New password/), { target: { value: password } })
  fireEvent.change(screen.getByLabelText(/Confirm new password/), { target: { value: confirm } })
  fireEvent.click(screen.getByRole('button', { name: 'Set password and continue' }))
}

describe('RequireAuth with a one-time password', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks(); localStorage.clear() })

  it('asks for a new password before opening the app, then continues with the fresh token', async () => {
    const fetchMock = mockApi(() => json({ token: 'full-token', expiresAtUtc: '2026-11-17T00:00:00Z', user: { ...newUser, mustChangePassword: false } }))
    renderApp()

    expect(await screen.findByRole('heading', { name: 'Set your password' })).toBeTruthy()
    expect(screen.getByText(/Welcome, Nimal Perera/)).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Dashboard' })).toBeNull()

    fill('OneTime#123', 'MyOwn#Pass1', 'MyOwn#Pass2')
    expect(await screen.findByText('The new passwords do not match.')).toBeTruthy()
    fill('OneTime#123', 'OneTime#123')
    expect(await screen.findByText('Choose a new password that is different from the one-time password.')).toBeTruthy()
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/change-password'))).toBe(false)
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/api/permissions/mine'))).toBe(false)

    fill('OneTime#123', 'MyOwn#Pass1')
    expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeTruthy()
    const call = fetchMock.mock.calls.find(([url]) => String(url).includes('/api/auth/change-password'))!
    expect(JSON.parse(String(call[1]!.body))).toEqual({ currentPassword: 'OneTime#123', newPassword: 'MyOwn#Pass1' })
    expect(localStorage.getItem('mobilebill.token')).toBe('full-token')
    // The screen permissions are only loaded once the password has been changed.
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/api/permissions/mine'))).toBe(true))
  })

  it('shows why the change was refused and keeps the user on the page', async () => {
    mockApi(() => json({ title: 'Bad request', detail: 'The one-time password is incorrect.' }, 400))
    renderApp()

    await screen.findByRole('heading', { name: 'Set your password' })
    fill('Wrong#123', 'MyOwn#Pass1')

    expect(await screen.findByText('The one-time password is incorrect.')).toBeTruthy()
    expect(screen.queryByRole('heading', { name: 'Dashboard' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Set password and continue' })).toHaveProperty('disabled', false)
  })
})
