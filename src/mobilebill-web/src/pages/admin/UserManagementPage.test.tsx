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
    if (url.endsWith('/api/users') && init?.method === 'POST') return json({ id: 'user-9', ...JSON.parse(String(init.body)), status: 'Active', registeredAtUtc: '2026-11-16T04:00:00Z', updatedAtUtc: null })
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
    await screen.findByRole('tab', { name: 'Create User' })
    expect(screen.getAllByRole('tab').map(tab => tab.textContent)).toEqual(['All Users', 'Pending Users', 'Create User', 'Permissions'])
    expect(await screen.findByText('Engineer Two')).toBeTruthy()
    expect(screen.getByText('Former User')).toBeTruthy()

    fireEvent.click(screen.getByRole('tab', { name: 'Pending Users' }))
    expect(await screen.findByText('New Person')).toBeTruthy()
    expect(screen.queryByText('Engineer Two')).toBeNull()
  })

  it('creates an active account from the Create User tab after checking the passwords match', async () => {
    const fetchMock = mockApi(); renderPage()
    fireEvent.click(await screen.findByRole('tab', { name: 'Create User' }))

    fireEvent.change(screen.getByLabelText(/Full Name/), { target: { value: ' Nimal Perera ' } })
    fireEvent.change(screen.getByLabelText(/Email/), { target: { value: 'nimal@concord.lk' } })
    fireEvent.mouseDown(screen.getByRole('combobox', { name: /Role/ }))
    fireEvent.click(await screen.findByRole('option', { name: 'HR User' }))
    fireEvent.change(screen.getByLabelText(/^Password/), { target: { value: 'Str0ngPass!' } })
    fireEvent.change(screen.getByLabelText(/Confirm Password/), { target: { value: 'different1' } })
    fireEvent.click(screen.getByRole('button', { name: 'Create User' }))
    expect(await screen.findByText('The passwords do not match.')).toBeTruthy()
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)

    fireEvent.change(screen.getByLabelText(/Confirm Password/), { target: { value: 'Str0ngPass!' } })
    fireEvent.click(screen.getByRole('button', { name: 'Create User' }))

    expect(await screen.findByText('Nimal Perera (nimal@concord.lk) can now sign in as HR User.')).toBeTruthy()
    const post = fetchMock.mock.calls.find(([url, init]) => String(url).endsWith('/api/users') && init?.method === 'POST')!
    expect(JSON.parse(String(post[1]!.body))).toEqual({ displayName: 'Nimal Perera', email: 'nimal@concord.lk', role: 'HrUser', password: 'Str0ngPass!' })
  })

  it('lets a granted role manage other accounts but never Administrator accounts, the Administrator role or permissions', async () => {
    const granted = [{ module: 'user-management', canView: true, canCreate: false, canEdit: true, canDelete: false }]
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/api/auth/me')) return json({ ...me, id: 'head-1', role: 'HeadOfIt' })
      if (url.includes('/api/permissions/mine')) return json(granted)
      if (url.includes('/api/users/pending')) return json([])
      if (url.includes('/api/users')) return json(users)
      return json({})
    })
    localStorage.setItem('mobilebill.token', 'test-token')
    renderPage()

    const engineerRow = (await screen.findByText('Engineer Two')).closest('tr')!
    // Enabled once the role's saved permissions have loaded (HeadOfIt has no User Management rights by default).
    await waitFor(() => expect(within(engineerRow).getByRole('combobox').getAttribute('aria-disabled')).not.toBe('true'))
    expect(screen.getAllByRole('tab').map(tab => tab.textContent)).toEqual(['All Users', 'Pending Users'])
    expect(within(screen.getByText('Admin One').closest('tr')!).getByRole('combobox').getAttribute('aria-disabled')).toBe('true')
    expect(within(engineerRow).queryByRole('button', { name: 'Deactivate' })).toBeNull()

    fireEvent.mouseDown(within(engineerRow).getByRole('combobox'))
    expect(await screen.findByRole('option', { name: 'Head of IT' })).toBeTruthy()
    expect(screen.queryByRole('option', { name: 'Administrator' })).toBeNull()
  })

  it('hides the Create User tab from anyone but an administrator', async () => {
    const fetchMock = mockApi()
    fetchMock.mockImplementation(async input => String(input).includes('/api/auth/me') ? json({ ...me, role: 'ITEngineer' }) : json([]))
    renderPage()

    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/api/auth/me'))).toBe(true))
    await new Promise(resolve => setTimeout(resolve, 0))
    expect(screen.getAllByRole('tab').map(tab => tab.textContent)).toEqual(['All Users', 'Pending Users'])
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
