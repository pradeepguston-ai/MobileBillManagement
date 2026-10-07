import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { DashboardPage } from '../../pages/DashboardPage'

// The real charts need a browser layout engine; these stand-ins list the data each chart receives.
vi.mock('@mui/x-charts', () => ({
  LineChart: ({ xAxis, series }: { xAxis: Array<{ data: string[] }>; series: Array<{ label: string; data: number[] }> }) =>
    <div data-testid="line-chart">{xAxis[0].data.join('|')} :: {series.map(item => `${item.label}=${item.data.join(',')}`).join(' ; ')}</div>,
  BarChart: ({ xAxis, yAxis, series }: { xAxis: Array<{ data?: string[] }>; yAxis: Array<{ data?: string[] }>; series: Array<{ label: string; data: number[]; stack?: string }> }) =>
    <div data-testid="bar-chart">{(yAxis[0].data ?? xAxis[0].data ?? []).join('|')} :: {series.map(item => `${item.label}${item.stack ? '[stacked]' : ''}=${item.data.join(',')}`).join(' ; ')}</div>,
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
  excessSplit: [{ key: 'deducted', label: 'Deducted from employees', amount: 6000 }, { key: 'company', label: 'By Company', amount: 2500 }],
  groups: {
    factory: [{ code: 'CAL', name: 'Concord Apparel', accounts: 120, actualBill: 200000, entitlement: 190000, calculatedExcess: 10000 }],
    department: [{ code: 'IT', name: 'IT', accounts: 10, actualBill: 20000, entitlement: 18000, calculatedExcess: 2000 }],
    category: [],
  },
  topOverLimit: [{ mobileNumber: '761499198', epf: '211464', employeeName: 'Employee One', callingName: 'Sam', factory: 'Concord Apparel', department: 'IT', actualBill: 4000, entitlement: 1200, calculatedExcess: 2800, responsibility: 'ByCompany', remark: 'Roaming' }],
  trend: [
    { batchId: 'jul', billingYear: 2026, billingMonth: 7, isApproved: true, totalActualBill: 380000, totalCalculatedExcess: 10000, deductedFromEmployees: 7000, borneByCompany: 3000, accounts: 258, waivedForEmployees: 0, unassessedExcess: 0 },
    { batchId: 'aug', billingYear: 2026, billingMonth: 8, isApproved: false, totalActualBill: 390096.74, totalCalculatedExcess: 9000, deductedFromEmployees: 6000, borneByCompany: 2500, accounts: 259, waivedForEmployees: 0, unassessedExcess: 500 },
  ],
  billRanges: [
    { key: 'within', label: 'Within limit', accounts: 219, calculatedExcess: 0 },
    { key: 'upTo500', label: 'Up to 500', accounts: 30, calculatedExcess: 4000 },
    { key: 'upTo2000', label: '500 – 2,000', accounts: 8, calculatedExcess: 3000 },
    { key: 'upTo5000', label: '2,000 – 5,000', accounts: 2, calculatedExcess: 2000 },
    { key: 'over5000', label: 'Over 5,000', accounts: 0, calculatedExcess: 0 },
  ],
  repeatOverLimit: { windowMonths: 6, minMonthsOver: 3, totalCount: 1, accounts: [{ mobileNumber: '771234567', epf: '300100', employeeName: 'Repeat Holder', factory: 'Concord Apparel', packageCode: 'PPU23_700', monthsOverLimit: 4, monthsBilled: 6, totalExcess: 3200, averageExcess: 800, selectedBatchExcess: 0 }] },
  assets: {
    simTypes: [{ key: 'VoiceData', label: 'Voice + Data', count: 200 }, { key: 'NotSet', label: 'Not set', count: 63 }],
    deviceStatuses: [{ key: 'Issued', label: 'Issued', count: 4 }, { key: 'ReturnPending', label: 'Return Pending', count: 1 }, { key: 'InStock', label: 'In Stock', count: 1 }],
    devices: 6,
    devicesWithEmployeesValue: 250000,
  },
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
    expect(mix.getByRole('img', { name: 'Charge mix chart' }).textContent).toContain('350K')
    expect(mix.getAllByRole('listitem').map(item => item.textContent?.replace(/\s+/g, ' '))).toEqual([expect.stringMatching(/^Usage.*300,000\.00 \(86%\)$/), expect.stringMatching(/^VAT.*50,000\.00 \(14%\)$/)])
    expect(mix.getByText(/Credits not shown in the chart: Discounts/)).toBeTruthy()
    const excess = within(screen.getByLabelText('Who pays the excess'))
    expect(excess.getAllByRole('listitem').map(item => item.textContent)).toEqual([expect.stringMatching(/^Deducted from employees.*\(71%\)$/), expect.stringMatching(/^By Company.*2,500\.00 \(29%\)$/)])

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

  it('shows recoveries per month, bill ranges, repeat over-limit numbers, SIM types and devices', async () => {
    mockApi(); await openInsights()

    const recovery = within(await screen.findByLabelText('Deductions recovered per month')).getByTestId('bar-chart').textContent
    expect(recovery).toBe('Jul 2026|Aug 2026* :: Deducted from employees[stacked]=7000,6000 ; By Company[stacked]=3000,2500 ; Not yet assigned[stacked]=0,500')   // no waivers: series left out
    expect(within(screen.getByLabelText('Bill range')).getByTestId('bar-chart').textContent).toBe('Within limit|Up to 500|500 – 2,000|2,000 – 5,000|Over 5,000 :: Numbers=219,30,8,2,0')

    const repeat = screen.getByRole('table', { name: 'Repeat over-limit numbers' })
    expect(within(repeat).getByText('771234567')).toBeTruthy()
    expect(within(repeat).getByText('4 of 6')).toBeTruthy()
    expect(within(repeat).getByText('Within limit')).toBeTruthy()

    const sims = within(screen.getByLabelText('SIM type mix'))
    expect(sims.getByRole('img', { name: 'SIM type chart' }).textContent).toContain('263')
    expect(sims.getAllByRole('listitem').map(item => item.textContent)).toEqual([expect.stringMatching(/^Voice \+ Data.*200 \(76%\)$/), expect.stringMatching(/^Not set.*63 \(24%\)$/)])
    const devices = screen.getByLabelText('Device status')
    expect(devices.textContent).toMatch(/6 company devices; LKR\s250,000\.00 depreciated value still with employees/)
    expect(within(devices).getAllByRole('listitem').map(item => item.textContent?.split(/\d/)[0])).toEqual(['Issued', 'Return Pending', 'In Stock'])
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
