import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { DashboardPage } from '../../pages/DashboardPage'

// The real charts need a browser layout engine; these stand-ins list the data each chart receives.
vi.mock('@mui/x-charts', () => ({
  LineChart: ({ xAxis, series }: { xAxis: Array<{ data: string[] }>; series: Array<{ label: string; data: number[] }> }) =>
    <div data-testid="line-chart">{xAxis[0].data.join('|')} :: {series.map(item => `${item.label}=${item.data.join(',')}`).join(' ; ')}</div>,
  PieChart: ({ series }: { series: Array<{ data: Array<{ label: string; value: number }> }> }) =>
    <div data-testid="pie-chart">{series[0].data.map(item => `${item.label}=${item.value}`).join(' ; ')}</div>,
  BarChart: ({ yAxis, series }: { yAxis: Array<{ data: string[] }>; series: Array<{ label: string; data: number[] }> }) =>
    <div data-testid="bar-chart">{yAxis[0].data.join('|')} :: {series.map(item => `${item.label}=${item.data.join(',')}`).join(' ; ')}</div>,
}))

const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
const kpis = (overrides: Record<string, number> = {}) => ({ totalActualBill: 390096.74, totalEntitlement: 380000, totalCalculatedExcess: 9000, deductedFromEmployees: 6000, borneByCompany: 2500, unassessedExcess: 500, totalRoaming: 1200, accounts: 259, overLimitAccounts: 40, ...overrides })
const july = { id: 'jul', billingYear: 2026, billingMonth: 7, provider: 'Dialog', status: 'Completed', isApproved: true }
const aug = { id: 'aug', billingYear: 2026, billingMonth: 8, provider: 'Dialog', status: 'HRApproval', isApproved: false }
const insights = {
  batches: [aug, july],
  selected: aug,
  current: kpis(),
  previous: kpis({ totalActualBill: 380000, totalCalculatedExcess: 10000, overLimitAccounts: 40 }),
  previousBatch: july,
  chargeMix: [{ key: 'usage', label: 'Usage', amount: 300000 }, { key: 'vat', label: 'VAT', amount: 50000 }, { key: 'discounts', label: 'Discounts', amount: -1200 }],
  excessSplit: [{ key: 'deducted', label: 'Deducted from employees', amount: 6000 }, { key: 'companyRoaming', label: 'Company – roaming', amount: 2500 }],
  groups: {
    factory: [{ code: 'CAL', name: 'Concord Apparel', accounts: 120, actualBill: 200000, entitlement: 190000, calculatedExcess: 10000 }],
    department: [{ code: 'IT', name: 'IT', accounts: 10, actualBill: 20000, entitlement: 18000, calculatedExcess: 2000 }],
    category: [],
  },
  topOverLimit: [{ mobileNumber: '761499198', epf: '211464', employeeName: 'Employee One', callingName: 'Sam', factory: 'Concord Apparel', department: 'IT', actualBill: 4000, entitlement: 1200, calculatedExcess: 2800, responsibility: 'ByCompany', remark: 'Roaming' }],
  trend: [
    { batchId: 'jul', billingYear: 2026, billingMonth: 7, isApproved: true, totalActualBill: 380000, totalCalculatedExcess: 10000, deductedFromEmployees: 7000, borneByCompany: 3000, accounts: 258 },
    { batchId: 'aug', billingYear: 2026, billingMonth: 8, isApproved: false, totalActualBill: 390096.74, totalCalculatedExcess: 9000, deductedFromEmployees: 6000, borneByCompany: 2500, accounts: 259 },
  ],
}

function mockApi(body: unknown = insights) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async input => {
    const url = String(input)
    if (url.includes('/api/insights')) return json(body)
    if (url.includes('/api/bill-batches?')) return json({ items: [], pageNumber: 1, pageSize: 5, totalCount: 0, totalPages: 0 })
    return json({})
  })
}

