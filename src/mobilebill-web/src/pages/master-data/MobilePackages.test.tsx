import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { MobileAllocationsPage, MobilePackagesPage } from './MasterPages'

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const page = (items: unknown[]) => ({ items, pageNumber: 1, pageSize: 100, totalCount: items.length, totalPages: items.length ? 1 : 0 })
const ppu = { id: 'pkg-1', code: 'PPU23_700', providerId: 'prov-1', providerName: 'Dialog', description: 'Unlimited any network calls & 5 GB data per month', monthlyRental: 700, totalWithTax: 940, defaultCreditLimit: 1000, isActive: true, allocationCount: 279, allocationsWithOtherRental: 0 }

async function selectOption(fieldName: string, search: string, optionName: string) {
  const input = await screen.findByRole('combobox', { name: fieldName })
  fireEvent.change(input, { target: { value: search } })
  fireEvent.click(await screen.findByRole('option', { name: optionName }))
}

describe('Mobile packages', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('creates a package with Dialog as the starting provider, its rental, total with tax and default credit limit', async () => {
    let submitted: unknown
    let providersLoaded = false
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = new URL(String(input))
      if (url.pathname === '/api/mobile-packages' && init?.method === 'POST') { submitted = JSON.parse(String(init.body)); return json({ id: 'pkg-1' }, 201) }
      if (url.pathname === '/api/providers') { providersLoaded = true; return json(page([{ id: 'prov-0', code: 'P00', name: 'Mobitel', isActive: true }, { id: 'prov-1', code: 'P01', name: 'Dialog', isActive: true }])) }
      return json(page([]))
    })
    render(<MobilePackagesPage />)
    await waitFor(() => expect(providersLoaded).toBe(true))
    await screen.findByText('No mobile packages match the selected filters.')

    fireEvent.click(screen.getByRole('button', { name: 'Create' }))
    expect((screen.getByRole('combobox', { name: 'Telecom Provider' }) as HTMLInputElement).value).toBe('P01 — Dialog')
    fireEvent.change(screen.getByRole('textbox', { name: 'Package Code' }), { target: { value: 'PPU23_700' } })
    fireEvent.change(screen.getByRole('textbox', { name: 'Package Description' }), { target: { value: 'Unlimited any network calls & 5 GB data per month' } })
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Rental (before tax)' }), { target: { value: '700' } })
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Total with Tax' }), { target: { value: '940' } })
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Default Credit Limit' }), { target: { value: '1000' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(submitted).toEqual({ providerId: 'prov-1', code: 'PPU23_700', description: 'Unlimited any network calls & 5 GB data per month', monthlyRental: 700, totalWithTax: 940, defaultCreditLimit: 1000 }))
  })

  it('offers Apply Rental only when some allocations are on another rental', async () => {
    const posted: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input)
      if (init?.method === 'POST') { posted.push(url); return json(3) }
      if (url.includes('/api/mobile-packages')) return json(page([{ ...ppu, monthlyRental: 750, allocationsWithOtherRental: 3 }, { ...ppu, id: 'pkg-2', code: 'OTHER' }]))
      return json(page([]))
    })
    render(<MobilePackagesPage />)

    expect(await screen.findByText('279 (3 on another rental)')).toBeTruthy()
    const apply = screen.getAllByRole('button', { name: 'Apply Rental' })
    expect(apply).toHaveLength(1)
    fireEvent.click(apply[0])
    fireEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Apply Rental' }))

    await waitFor(() => expect(posted).toEqual([expect.stringContaining('/api/mobile-packages/pkg-1/apply-rental')]))
  })

  it('fills in the rental and credit limit from the chosen package for a new allocation', async () => {
    let submitted: unknown
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = new URL(String(input))
      if (url.pathname === '/api/mobile-accounts' && init?.method === 'POST') { submitted = JSON.parse(String(init.body)); return json({ id: 'm1' }, 201) }
      if (url.pathname === '/api/employees') return json(page([{ id: 'e1', epf: 'EPF-1', fullName: 'Holder', factoryName: 'Head Office', isActive: true }]))
      if (url.pathname === '/api/mobile-packages') return json(page([ppu]))
      return json(page([]))
    })
    render(<MobileAllocationsPage />)

    fireEvent.click(await screen.findByRole('button', { name: 'Create' }))
    fireEvent.change(screen.getByRole('textbox', { name: 'Mobile Number' }), { target: { value: '0771234567' } })
    await selectOption('Employee', 'Holder', 'EPF-1 — Holder (Head Office)')
    await selectOption('SIM Type', 'Voice', 'Voice + Data')
    await selectOption('Package', 'PPU23', 'PPU23_700 — Unlimited any network calls & 5 GB data per month (Dialog)')

    expect((screen.getByRole('spinbutton', { name: 'Monthly Rental' }) as HTMLInputElement).value).toBe('700')
    expect((screen.getByRole('spinbutton', { name: 'Monthly Credit Limit' }) as HTMLInputElement).value).toBe('1000')
    fireEvent.change(screen.getByRole('spinbutton', { name: 'Monthly Credit Limit' }), { target: { value: '2000' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(submitted).toEqual({ mobileNumber: '0771234567', employeeId: 'e1', monthlyCreditLimit: 2000, monthlyRental: 700, packageId: 'pkg-1', simType: 'VoiceData' }))
  })

  it('shows each allocation\'s package and marks a rental that differs from it', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => String(input).includes('/api/mobile-accounts')
      ? json(page([
        { id: 'm1', mobileNumber: '0771', epf: 'EPF-1', employeeName: 'A', monthlyCreditLimit: 1000, monthlyRental: 700, isActive: true, status: 'Assigned', packageCode: 'PPU23_700', differsFromPackage: false, simType: 'ESim' },
        { id: 'm2', mobileNumber: '0772', epf: 'EPF-2', employeeName: 'B', monthlyCreditLimit: 1000, monthlyRental: 650, isActive: true, status: 'Assigned', packageCode: 'PPU23_700', differsFromPackage: true },
        { id: 'm3', mobileNumber: '0773', epf: 'EPF-3', employeeName: 'C', monthlyCreditLimit: 0, monthlyRental: 2100, isActive: true, status: 'Assigned', packageCode: null },
      ]))
      : json(page([])))
    render(<MobileAllocationsPage />)

    expect(await screen.findByText('PPU23_700')).toBeTruthy()
    expect(screen.getByText('PPU23_700 (rental differs)')).toBeTruthy()
    expect(screen.getByText('eSIM')).toBeTruthy()
    expect(screen.getByRole('table').textContent).toContain('—')
  })
})
