import { cleanup, fireEvent, render, screen } from '@testing-library/react'
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

  it('can only reassign a mobile allocation to another employee', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const path = new URL(String(input), 'http://localhost').pathname
      if (path === '/api/employees') return page([{ id: 'e1', epf: 'EPF-1', fullName: 'First Employee', isActive: true }])
      return page([{ id: 'm1', mobileNumber: '0771234567', epf: 'EPF-1', employeeName: 'First Employee', monthlyCreditLimit: 1500, monthlyRental: 700, factory: 'F', department: 'D', isActive: true }])
    })
    render(<MobileAllocationsPage />)

    expect(await screen.findByText('0771234567')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Create' })).toBeNull()
    expect(screen.queryByRole('button', { name: 'Deactivate' })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Reassign' }))

    expect(await screen.findByText('Reassign Mobile Number')).toBeTruthy()
    expect((screen.getByRole('textbox', { name: 'Mobile Number' }) as HTMLInputElement).disabled).toBe(true)
    expect((screen.getByRole('spinbutton', { name: 'Monthly Credit Limit' }) as HTMLInputElement).disabled).toBe(true)
    expect((screen.getByRole('spinbutton', { name: 'Monthly Rental' }) as HTMLInputElement).disabled).toBe(true)
    expect((screen.getByRole('combobox', { name: 'Employee' }) as HTMLInputElement).disabled).toBe(false)
  })
})
