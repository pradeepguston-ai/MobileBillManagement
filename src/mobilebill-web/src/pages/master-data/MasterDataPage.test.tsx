import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { DepartmentsPage, EmployeesPage, FactoriesPage, MobileAllocationsPage } from './MasterPages'

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const page = (items: unknown[], pageNumber = 1, totalPages = 1, totalCount = items.length) => ({ items, pageNumber, pageSize: 100, totalCount, totalPages })
// Filling the whole employee form takes about 5 seconds on slower machines, just over Vitest's default limit.
const LongFormTimeout = 15000

// Opens a short drop-down and clicks an option, without typing a search.
async function pickOption(fieldName: string, optionName: string) {
  fireEvent.keyDown(screen.getByRole('combobox', { name: fieldName }), { key: 'ArrowDown' })
  fireEvent.click(await screen.findByRole('option', { name: optionName }))
}

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

  it('creates a department by name only: its code is generated on the server and never shown', async () => {
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
    expect(screen.queryByRole('textbox', { name: /Code/ })).toBeNull()
    expect(screen.queryByRole('columnheader', { name: 'Code' })).toBeNull()
    fireEvent.change(screen.getByRole('textbox', { name: /Name/ }), { target: { value: 'Information Technology' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(submittedBody).toEqual({ name: 'Information Technology' }))
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
    fireEvent.change(screen.getByRole('textbox', { name: 'Name with Initials' }), { target: { value: 'T. EMPLOYEE' } })
    fireEvent.change(screen.getByRole('textbox', { name: 'Calling Name' }), { target: { value: 'Test' } })
    // The title follows the gender and cannot be typed.
    const title = screen.getByRole('textbox', { name: 'Title' }) as HTMLInputElement
    expect(title.disabled).toBe(true)
    await selectSearchResult('Gender', 'Fe', 'Female')
    expect(title.value).toBe('MS.')
    const gender = screen.getByRole('combobox', { name: 'Gender' })
    fireEvent.change(gender, { target: { value: '' } })
    fireEvent.keyDown(gender, { key: 'ArrowDown' })
    fireEvent.click(await screen.findByRole('option', { name: 'Male' }))
    expect(title.value).toBe('MR.')
    // Category, designation and department are picked by name; the factory keeps its code.
    await selectSearchResult('Category', 'Management', 'Management')
    await selectSearchResult('Designation', 'Engineer', 'Engineer')
    await selectSearchResult('Factory', 'F01', 'F01 — Head Office')
    await selectSearchResult('Department', 'Information', 'Information Technology')
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(submittedBody).toEqual({
      epf: 'EPF-100',
      fullName: 'Test Employee',
      nameWithInitials: 'T. EMPLOYEE',
      callingName: 'Test',
      gender: 'Male',
      categoryCode: 'CAT02',
      designationCode: 'DES01',
      factoryCode: 'F01',
      departmentCode: 'D01',
      sectionCode: null,
      subSectionCode: null,
      thirdPartyCompanyCode: null,
    }))
    expect(screen.queryByRole('combobox', { name: 'Third Party Allocation' })).toBeNull()
  }, LongFormTimeout)

  it('offers Third Party Allocation only for the OTHER - THIRD-PARTY category and clears it when the category changes', async () => {
    let submittedBody: unknown
    vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
      const url = new URL(String(input))
      if (url.pathname === '/api/employees' && init?.method === 'POST') { submittedBody = JSON.parse(String(init.body)); return json({ id: 'employee-1' }, 201) }
      if (url.pathname === '/api/departments') return json(page([{ id: 'd1', code: 'DEP029', name: 'SECURITY', isActive: true }, { id: 'd2', code: 'DEP012', name: 'INFORMATION TECHNOLOGY (IT)', isActive: true }]))
      if (url.pathname === '/api/third-party-companies') return json(page([{ id: 't1', code: 'TPC001', name: 'AVENGERS SECURITY & INVESTIGATIONS (PVT) LTD', isActive: true }]))
      if (url.pathname === '/api/categories') return json(page([{ id: 'c1', code: 'CAT005', name: 'OTHER - THIRD-PARTY', isActive: true }, { id: 'c2', code: 'CAT001', name: 'GENERAL EMPLOYEE', isActive: true }]))
      if (url.pathname === '/api/designations') return json(page([{ id: 'g1', code: 'DES090', name: 'OTHER - THIRD-PARTY', isActive: true }]))
      if (url.pathname === '/api/factories') return json(page([{ id: 'f1', code: 'CAL', name: 'CONCORD APPAREL (PVT) LTD', isActive: true }]))
      return json(page([], 1, 0, 0))
    })

    render(<EmployeesPage />)
    fireEvent.click(screen.getByRole('button', { name: 'Create' }))
    expect(screen.queryByRole('combobox', { name: 'Third Party Allocation' })).toBeNull()

    await pickOption('Department', 'SECURITY')
    expect(screen.queryByRole('combobox', { name: 'Third Party Allocation' })).toBeNull()   // the department does not decide it
    await pickOption('Category', 'OTHER - THIRD-PARTY')
    await pickOption('Third Party Allocation', 'AVENGERS SECURITY & INVESTIGATIONS (PVT) LTD')
    await pickOption('Category', 'GENERAL EMPLOYEE')
    expect(screen.queryByRole('combobox', { name: 'Third Party Allocation' })).toBeNull()
    await pickOption('Category', 'OTHER - THIRD-PARTY')
    const allocation = screen.getByRole('combobox', { name: 'Third Party Allocation' }) as HTMLInputElement
    expect(allocation.value).toBe('Not Applicable')   // cleared when the category changed
    await pickOption('Third Party Allocation', 'AVENGERS SECURITY & INVESTIGATIONS (PVT) LTD')

    fireEvent.change(screen.getByRole('textbox', { name: 'EPF' }), { target: { value: 'SEC_CAL_01' } })
    fireEvent.change(screen.getByRole('textbox', { name: 'Full Name' }), { target: { value: 'ARUNA SHANTHA JAYASINGHE' } })
    await selectSearchResult('Gender', 'Ma', 'Male')
    await selectSearchResult('Designation', 'OTHER', 'OTHER - THIRD-PARTY')
    await selectSearchResult('Factory', 'CAL', 'CAL — CONCORD APPAREL (PVT) LTD')
    fireEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(submittedBody).toMatchObject({ categoryCode: 'CAT005', departmentCode: 'DEP029', thirdPartyCompanyCode: 'TPC001' }))
  }, LongFormTimeout)

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

    await selectSearchResult('Department', 'Information', 'Information Technology')
    expect((screen.getByRole('combobox', { name: 'Department' }) as HTMLInputElement).value).toBe('Information Technology')
    await selectSearchResult('Factory', 'F02', 'F02 — Plant')
    expect((screen.getByRole('combobox', { name: 'Department' }) as HTMLInputElement).value).toBe('Information Technology')
  })

  it('filters employees and mobile allocations by factory and department', async () => {
    const listed: URL[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
      const url = new URL(String(input))
      if (url.pathname === '/api/factories') return json(page([{ id: 'f1', code: 'CAL', name: 'CONCORD APPAREL (PVT) LTD', isActive: true }]))
      if (url.pathname === '/api/departments') return json(page([{ id: 'd1', code: 'DEP001', name: 'ACCOUNTS', isActive: true }]))
      if (url.pathname === '/api/employees' || url.pathname === '/api/mobile-accounts') listed.push(url)
      return json(page([], 1, 0, 0))
    })

    for (const [Page, path] of [[EmployeesPage, '/api/employees'], [MobileAllocationsPage, '/api/mobile-accounts']] as const) {
      listed.length = 0
      const { unmount } = render(<Page />)
      fireEvent.mouseDown(await screen.findByRole('combobox', { name: 'Factory' }))
      fireEvent.click(await screen.findByRole('option', { name: 'CONCORD APPAREL (PVT) LTD' }))
      fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Department' }))
      fireEvent.click(await screen.findByRole('option', { name: 'ACCOUNTS' }))

      await waitFor(() => expect(listed.some(url => url.pathname === path && url.searchParams.get('factoryCode') === 'CAL' && url.searchParams.get('departmentCode') === 'DEP001')).toBe(true))
      unmount()
    }
  }, LongFormTimeout)

  describe('mobile allocations', () => {
    const employee = { id: 'employee-1', epf: 'EPF-100', fullName: 'TESTING EMPLOYEE PERERA', nameWithInitials: 'T.E. PERERA', callingName: 'TEST', factoryName: 'Head Office', isActive: true }

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
      // Shown with initials and calling name, and still found by typing the full name.
      await selectSearchResult('Employee', 'TESTING EMPLOYEE', 'EPF-100 — T.E. PERERA (TEST) — Head Office')
      fireEvent.change(screen.getByRole('spinbutton', { name: 'Monthly Credit Limit' }), { target: { value: '1500.25' } })
      fireEvent.change(screen.getByRole('spinbutton', { name: 'Monthly Rental' }), { target: { value: '700' } })
      fireEvent.click(screen.getByRole('button', { name: 'Save' }))

      await waitFor(() => expect(submittedBody).toEqual({ mobileNumber: '0771234567', employeeId: 'employee-1', monthlyCreditLimit: 1500.25, monthlyRental: 700, packageId: null, simType: null }))
    })

    it('lists the name with initials, or the full name when there are none', async () => {
      const account = { id: 'account-1', mobileNumber: '771234567', epf: 'EPF-100', factory: 'Head Office', department: 'IT', monthlyCreditLimit: 1000, monthlyRental: 700, isActive: true, status: 'Assigned' }
      vi.spyOn(globalThis, 'fetch').mockImplementation(async input => new URL(String(input)).pathname === '/api/mobile-accounts'
        ? json(page([{ ...account, employeeName: 'TESTING EMPLOYEE PERERA', nameWithInitials: 'T.E. PERERA', callingName: 'TESTER' }, { ...account, id: 'account-2', mobileNumber: '771234568', employeeName: 'SAMAN PERERA', nameWithInitials: null }]))
        : json(page([], 1, 0, 0)))

      render(<MobileAllocationsPage />)

      expect(await screen.findByRole('columnheader', { name: 'Name With Initials' })).toBeTruthy()
      expect(await screen.findByText('T.E. PERERA')).toBeTruthy()
      expect(screen.getByText('SAMAN PERERA')).toBeTruthy()
      expect(screen.queryByText('TESTING EMPLOYEE PERERA')).toBeNull()
      expect(screen.getByRole('columnheader', { name: 'Calling Name' })).toBeTruthy()
      expect(screen.getByText('TESTER')).toBeTruthy()
    })
  })
})
