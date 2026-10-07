import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

// Signed in as an HR User: allowed only where the page's role list includes HrUser.
vi.mock('../../auth/AuthContext', () => ({ useCanEditMasterData: (roles: readonly string[] = ['Administrator', 'ITEngineer']) => roles.includes('HrUser') }))

import { FactoriesPage, MobileAllocationsPage, ProvidersPage } from './MasterPages'

const page = (items: unknown[]) => new Response(JSON.stringify({ items, pageNumber: 1, pageSize: 20, totalCount: items.length, totalPages: 1 }), { status: 200, headers: { 'Content-Type': 'application/json' } })

describe('HR and Finance user master-data access', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('can create and edit organisation masters', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(page([{ id: 'f1', code: 'F01', name: 'Factory One', isActive: true }]))
    render(<FactoriesPage />)
    expect(await screen.findByText('Factory One')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Create' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Edit' })).toBeTruthy()
  })

  it('sees telecom providers read-only', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(page([{ id: 'p1', code: 'DIALOG', name: 'Dialog', isActive: true }]))
    render(<ProvidersPage />)
    expect(await screen.findByText('Dialog')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Create' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Edit' })).toBeNull()
  })

  it('sees mobile allocations view-only, with no actions on assigned or pooled numbers', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const path = new URL(String(input), 'http://localhost').pathname
      if (path === '/api/employees') return page([{ id: 'e1', epf: 'EPF-1', fullName: 'First Employee', isActive: true }])
      return page([
        { id: 'm1', mobileNumber: '0771234567', epf: 'EPF-1', employeeName: 'First Employee', monthlyCreditLimit: 1500, monthlyRental: 700, factory: 'F', department: 'D', isActive: true, status: 'Assigned' },
        { id: 'm2', mobileNumber: '0771234568', epf: 'EPF-2', employeeName: 'Leaver', monthlyCreditLimit: 1500, monthlyRental: 700, factory: 'F', department: 'D', isActive: true, status: 'Pooled', pooledOn: '2026-09-01' },
      ])
    })
    render(<MobileAllocationsPage />)

    expect(await screen.findByText('0771234567')).toBeTruthy()
    expect(screen.getByText('0771234568')).toBeTruthy()
    for (const name of ['Create', 'Import', 'Edit', 'Reassign', 'Deactivate', 'Release to Pool', 'Assign from Pool', 'Disconnect'])
      expect(screen.queryByRole('button', { name })).toBeNull()
  })
})
