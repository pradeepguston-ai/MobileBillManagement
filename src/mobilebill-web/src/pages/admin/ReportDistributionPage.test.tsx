import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { ReportDistributionPage } from './ReportDistributionPage'

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
const users = [
  { userId: 'u1', displayName: 'Kumara Mohottige', email: 'hr@concord.lk', role: 'GroupHrManager' },
  { userId: 'u2', displayName: 'Finance User', email: 'finance@concord.lk', role: 'FinanceUser' },
]
const rule = {
  id: 'r1', name: 'CAPL - Executive and Staff', factoryCodes: ['CAPL'], factoryLabel: 'Concord Apparel', categoryCodes: ['EX', 'ST'], categoryLabel: 'Executive, Staff', isActive: true,
  recipients: [{ userId: 'u1', email: 'hr@concord.lk', displayName: 'Kumara Mohottige', inactive: false }, { userId: 'u3', email: 'old@concord.lk', displayName: 'Old Manager', inactive: true }, { userId: null, email: 'manager.capl@concord.lk', displayName: null, inactive: false }],
}

function mockApi(rules: unknown[] = [rule]) {
  const writes: Array<{ method: string; url: string; body: unknown }> = []
  vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = String(input)
    if (init?.method && init.method !== 'GET') { writes.push({ method: init.method, url, body: init.body ? JSON.parse(String(init.body)) : undefined }); return init.method === 'DELETE' ? new Response(null, { status: 204 }) : json(rule) }
    if (url.includes('/recipient-options')) return json(users)
    if (url.includes('/report-distribution/rules')) return json(rules)
    return json({ items: [], pageNumber: 1, pageSize: 100, totalCount: 0, totalPages: 0 })
  })
  return writes
}

function renderPage() { return render(<MemoryRouter><ReportDistributionPage /></MemoryRouter>) }

describe('Report Distribution', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('lists each rule with its factories, categories and recipients, marking inactive users', async () => {
    mockApi(); renderPage()

    const row = (await screen.findByText('CAPL - Executive and Staff')).closest('tr')!
    expect(within(row).getByText('Concord Apparel')).toBeTruthy()
    expect(within(row).getByText('Executive, Staff')).toBeTruthy()
    expect(within(row).getByText('Kumara Mohottige')).toBeTruthy()
    expect(within(row).getByText('Old Manager (inactive)')).toBeTruthy()
    expect(within(row).getByText('manager.capl@concord.lk')).toBeTruthy()
    expect(within(row).getByText('Active')).toBeTruthy()
  })

  it('adds a rule with several system users and a typed email address', async () => {
    const writes = mockApi([]); renderPage()
    await screen.findByText(/No report distribution rules yet/)

    fireEvent.click(screen.getByRole('button', { name: 'Add Rule' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.change(within(dialog).getByLabelText(/Rule name/), { target: { value: 'All factories - Managers' } })
    const recipients = within(dialog).getByLabelText('Recipients')
    for (const name of ['Kumara', 'Finance']) {
      fireEvent.change(recipients, { target: { value: name } })
      fireEvent.click(await screen.findByRole('option', { name: new RegExp(name) }))
    }
    fireEvent.change(recipients, { target: { value: 'gm@concord.lk' } })
    fireEvent.keyDown(recipients, { key: 'Enter' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(writes).toEqual([{ method: 'POST', url: expect.stringContaining('/api/report-distribution/rules'), body: { name: 'All factories - Managers', factoryCodes: [], categoryCodes: [], isActive: true, emails: ['gm@concord.lk'], userIds: ['u1', 'u2'] } }]))
  })

  it('refuses a typed address that is not an email', async () => {
    const writes = mockApi([]); renderPage()
    await screen.findByText(/No report distribution rules yet/)

    fireEvent.click(screen.getByRole('button', { name: 'Add Rule' }))
    const dialog = await screen.findByRole('dialog')
    fireEvent.change(within(dialog).getByLabelText(/Rule name/), { target: { value: 'Rule' } })
    const recipients = within(dialog).getByLabelText('Recipients')
    fireEvent.change(recipients, { target: { value: 'not-an-email' } })
    fireEvent.keyDown(recipients, { key: 'Enter' })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(await within(dialog).findByText("'not-an-email' is not a valid email address.")).toBeTruthy()
    expect(writes).toEqual([])
  })

  it('edits a rule, keeping its recipients including an inactive user', async () => {
    const writes = mockApi(); renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Edit' }))
    const dialog = await screen.findByRole('dialog')
    expect((within(dialog).getByLabelText(/Rule name/) as HTMLInputElement).value).toBe('CAPL - Executive and Staff')
    fireEvent.click(within(dialog).getByRole('switch', { name: /Active/ }))
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(writes).toEqual([{ method: 'PUT', url: expect.stringContaining('/api/report-distribution/rules/r1'), body: { name: 'CAPL - Executive and Staff', factoryCodes: ['CAPL'], categoryCodes: ['EX', 'ST'], isActive: false, emails: ['manager.capl@concord.lk'], userIds: ['u1', 'u3'] } }]))
  })

  it('deletes a rule after confirming', async () => {
    const writes = mockApi(); renderPage()

    fireEvent.click(await screen.findByRole('button', { name: 'Delete' }))
    fireEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Delete' }))

    await waitFor(() => expect(writes).toEqual([{ method: 'DELETE', url: expect.stringContaining('/api/report-distribution/rules/r1'), body: undefined }]))
  })
})
