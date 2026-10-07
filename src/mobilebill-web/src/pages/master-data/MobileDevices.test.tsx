import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

// Signed in as an IT Engineer unless a test says otherwise.
const signedIn = vi.hoisted(() => ({ role: 'ITEngineer' }))
vi.mock('../../auth/AuthContext', () => ({ useCanEditMasterData: (roles: readonly string[] = ['Administrator', 'ITEngineer']) => roles.includes(signedIn.role) }))

import { MobileDevicesPage } from './MasterPages'

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const page = (items: unknown[]) => json({ items, pageNumber: 1, pageSize: 100, totalCount: items.length, totalPages: items.length ? 1 : 0 })
const device = (id: string, assetTag: string, status: string, extra: Record<string, unknown> = {}) => ({
  id, assetTag, imei1: '356789010000014', imei2: null, brand: 'Samsung', model: 'Galaxy A35', serialNumber: null, purchaseDate: '2025-10-07', purchaseCost: 85000,
  warrantyUntil: null, supplier: null, notes: null, status, statusSince: '2026-10-01', daysInStatus: 6, holderEmployeeId: null, holderEpf: null, holderName: null,
  holderFactory: null, holderDepartment: null, isActive: true, currentValue: 63750, ...extra,
})
const issued = device('d1', 'GL-MOB-0001', 'Issued', { holderEmployeeId: 'e1', holderEpf: 'EPF-1', holderName: 'Holder', holderFactory: 'Head Office' })
const spare = device('d2', 'GL-MOB-0002', 'InStock')
const repair = device('d3', 'GL-MOB-0003', 'UnderRepair')

function mockApi(posted: Array<{ url: string; body: unknown }> = []) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = new URL(String(input), 'http://localhost')
    if (init?.method === 'POST') { posted.push({ url: url.pathname, body: JSON.parse(String(init.body)) }); return json(issued) }
    if (url.pathname === '/api/mobile-devices/in-stock') return page([spare])
    if (url.pathname === '/api/mobile-devices/to-collect') return json([
      { deviceId: 'd9', assetTag: 'GL-MOB-0009', brand: 'Apple', model: 'iPhone 13', imei1: '356789010000097', employeeId: 'e9', epf: 'EPF-9', employeeName: 'Leaver', factory: 'Head Office', department: 'IT', resignedOn: '2026-09-15', returnPendingSince: '2026-09-15', daysWaiting: 22, isOverdue: true, purchaseCost: 120000, recoverableAmount: 60000 },
    ])
    if (url.pathname === '/api/mobile-devices') return page([issued, spare, repair])
    if (url.pathname === '/api/employees') return page([{ id: 'e2', epf: 'EPF-2', fullName: 'Joiner', factoryName: 'Head Office', isActive: true }])
    return page([])
  })
}

async function choose(fieldName: string, optionName: string | RegExp) {
  fireEvent.mouseDown(await screen.findByRole('combobox', { name: fieldName }))
  fireEvent.click(await screen.findByRole('option', { name: optionName }))
}

describe('Mobile devices', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks(); signedIn.role = 'ITEngineer' })

  it('lists devices with their status and holder, and offers the actions that fit each status', async () => {
    mockApi(); render(<MobileDevicesPage />)

    const table = await screen.findByRole('table')
    const rows = within(table).getAllByRole('row')
    expect(rows[1].textContent).toContain('EPF-1 — Holder')
    expect(within(rows[1]).getByText('Issued')).toBeTruthy()
    expect(within(rows[1]).getByRole('button', { name: 'Return' })).toBeTruthy()
    expect(within(rows[1]).getByRole('button', { name: 'Replace' })).toBeTruthy()
    expect(within(rows[1]).queryByRole('button', { name: 'Issue' })).toBeNull()
    expect(within(rows[2]).getByRole('button', { name: 'Issue' })).toBeTruthy()
    expect(within(rows[3]).getByText('Under Repair · 6 days')).toBeTruthy()
    expect(within(rows[3]).getByRole('button', { name: 'Back In Stock' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Deactivate' })).toBeNull()
  })

  it('issues an in-stock device to an employee', async () => {
    const posted: Array<{ url: string; body: unknown }> = []
    mockApi(posted); render(<MobileDevicesPage />)

    const row = within(await screen.findByRole('table')).getAllByRole('row')[2]
    fireEvent.click(within(row).getByRole('button', { name: 'Issue' }))
    fireEvent.change(await screen.findByRole('combobox', { name: 'Employee' }), { target: { value: 'Joiner' } })
    fireEvent.click(await screen.findByRole('option', { name: 'EPF-2 — Joiner (Head Office)' }))
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Issue' }))

    await waitFor(() => expect(posted).toEqual([{ url: '/api/mobile-devices/d2/issue', body: { employeeId: 'e2', issuedOn: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/), notes: null } }]))
  })

  it('replaces a damaged device with one from stock and can charge the employee', async () => {
    const posted: Array<{ url: string; body: unknown }> = []
    mockApi(posted); render(<MobileDevicesPage />)

    const row = within(await screen.findByRole('table')).getAllByRole('row')[1]
    fireEvent.click(within(row).getByRole('button', { name: 'Replace' }))
    fireEvent.change(await screen.findByRole('combobox', { name: 'New device (In Stock)' }), { target: { value: 'GL' } })
    fireEvent.click(await screen.findByRole('option', { name: 'GL-MOB-0002 — Samsung Galaxy A35' }))
    fireEvent.change(screen.getByRole('textbox', { name: 'What happened' }), { target: { value: 'Screen broken' } })
    await choose('Charge the employee for the old device?', /^Yes/)
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Replace' }))

    await waitFor(() => expect(posted).toEqual([{ url: '/api/mobile-devices/d1/replace', body: { newDeviceId: 'd2', replacedOn: expect.any(String), oldCondition: 'Damaged', notes: 'Screen broken', chargeEmployee: true } }]))
  })

  it('returns a device with its condition and reason', async () => {
    const posted: Array<{ url: string; body: unknown }> = []
    mockApi(posted); render(<MobileDevicesPage />)

    const row = within(await screen.findByRole('table')).getAllByRole('row')[1]
    fireEvent.click(within(row).getByRole('button', { name: 'Return' }))
    await choose('Condition', 'Needs repair')
    await choose('Reason', 'Upgrade')
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Return' }))

    await waitFor(() => expect(posted).toEqual([{ url: '/api/mobile-devices/d1/return', body: { returnedOn: expect.any(String), condition: 'UnderRepair', reason: 'Upgrade', notes: null, chargeEmployee: false } }]))
  })

  it('lists devices to collect from leavers with the overdue recoverable value', async () => {
    mockApi(); render(<MobileDevicesPage />)

    fireEvent.click(screen.getByRole('tab', { name: 'To Collect' }))
    const table = await screen.findByRole('table', { name: 'Devices to collect' })
    expect(within(table).getByText('EPF-9 — Leaver')).toBeTruthy()
    expect(within(table).getByText('22 days · overdue')).toBeTruthy()
    expect(screen.getByText(/1 is overdue \(more than 14 days\)/)).toBeTruthy()
  })

  it('is view-only for HR and Finance users', async () => {
    signedIn.role = 'HrUser'
    mockApi(); render(<MobileDevicesPage />)

    await screen.findByRole('table')
    for (const name of ['Create', 'Edit', 'Issue', 'Return', 'Replace', 'Mark Lost', 'Retire', 'Back In Stock'])
      expect(screen.queryByRole('button', { name })).toBeNull()
    expect(screen.getByRole('button', { name: 'Device Register Excel' })).toBeTruthy()
  })
})
