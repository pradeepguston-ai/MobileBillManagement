import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { DepartmentsPage, EmployeesPage, FactoriesPage, MobileAllocationsPage } from './MasterPages'

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const page = (items: unknown[], pageNumber = 1, totalPages = 1, totalCount = items.length) => ({ items, pageNumber, pageSize: 100, totalCount, totalPages })

async function selectSearchResult(fieldName: string, search: string, optionName: string) {
  const input = await screen.findByRole('combobox', { name: fieldName })
  fireEvent.change(input, { target: { value: search } })
  fireEvent.click(await screen.findByRole('option', { name: optionName }))
}

describe('MasterDataPage states', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('shows an explicit empty state after an empty server page', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response(JSON.stringify({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }), { status: 200, headers: { 'Content-Type': 'application/json' } }))

    render(<FactoriesPage />)

    expect(await screen.findByText('No factories match the selected filters.')).toBeTruthy()
  })

  it('creates a department using just a code and name', async () => {
    let submittedBody: unknown
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = String(input)
      if (url.includes('/api/departments') && init?.method === 'POST') {
        submittedBody = JSON.parse(String(init.body))
        return new Response(JSON.stringify({ id: 'department-1' }), { status: 201, headers: { 'Content-Type': 'application/json' } })
      }
      return new Response(JSON.stringify({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }), { status: 200, headers: { 'Content-Type': 'application/json' } })
    })

    render(<DepartmentsPage />)
    fireEvent.click(screen.getByRole('button', { name: 'Create' }))

    expect(screen.queryByRole('combobox', { name: 'Factory' })).toBeNull()
    fireEvent.change(screen.getByRole('textbox', { name: /Code/ }), { target: { value: 'IT' } })
    fireEvent.change(screen.getByRole('textbox', { name: /Name/ }), { target: { value: 'Information Technology' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(submittedBody).toEqual({
      code: 'IT',
      name: 'Information Technology',
    }))
  })

  it('creates an employee using searchable active master-data selections', async () => {
    let submittedBody: unknown
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = new URL(String(input))
      if (url.pathname === '/api/employees' && init?.method === 'POST') {
        submittedBody = JSON.parse(String(init.body))
        return json({ id: 'employee-1' }, 201)
      }
      if (url.pathname === '/api/categories') {
        return url.searchParams.get('pageNumber') === '2'
          ? json(page([{ id: 'category-2', code: 'CAT02', name: 'Management', isActive: true }], 2, 2, 2))
          : json(page([{ id: 'category-1', code: 'CAT01', name: 'Staff', isActive: true }], 1, 2, 2))
      }
      if (url.pathname === '/api/designations') return json(page([{ id: 'designation-1', code: 'DES01', name: 'Engineer', isActive: true }]))
      if (url.pathname === '/api/factories') return json(page([{ id: 'factory-1', code: 'F01', name: 'Head Office', isActive: true }]))
      if (url.pathname === '/api/departments') return json(page([{ id: 'department-1', code: 'D01', name: 'Information Technology', isActive: true }]))
      return json(page([], 1, 0, 0))
    })

    render(<EmployeesPage />)
    fireEvent.click(screen.getByRole('button', { name: 'Create' }))

    fireEvent.change(screen.getByRole('textbox', { name: 'EPF' }), { target: { value: 'EPF-100' } })
    fireEvent.change(screen.getByRole('textbox', { name: 'Full Name' }), { target: { value: 'Test Employee' } })
    fireEvent.change(screen.getByRole('textbox', { name: 'Calling Name' }), { target: { value: 'Test' } })
    await selectSearchResult('Category', 'Management', 'CAT02 — Management')
    await selectSearchResult('Designation', 'Engineer', 'DES01 — Engineer')
    await selectSearchResult('Factory', 'F01', 'F01 — Head Office')
    await selectSearchResult('Department', 'Information', 'D01 — Information Technology')
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(submittedBody).toEqual({
      epf: 'EPF-100',
      fullName: 'Test Employee',
      callingName: 'Test',
      categoryCode: 'CAT02',
      designationCode: 'DES01',
      factoryCode: 'F01',
      departmentCode: 'D01',
      sectionCode: null,
      subSectionCode: null,
    }))
  })

  it('allows selecting a department independently of the chosen factory', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = new URL(String(input))
      if (url.pathname === '/api/factories') return json(page([
        { id: 'factory-1', code: 'F01', name: 'Head Office', isActive: true },
        { id: 'factory-2', code: 'F02', name: 'Plant', isActive: true },
      ]))
      if (url.pathname === '/api/departments') return json(page([
        { id: 'department-1', code: 'D01', name: 'Information Technology', isActive: true },
        { id: 'department-2', code: 'D02', name: 'Operations', isActive: true },
      ]))
      return json(page([], 1, 0, 0))
    })

    render(<EmployeesPage />)
    fireEvent.click(screen.getByRole('button', { name: 'Create' }))

    await selectSearchResult('Department', 'D01', 'D01 — Information Technology')
    expect((screen.getByRole('combobox', { name: 'Department' }) as HTMLInputElement).value).toBe('D01 — Information Technology')
    await selectSearchResult('Factory', 'F02', 'F02 — Plant')
    expect((screen.getByRole('combobox', { name: 'Department' }) as HTMLInputElement).value).toBe('D01 — Information Technology')
  })

  describe('mobile allocations', () => {
    const employee = { id: 'employee-1', epf: 'EPF-100', fullName: 'Test Employee', factoryName: 'Head Office', isActive: true }

    it('creates an allocation for the employee picked by EPF, name and factory', async () => {
      let submittedBody: unknown
      vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
        const url = new URL(String(input))
        if (url.pathname === '/api/mobile-accounts' && init?.method === 'POST') {
          submittedBody = JSON.parse(String(init.body))
          return json({ id: 'account-1' }, 201)
        }
        if (url.pathname === '/api/employees') return json(page([employee]))
        return json(page([], 1, 0, 0))
      })

      render(<MobileAllocationsPage />)
      fireEvent.click(screen.getByRole('button', { name: 'Create' }))

      expect(screen.queryByRole('textbox', { name: /Employee ID/ })).toBeNull()
      fireEvent.change(screen.getByRole('textbox', { name: /Mobile Number/ }), { target: { value: '0771234567' } })
      await selectSearchResult('Employee', 'EPF-100', 'EPF-100 — Test Employee (Head Office)')
      fireEvent.change(screen.getByRole('spinbutton', { name: 'Monthly Credit Limit' }), { target: { value: '1500.25' } })
      fireEvent.change(screen.getByRole('spinbutton', { name: 'Monthly Rental' }), { target: { value: '700' } })
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))

      await waitFor(() => expect(submittedBody).toEqual({ mobileNumber: '0771234567', employeeId: 'employee-1', monthlyCreditLimit: 1500.25, monthlyRental: 700, packageId: null, simType: null }))
    })
  })
})
