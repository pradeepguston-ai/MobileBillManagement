import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { AuthProvider } from '../../auth/AuthContext'
import { UserManagementPage } from './UserManagementPage'

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const me = { id: 'admin-1', email: 'admin@example.com', displayName: 'Admin One', role: 'Administrator', status: 'Active' }
const users = [
  { id: 'admin-1', email: 'admin@example.com', displayName: 'Admin One', role: 'Administrator', status: 'Active', registeredAtUtc: '2026-09-01T04:00:00Z', updatedAtUtc: null },
  { id: 'user-2', email: 'eng@example.com', displayName: 'Engineer Two', role: 'ITEngineer', status: 'Active', registeredAtUtc: '2026-09-02T04:00:00Z', updatedAtUtc: null },
  { id: 'user-3', email: 'old@example.com', displayName: 'Former User', role: 'Cfo', status: 'Deactivated', registeredAtUtc: '2026-09-03T04:00:00Z', updatedAtUtc: null },
]

function mockApi() {
  localStorage.setItem('mobilebill.token', 'test-token')
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = String(input)
    if (url.includes('/api/auth/me')) return json(me)
    if (url.includes('/api/users/pending')) return json([{ id: 'p1', email: 'new@example.com', displayName: 'New Person', requestedRole: 'HeadOfIt', registeredAtUtc: '2026-09-20T04:00:00Z' }])
    if (url.endsWith('/role') && init?.method === 'PUT') return json({ ...users[1], role: 'HeadOfIt' })
    if (url.endsWith('/deactivate') || url.endsWith('/activate')) return json(users[1])
    if (url.includes('/api/users')) return json(users)
    return json({})
  })
}

const renderPage = () => render(<AuthProvider><UserManagementPage /></AuthProvider>)

describe('UserManagementPage', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks(); localStorage.clear() })

  it('shows all users first and pending registrations on the second tab', async () => {
    mockApi(); renderPage()

    expect(screen.getByRole('heading', { name: 'User Management' })).toBeTruthy()
    expect(screen.getAllByRole('tab').map(tab => tab.textContent)).toEqual(['All Users', 'Pending Users'])
    expect(await screen.findByText('Engineer Two')).toBeTruthy()
    expect(screen.getByText('Former User')).toBeTruthy()

    fireEvent.click(screen.getByRole('tab', { name: 'Pending Users' }))
    expect(await screen.findByText('New Person')).toBeTruthy()
    expect(screen.queryByText('Engineer Two')).toBeNull()
  })

  it('changes a role, confirms before deactivating, and protects the signed-in administrator', async () => {
    const fetchMock = mockApi(); renderPage()
    const engineerRow = (await screen.findByText('Engineer Two')).closest('tr')!

    fireEvent.mouseDown(within(engineerRow).getByRole('combobox'))
    fireEvent.click(await screen.findByRole('option', { name: 'Head of IT' }))
    await waitFor(() => expect(fetchMock.mock.calls.some(([url, init]) => String(url).endsWith('/api/users/user-2/role') && init?.method === 'PUT' && String(init.body) === JSON.stringify({ role: 'HeadOfIt' }))).toBe(true))

    fireEvent.click(within(screen.getByText('Engineer Two').closest('tr')!).getByRole('button', { name: 'Deactivate' }))
    const dialog = await screen.findByRole('dialog')
    expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/user-2/deactivate'))).toBe(false)
    fireEvent.click(within(dialog).getByRole('button', { name: 'Deactivate' }))
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).endsWith('/user-2/deactivate'))).toBe(true))

    const selfRow = (await screen.findByText('Admin One')).closest('tr')!
    await waitFor(() => expect(within(selfRow).getByRole('button', { name: 'Deactivate' })).toHaveProperty('disabled', true))
    expect(within(screen.getByText('Former User').closest('tr')!).getByRole('button', { name: 'Activate' })).toBeTruthy()
  })
})