async function openInsights() {
  render(<MemoryRouter><DashboardPage /></MemoryRouter>)

}

describe('Dashboard insights', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('shows comparison cards, trend, charge mix, excess split, cost by group and the top over-limit numbers', async () => {
    mockApi(); await openInsights()

    const bill = await screen.findByLabelText('Total Actual Bill')
    expect(bill.textContent).toContain('390,096.74')
    expect(bill.textContent).toContain('▲')
    expect(bill.textContent).toContain('(2.7%)')
    expect(bill.textContent).toContain('vs July 2026')
    expect(screen.getByLabelText('Calculated Excess').textContent).toContain('▼')
    expect(screen.getByLabelText('Numbers over Limit').textContent).toContain('No change')
    expect(screen.getByText('Provisional – not yet approved')).toBeTruthy()

    expect(within(screen.getByLabelText('Monthly trend')).getByTestId('line-chart').textContent).toContain('Jul 2026|Aug 2026*')
    const mix = within(screen.getByLabelText('Where the money goes'))
    expect(mix.getByTestId('pie-chart').textContent).toBe('Usage=300000 ; VAT=50000')
    expect(mix.getByText(/Credits not shown in the chart: Discounts/)).toBeTruthy()
    expect(within(screen.getByLabelText('Who pays the excess')).getByTestId('pie-chart').textContent).toContain('Company – roaming=2500')

    expect(within(screen.getByLabelText('Cost by factory')).getByTestId('bar-chart').textContent).toContain('Concord Apparel')
    fireEvent.click(screen.getByRole('button', { name: 'Department' }))
    expect(within(await screen.findByLabelText('Cost by department')).getByTestId('bar-chart').textContent).toContain('IT :: Actual bill=20000')

    const top = screen.getByRole('table', { name: 'Top over-limit numbers' })
    expect(within(top).getByText('761499198')).toBeTruthy()
    expect(within(top).getByText('Roaming')).toBeTruthy()
    expect(within(top).getByText('By Company')).toBeTruthy()
    expect(within(top).getByText('211464')).toBeTruthy()
    expect(within(top).getByText('Sam')).toBeTruthy()
    expect(within(top).getAllByRole('columnheader').map(cell => cell.textContent).slice(0, 5)).toEqual(['#', 'Mobile', 'EPF', 'Employee', 'Calling Name'])
  })

  it('reloads for another batch and trend period', async () => {
    const fetchMock = mockApi(); await openInsights()
    await screen.findByLabelText('Total Actual Bill')

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Billing batch' }))
    fireEvent.click(await screen.findByRole('option', { name: 'July 2026 · Dialog' }))
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).includes('batchId=jul'))).toBe(true))

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Trend period' }))
    fireEvent.click(await screen.findByRole('option', { name: 'Last 12 approved batches' }))
    await waitFor(() => expect(fetchMock.mock.calls.some(([url]) => String(url).includes('months=12'))).toBe(true))
  })

  it('explains when there is nothing to analyse yet', async () => {
    mockApi({ ...insights, batches: [], selected: null, current: null, previous: null, previousBatch: null, chargeMix: [], excessSplit: [], groups: { factory: [], department: [], category: [] }, topOverLimit: [], trend: [] })
    await openInsights()

    expect(await screen.findByText('Insights appear once a billing batch has been matched to employees.')).toBeTruthy()
  })

  it('shows only insights on the Dashboard, without the batch overview', async () => {
    mockApi(); await openInsights()

    expect(screen.getByRole('heading', { name: 'Dashboard' })).toBeTruthy()
    expect(await screen.findByLabelText('Total Actual Bill')).toBeTruthy()
    expect(screen.queryByRole('tab')).toBeNull()
    expect(screen.queryByText('CURRENT / LATEST BATCH')).toBeNull()
    expect(screen.queryByText('Recent Billing Batches')).toBeNull()
  })
})
