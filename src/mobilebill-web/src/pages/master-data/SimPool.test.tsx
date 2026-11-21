import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

// Signed in as an IT Engineer (SIM Pool actions are for Administrator and IT Engineer).
vi.mock('../../auth/AuthContext', async () => (await import('../../test/roleAuthMock')).roleAuthMock(() => 'ITEngineer'))

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
        { id: 'p1', mobileNumber: '0770000001', previousEpf: 'EPF-1', previousEmployeeName: 'Old Holder', previousNameWithInitials: 'O. HOLDER', previousCallingName: 'OLDIE', factory: 'F', department: 'D', pooledOn: '2026-07-01', reason: 'Resigned', daysInPool: 96, isLongIdle: true, monthlyCreditLimit: 1000, monthlyRental: 100 },
        { id: 'p2', mobileNumber: '0770000002', previousEpf: 'EPF-2', previousEmployeeName: 'Recent Leaver', factory: 'F', department: 'D', pooledOn: '2026-09-20', reason: 'Resigned', daysInPool: 15, isLongIdle: false, monthlyCreditLimit: 1000, monthlyRental: 100 },
      ])
      return page([])
    })
    render(<MobileAllocationsPage />)

    fireEvent.click(screen.getByRole('tab', { name: 'SIM Pool' }))
    expect(await screen.findByText('96 days · over 60')).toBeTruthy()
    expect(screen.getByText('15 days')).toBeTruthy()
    expect(screen.getByText(/1 has been idle for more than 60 days/)).toBeTruthy()
    // EPF in its own column; the previous holder with initials and the calling name in brackets, or the full name when there are no initials.
    expect(screen.getByRole('columnheader', { name: 'EPF' })).toBeTruthy()
    expect(screen.getByRole('cell', { name: 'EPF-1' })).toBeTruthy()
    expect(screen.getByRole('cell', { name: 'O. HOLDER (OLDIE)' })).toBeTruthy()
    expect(screen.getByRole('cell', { name: 'Recent Leaver' })).toBeTruthy()
  })

  it('assigns a pooled number to a new holder from the SIM Pool tab, then reloads the pool', async () => {
    const posted: Array<{ url: string; body: unknown }> = []
    let poolLoads = 0
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input)
      if (init?.method === 'POST') { posted.push({ url, body: JSON.parse(String(init.body)) }); return json({ ...allocation, id: 'p1', status: 'Assigned' }) }
      if (url.includes('/api/mobile-accounts/pool')) return json(poolLoads++ === 0
        ? [{ id: 'p1', mobileNumber: '0770000001', previousEpf: 'EPF-1', previousEmployeeName: 'Old Holder', factory: 'F', department: 'D', pooledOn: '2026-07-01', reason: 'Resigned', daysInPool: 96, isLongIdle: true, monthlyCreditLimit: 1000, monthlyRental: 100 }]
        : [])
      if (url.includes('/api/employees')) return page([{ id: 'e2', epf: 'EPF-2', fullName: 'Joiner', factoryName: 'Head Office', isActive: true }])
      if (url.includes('/api/mobile-packages')) return page([{ id: 'pk1', code: 'PKG-500', description: 'Basic', providerName: 'Dialog', monthlyRental: 500, defaultCreditLimit: 1500, isActive: true }])
      return page([])
    })
    render(<MobileAllocationsPage />)

    fireEvent.click(screen.getByRole('tab', { name: 'SIM Pool' }))
    // Assign comes before Disconnect in the row.
    const row = (await screen.findByRole('cell', { name: '0770000001' })).closest('tr')!
    expect(within(row).getAllByRole('button').map(button => button.textContent)).toEqual(['Assign', 'Disconnect'])
    fireEvent.click(within(row).getByRole('button', { name: 'Assign' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Assign 0770000001 to a new holder')).toBeTruthy()
    // The amounts start as the pooled SIM's; choosing a package fills in its rental.
    expect((within(dialog).getByRole('spinbutton', { name: 'Monthly Credit Limit' }) as HTMLInputElement).value).toBe('1000')
    fireEvent.change(within(dialog).getByRole('combobox', { name: 'New holder' }), { target: { value: 'Joiner' } })
    fireEvent.click(await screen.findByRole('option', { name: 'EPF-2 — Joiner — Head Office' }))
    fireEvent.change(within(dialog).getByRole('combobox', { name: 'Package' }), { target: { value: 'PKG' } })
    fireEvent.click(await screen.findByRole('option', { name: /PKG-500/ }))
    expect((within(dialog).getByRole('spinbutton', { name: 'Monthly Rental' }) as HTMLInputElement).value).toBe('500')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Assign' }))

    await waitFor(() => expect(posted).toEqual([{ url: expect.stringContaining('/api/mobile-accounts/p1/assign-from-pool'), body: { employeeId: 'e2', monthlyCreditLimit: 1000, monthlyRental: 500, packageId: 'pk1' } }]))
    expect(await screen.findByText('The SIM Pool is empty. Every active number has a holder.')).toBeTruthy()
  })

  it('asks for the new holder before assigning a pooled number', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      if (url.includes('/api/mobile-accounts/pool')) return json([{ id: 'p1', mobileNumber: '0770000001', previousEpf: 'EPF-1', previousEmployeeName: 'Old Holder', factory: 'F', department: 'D', pooledOn: '2026-07-01', reason: 'Resigned', daysInPool: 10, isLongIdle: false, monthlyCreditLimit: 1000, monthlyRental: 100 }])
      return page([])
    })
    render(<MobileAllocationsPage />)

    fireEvent.click(screen.getByRole('tab', { name: 'SIM Pool' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Assign' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByRole('button', { name: 'Assign' }))

    expect(await within(dialog).findByText('New holder is required.')).toBeTruthy()
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)
  })

  it('disconnects a pooled number from the SIM Pool tab with the date and reason, then reloads the pool', async () => {
    const posted: Array<{ url: string; body: unknown }> = []
    let poolLoads = 0
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input)
      if (init?.method === 'POST') { posted.push({ url, body: JSON.parse(String(init.body)) }); return json({ ...allocation, id: 'p1', status: 'Disconnected' }) }
      if (url.includes('/api/mobile-accounts/pool')) return json(poolLoads++ === 0
        ? [{ id: 'p1', mobileNumber: '0770000001', previousEpf: 'EPF-1', previousEmployeeName: 'Old Holder', factory: 'F', department: 'D', pooledOn: '2026-07-01', reason: 'Resigned', daysInPool: 96, isLongIdle: true, monthlyCreditLimit: 1000, monthlyRental: 100 }]
        : [])
      return page([])
    })
    render(<MobileAllocationsPage />)

    fireEvent.click(screen.getByRole('tab', { name: 'SIM Pool' }))
    fireEvent.click(await screen.findByRole('button', { name: 'Disconnect' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Disconnect 0770000001')).toBeTruthy()
    // The reason starts as the SIM Pool reason.
    expect((within(dialog).getByLabelText(/Reason/) as HTMLInputElement).value).toBe('Resigned')
    fireEvent.change(within(dialog).getByLabelText(/Disconnection date/), { target: { value: '2026-11-05' } })
    fireEvent.change(within(dialog).getByLabelText(/Reason/), { target: { value: 'No longer needed' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Disconnect' }))

    await waitFor(() => expect(posted).toEqual([{ url: expect.stringContaining('/api/mobile-accounts/p1/disconnect'), body: { disconnectedOn: '2026-11-05', reason: 'No longer needed' } }]))
    expect(await screen.findByText('The SIM Pool is empty. Every active number has a holder.')).toBeTruthy()
  })

  it('lists disconnected numbers with the reason, read only', async () => {
    const requested: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      requested.push(url)
      if (url.includes('/api/mobile-accounts?') && url.includes('status=Disconnected')) return page([
        { ...allocation, id: 'd1', mobileNumber: '0779999999', nameWithInitials: 'A. B. LEAVER', callingName: 'LEE', status: 'Disconnected', disconnectedOn: '2026-09-15', statusReason: 'Lost phone', isActive: false },
      ])
      return page([])
    })
    render(<MobileAllocationsPage />)

    fireEvent.click(screen.getByRole('tab', { name: 'Disconnected' }))
    expect(await screen.findByRole('cell', { name: 'Lost phone' })).toBeTruthy()
    expect(screen.getByRole('columnheader', { name: 'Reason for Disconnection' })).toBeTruthy()
    expect(screen.getByRole('cell', { name: '2026-09-15' })).toBeTruthy()
    expect(screen.getByRole('cell', { name: 'A. B. LEAVER (LEE)' })).toBeTruthy()
    // A view only: no create, edit, import, row actions or Active/Inactive filter.
    const panel = screen.getByRole('tabpanel')
    expect(within(panel).queryByRole('button', { name: /Create|Edit|Import|Disconnect|Deactivate/ })).toBeNull()
    expect(within(panel).queryByRole('combobox', { name: 'Status' })).toBeNull()
    expect(requested.some(url => url.includes('/api/mobile-accounts?') && url.includes('status=Disconnected'))).toBe(true)
  })

  it('downloads the allocations Excel report', async () => {
    const requested: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = String(input)
      requested.push(url)
      if (url.includes('/api/mobile-accounts/export/excel')) return new Response(new Blob(['xlsx']), { status: 200, headers: { 'Content-Disposition': 'attachment; filename="Mobile_Allocations_2026_11_06.xlsx"' } })
      return page([])
    })
    const createObjectUrl = vi.fn(() => 'blob:report')
    Object.assign(URL, { createObjectURL: createObjectUrl, revokeObjectURL: vi.fn() })
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
    render(<MobileAllocationsPage />)

    fireEvent.click(screen.getByRole('button', { name: 'Download Excel' }))

    await waitFor(() => expect(click).toHaveBeenCalled())
    expect(requested.some(url => url.includes('/api/mobile-accounts/export/excel'))).toBe(true)
    expect((click.mock.contexts[0] as HTMLAnchorElement).download).toBe('Mobile_Allocations_2026_11_06.xlsx')
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
