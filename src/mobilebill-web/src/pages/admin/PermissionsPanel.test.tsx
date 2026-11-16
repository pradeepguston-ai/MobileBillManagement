import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { AuthProvider } from '../../auth/AuthContext'
import { PermissionsPanel } from './PermissionsPanel'

const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
const modules = [
  { key: 'dashboard', label: 'Dashboard', group: 'General', note: null, actions: ['View'] },
  { key: 'employees', label: 'Employees', group: 'Masters', note: 'Edit includes resignations.', actions: ['View', 'Create', 'Edit', 'Delete'] },
]
const full = (module: string) => ({ module, canView: true, canCreate: module !== 'dashboard', canEdit: module !== 'dashboard', canDelete: module !== 'dashboard' })
const matrix = {
  modules,
  roles: [
    { role: 'Administrator', locked: true, customised: false, permissions: [full('dashboard'), full('employees')] },
    { role: 'ITEngineer', locked: false, customised: false, permissions: [full('dashboard'), full('employees')] },
    { role: 'HrUser', locked: false, customised: false, permissions: [full('dashboard'), { module: 'employees', canView: true, canCreate: true, canEdit: true, canDelete: false }] },
  ],
}

function mockApi() {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = String(input)
    if (url.endsWith('/api/permissions/roles/HrUser') && init?.method === 'PUT') {
      const body = JSON.parse(String(init.body)) as { permissions: unknown[] }
      return json({ role: 'HrUser', locked: false, customised: true, permissions: body.permissions })
    }
    if (url.endsWith('/api/permissions/roles/HrUser/reset')) return json(matrix.roles[2])
    return json(matrix)
  })
}

const box = (name: string) => screen.getByRole('checkbox', { name }) as HTMLInputElement

async function chooseRole(label: string) {
  fireEvent.click(await screen.findByRole('radio', { name: label }))
}

