import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { PermissionAction, PermissionModule } from '../../api/permissionsApi'

// Permissions saved in User Management decide each button separately, whatever the role's default.
const granted = vi.hoisted(() => ({ list: [] as string[] }))
vi.mock('../../auth/AuthContext', async () => {
  const base = (await import('../../test/roleAuthMock')).roleAuthMock(() => 'Viewer')
  const can = (module: PermissionModule, action: PermissionAction = 'View') => granted.list.includes(`${module}:${action}`)
  return { ...base, usePermission: can, usePermissions: () => can }
})

import { FactoriesPage, MobileAllocationsPage } from './MasterPages'

const page = (items: unknown[]) => new Response(JSON.stringify({ items, pageNumber: 1, pageSize: 20, totalCount: items.length, totalPages: 1 }), { status: 200, headers: { 'Content-Type': 'application/json' } })

describe('Master data screens follow the saved permissions', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks(); granted.list = [] })

  it('shows only Create when the role may create but not edit or deactivate', async () => {
    granted.list = ['factories:View', 'factories:Create']
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(page([{ id: 'f1', code: 'F01', name: 'Factory One', isActive: true }]))
    render(<FactoriesPage />)

    expect(await screen.findByText('Factory One')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Create' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Edit' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Deactivate' })).toBeNull()
  })

  it('shows Deactivate without Edit when only Delete is granted', async () => {
    granted.list = ['factories:View', 'factories:Delete']
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(page([{ id: 'f1', code: 'F01', name: 'Factory One', isActive: true }]))
    render(<FactoriesPage />)

    expect(await screen.findByText('Factory One')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Deactivate' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Create' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Edit' })).toBeNull()
  })

  it('splits mobile allocation actions: Edit covers reassign and the pool, Delete covers disconnect, import needs Create and Edit', async () => {
    granted.list = ['mobile-allocations:View', 'mobile-allocations:Edit']
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const path = new URL(String(input), 'http://localhost').pathname
      if (path === '/api/employees') return page([])
      return page([{ id: 'm1', mobileNumber: '0771234567', epf: 'EPF-1', employeeName: 'First Employee', monthlyCreditLimit: 1500, monthlyRental: 700, factory: 'F', department: 'D', isActive: true, status: 'Assigned' }])
    })
    render(<MobileAllocationsPage />)

    expect(await screen.findByText('0771234567')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Edit' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Release to Pool' })).toBeTruthy()
    for (const name of ['Create', 'Import', 'Deactivate', 'Disconnect'])
      expect(screen.queryByRole('button', { name })).toBeNull()
  })
})
