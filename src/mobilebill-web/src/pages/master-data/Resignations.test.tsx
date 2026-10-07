import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

// Signed in as an HR User.
vi.mock('../../auth/AuthContext', () => ({ useCanEditMasterData: (roles: readonly string[] = ['Administrator', 'ITEngineer']) => roles.includes('HrUser') }))

import { EmployeesPage } from './MasterPages'

const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
const page = (items: unknown[]) => json({ items, pageNumber: 1, pageSize: 20, totalCount: items.length, totalPages: 1 })
const pending = { id: 'e1', epf: 'EPF-1', fullName: 'Serving Notice', callingName: 'Sam', factory: 'Factory A', department: 'IT', section: null, resignedOn: '2026-10-31', reason: 'Notice given', daysLeft: 3, mobileNumbers: ['0771000001', '0771000002'] }
const resigned = { id: 'e2', epf: 'EPF-2', fullName: 'Gone', callingName: null, factory: 'Factory A', department: 'HR', section: 'Payroll', resignedOn: '2026-09-30', reason: 'Resigned', daysLeft: 0, mobileNumbers: ['0771000009'] }

describe('Employee resignations', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('marks an employee serving notice as Leaving on the employee list', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => String(input).includes('/api/employees')
      ? page([{ id: 'e1', epf: 'EPF-1', fullName: 'Serving Notice', isActive: true, resignedOn: '2026-10-31' }, { id: 'e2', epf: 'EPF-2', fullName: 'Gone', isActive: false, resignedOn: '2026-09-30' }, { id: 'e3', epf: 'EPF-3', fullName: 'Stays', isActive: true, resignedOn: null }])
      : page([]))
    render(<EmployeesPage />)

    expect(await screen.findByText('Leaving 2026-10-31')).toBeTruthy()
    const table = screen.getByRole('table')
    expect(within(table).getByText('Resigned')).toBeTruthy()
    expect(within(table).getByText('Active')).toBeTruthy()
  })

  it('lists pending resignations with days left and numbers held, and cancels one', async () => {
    const posted: string[] = []
    let cancelled = false
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input)
      if (init?.method === 'POST') { posted.push(url); cancelled = true; return new Response(null, { status: 204 }) }
      if (url.includes('/api/employees/resignations?status=Pending')) return json(cancelled ? [] : [pending])
      return page([])
    })
    render(<EmployeesPage />)

    fireEvent.click(screen.getByRole('tab', { name: 'Pending Resignation' }))
    const table = await screen.findByRole('table', { name: 'Pending resignations' })
    expect(within(table).getByText('Serving Notice')).toBeTruthy()
    expect(within(table).getByText('3 days')).toBeTruthy()
    expect(within(table).getByText('0771000001, 0771000002')).toBeTruthy()

    fireEvent.click(within(table).getByRole('button', { name: 'Cancel Resignation' }))
    fireEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancel Resignation' }))

    await waitFor(() => expect(posted).toEqual([expect.stringContaining('/api/employees/e1/cancel-resignation')]))
    expect(await screen.findByText(/No pending resignations/)).toBeTruthy()
  })

  it('lists resigned employees with their numbers still in the SIM Pool', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => String(input).includes('/api/employees/resignations?status=Resigned') ? json([resigned]) : page([]))
    render(<EmployeesPage />)

    fireEvent.click(screen.getByRole('tab', { name: 'Resigned' }))
    const table = await screen.findByRole('table', { name: 'Resigned employees' })
    expect(within(table).getByText('Gone')).toBeTruthy()
    expect(within(table).getByText('2026-09-30')).toBeTruthy()
    expect(within(table).getByText('0771000009')).toBeTruthy()
    expect(within(table).queryByRole('button')).toBeNull()
  })
})