describe('Permissions tab', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('lists each screen with View, Create, Edit and Deactivate, and marks actions that do not apply', async () => {
    mockApi(); render(<PermissionsPanel />)

    const region = await screen.findByRole('region', { name: 'Permissions for IT Engineer' })
    expect(within(region).getByText('Masters')).toBeTruthy()
    expect(within(region).getByText('Edit includes resignations.')).toBeTruthy()
    expect(box('Delete Employees').checked).toBe(true)
    expect(within(screen.getByRole('group', { name: 'Dashboard' })).getAllByRole('img', { name: 'Not applicable' })).toHaveLength(3)
    expect(screen.getByRole('radio', { name: 'IT Engineer' }).getAttribute('aria-checked')).toBe('true')
  })

  it('keeps the Administrator role locked with full access', async () => {
    mockApi(); render(<PermissionsPanel />)
    await chooseRole('Administrator')

    expect(await screen.findByText(/always has full access/)).toBeTruthy()
    expect(box('Edit Employees').disabled).toBe(true)
    expect(screen.queryByRole('button', { name: /Full access for/ })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Reset to Defaults' })).toBeNull()
  })

  it('ticks View with any change, clears changes with View, and saves the role', async () => {
    const fetchMock = mockApi(); render(<PermissionsPanel />)
    await chooseRole('HR User')
    await screen.findByRole('region', { name: 'Permissions for HR User' })
    expect(screen.queryByRole('button', { name: 'Save Changes' })).toBeNull()

    fireEvent.click(box('View Employees'))
    expect([box('Create Employees').checked, box('Edit Employees').checked]).toEqual([false, false])
    fireEvent.click(box('Delete Employees'))
    expect([box('View Employees').checked, box('Delete Employees').checked]).toEqual([true, true])
    expect(screen.getByText('Unsaved changes for HR User')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Save Changes' }))
    expect(await screen.findByText(/Permissions saved for HR User/)).toBeTruthy()
    const put = fetchMock.mock.calls.find(([, init]) => init?.method === 'PUT')!
    expect(JSON.parse(String(put[1]!.body)).permissions).toContainEqual({ module: 'employees', canView: true, canCreate: false, canEdit: false, canDelete: true })
    expect(screen.getByText('Customised')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Save Changes' })).toBeNull()
  })

  it('sets a whole group to view only or no access at once', async () => {
    mockApi(); render(<PermissionsPanel />)
    await screen.findByRole('region', { name: 'Permissions for IT Engineer' })

    fireEvent.click(screen.getByRole('button', { name: 'View only for Masters' }))
    expect([box('View Employees').checked, box('Edit Employees').checked, box('Delete Employees').checked]).toEqual([true, false, false])
    fireEvent.click(screen.getByRole('button', { name: 'No access for Masters' }))
    expect(box('View Employees').checked).toBe(false)
    fireEvent.click(screen.getByRole('button', { name: 'Discard' }))
    expect(box('Delete Employees').checked).toBe(true)
  })

  it('asks before switching roles with unsaved changes', async () => {
    mockApi(); render(<PermissionsPanel />)
    await screen.findByRole('region', { name: 'Permissions for IT Engineer' })
    fireEvent.click(box('Delete Employees'))

    await chooseRole('HR User')
    const dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Discard' }))
    await waitFor(() => expect(screen.getByRole('region', { name: 'Permissions for HR User' })).toBeTruthy())
  })
})

describe('Permissions tab for a role given Role Permissions', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks(); localStorage.clear() })

  const adminModules = [
    ...modules,
    { key: 'user-management', label: 'User Management & Password Resets', group: 'Administration', note: null, actions: ['View', 'Create', 'Edit', 'Delete'], administratorOnly: true },
  ]
  const none = (module: string) => ({ module, canView: false, canCreate: false, canEdit: false, canDelete: false })
  const roles = ['ITEngineer', 'HeadOfIt'].map(role => ({ role, locked: false, customised: false, permissions: [full('dashboard'), full('employees'), none('user-management')] }))

  function signInAsHeadOfIt(canEdit: boolean) {
    localStorage.setItem('mobilebill.token', 'test-token')
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/api/auth/me')) return json({ id: 'head-1', email: 'head@example.com', displayName: 'Head', role: 'HeadOfIt', status: 'Active' })
      if (url.includes('/api/permissions/mine')) return json([{ module: 'role-permissions', canView: true, canCreate: false, canEdit, canDelete: false }])
      return json({ modules: adminModules, roles })
    })
    render(<AuthProvider><PermissionsPanel /></AuthProvider>)
  }

  it('changes other roles but leaves the Administration screens to Administrators', async () => {
    signInAsHeadOfIt(true)
    await screen.findByRole('region', { name: 'Permissions for IT Engineer' })

    await waitFor(() => expect(box('Create Employees').disabled).toBe(false))
    expect(box('View User Management & Password Resets').disabled).toBe(true)
    expect(screen.getByText('Set by Administrators')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Full access for Masters' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Full access for Administration' })).toBeNull()

    fireEvent.click(box('Delete Employees'))
    expect(screen.getByRole('button', { name: 'Save Changes' })).toBeTruthy()
  })

  it('cannot change its own role', async () => {
    signInAsHeadOfIt(true)
    await screen.findByRole('region', { name: 'Permissions for IT Engineer' })
    await waitFor(() => expect(box('Create Employees').disabled).toBe(false))

    await chooseRole('Head of IT')
    expect(await screen.findByText('This is your own role. Only an Administrator can change its permissions.')).toBeTruthy()
    expect(box('Create Employees').disabled).toBe(true)
    expect(screen.queryByRole('button', { name: 'Reset to Defaults' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Full access for Masters' })).toBeNull()
  })

  it('only looks with View', async () => {
    signInAsHeadOfIt(false)
    expect(await screen.findByText('You can view these permissions but not change them.')).toBeTruthy()
    expect(box('Create Employees').disabled).toBe(true)
    expect(screen.queryByRole('button', { name: 'Reset to Defaults' })).toBeNull()
  })
})
