import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

// Signed in as an IT Engineer (SIM Pool actions are for Administrator and IT Engineer).
vi.mock('../../auth/AuthContext', () => ({ useCanEditMasterData: (roles: readonly string[] = ['Administrator', 'ITEngineer']) => roles.includes('ITEngineer') }))

import { EmployeesPage, MobileAllocationsPage } from './MasterPages'

const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
const page = (items: unknown[]) => json({ items, pageNumber: 1, pageSize: 20, totalCount: items.length, totalPages: 1 })
const allocation = { id: 'm1', mobileNumber: '0771234567', epf: 'EPF-1', employeeName: 'Leaver', monthlyCreditLimit: 1500, monthlyRental: 700, factory: 'F', department: 'D', isActive: true }

describe('SIM Pool', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('releases an assigned number to the pool with the resignation date', async () => {
    const posted: Array<{ url: string; body: unknown }> = []
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input)
      if (init?.method === 'POST') { posted.push({ url, body: JSON.parse(String(init.body)) }); return json({ ...allocation, status: 'Pooled' }) }
      if (url.includes('/api/employees')) return page([])
      return page([{ ...allocation, status: 'Assigned' }])
    })
    render(<MobileAllocationsPage />)

    expect(await screen.findByText('0771234567')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Assign from Pool' })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Release to Pool' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.change(within(dialog).getByLabelText(/Resignation date/), { target: { value: '2026-10-03' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Release to Pool' }))

    await waitFor(() => expect(posted).toEqual([{ url: expect.stringContaining('/api/mobile-accounts/m1/release-to-pool'), body: { resignedOn: '2026-10-03', reason: 'Resigned' } }]))
  })

  it('assigns a pooled number starting from its existing amounts, which can be changed', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/api/employees')) return page([{ id: 'e2', epf: 'EPF-2', fullName: 'Joiner', isActive: true }])
      return page([{ ...allocation, status: 'Pooled', pooledOn: '2026-08-01' }])
    })
    render(<MobileAllocationsPage />)

    expect(await screen.findByText('SIM Pool since 2026-08-01')).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Release to Pool' })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Assign from Pool' }))
    const dialog = await screen.findByRole('dialog')
    expect((within(dialog).getByRole('spinbutton', { name: 'Monthly Credit Limit' }) as HTMLInputElement).disabled).toBe(false)
    expect((within(dialog).getByRole('spinbutton', { name: 'Monthly Credit Limit' }) as HTMLInputElement).value).toBe('1500')
  })

  it('shows days in pool and flags numbers idle over 60 days', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      if (String(input).includes('/api/mobile-accounts/pool')) return json([
        { id: 'p1', mobileNumber: '0770000001', previousEpf: 'EPF-1', previousEmployeeName: 'Old Holder', factory: 'F', department: 'D', pooledOn: '2026-07-01', reason: 'Resigned', daysInPool: 96, isLongIdle: true, monthlyCreditLimit: 1000, monthlyRental: 100 },
        { id: 'p2', mobileNumber: '0770000002', previousEpf: 'EPF-2', previousEmployeeName: 'Recent Leaver', factory: 'F', department: 'D', pooledOn: '2026-09-20', reason: 'Resigned', daysInPool: 15, isLongIdle: false, monthlyCreditLimit: 1000, monthlyRental: 100 },
      ])
      return page([])
    })
    render(<MobileAllocationsPage />)

    fireEvent.click(screen.getByRole('tab', { name: 'SIM Pool' }))
    expect(await screen.findByText('96 days · over 60')).toBeTruthy()
    expect(screen.getByText('15 days')).toBeTruthy()
    expect(screen.getByText(/1 has been idle for more than 60 days/)).toBeTruthy()
  })

  it('resigns an employee from the employee list', async () => {
    const posted: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      if (init?.method === 'POST') { posted.push(String(input)); return json([]) }
      if (String(input).includes('/api/employees')) return page([{ id: 'e1', epf: 'EPF-1', fullName: 'Leaver', isActive: true }])
      return page([])
    })
    render(<EmployeesPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Resign' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Resign' }))

    await waitFor(() => expect(posted).toEqual([expect.stringContaining('/api/employees/e1/resign')]))
  })
})
