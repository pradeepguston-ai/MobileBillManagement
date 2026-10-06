import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'

import { BillTrendSection } from './BillTrendSection'

vi.mock('@mui/x-charts', () => ({
  LineChart: ({ xAxis, series }: { xAxis: Array<{ data: string[] }>; series: Array<{ label: string; data: number[] }> }) =>
    <div data-testid="line-chart">{xAxis[0].data.join('|')} :: {series.map(item => `${item.label}=${item.data.join(',')}`).join(' ; ')}</div>,
}))

const json = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
const point = (month: number, actualBill: number, extra: Record<string, unknown> = {}) => ({
  billingYear: 2026, billingMonth: month, actualBill, entitlement: 1500, calculatedExcess: Math.max(0, actualBill - 1500), finalDeduction: Math.max(0, actualBill - 1500), vas: 0,
  responsibility: 'ByUser', isOverLimit: actualBill > 1500, isPreliminary: false, isCurrent: false, numbers: 1, holderEpf: 'EPF-1', holderName: 'Kasun', isOtherHolder: false, ...extra,
})
const trend = {
  monthlyBillId: 'bill', mobileNumber: '0771', employeeEpf: 'EPF-1', employeeName: 'Kasun', scope: 'ThisNumber', months: 12,
  points: [point(7, 1000), point(8, 2000), point(9, 2600, { isCurrent: true, isPreliminary: true })],
  currentActualBill: 2600, average: 1500, changePercent: 73.3, isAboveUsual: true, monthsOverLimit: 2, highestYear: 2026, highestMonth: 9, highestActualBill: 2600, aboveUsualThresholdPercent: 30,
}

describe('BillTrendSection', () => {
  afterEach(() => { cleanup(); vi.restoreAllMocks() })

  it('charts the months with the summary and the above-usual warning', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => json(trend))
    render(<BillTrendSection batchId="batch" rowId="bill" />)

    const chart = await screen.findByTestId('line-chart')
    expect(chart.textContent).toContain('Jul 26|Aug 26|Sep 26 ●*')
    expect(chart.textContent).toContain('Actual Bill=1000,2000,2600')
    expect(screen.getByText(/\(\+73\.3%\) · Over limit 2 of 3 months · Highest Sep 2026/)).toBeTruthy()
    expect(screen.getByText(/Above usual/)).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Show monthly figures' }))
    const table = screen.getByRole('table', { name: 'Bill trend by month' })
    expect(within(table).getAllByRole('row')).toHaveLength(4)
  })

  it('asks the server for the chosen history and months', async () => {
    const requested: string[] = []
    vi.spyOn(globalThis, 'fetch').mockImplementation(async input => { requested.push(String(input)); return json(trend) })
    render(<BillTrendSection batchId="batch" rowId="bill" />)
    await screen.findByTestId('line-chart')

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'History of' }))
    fireEvent.click(await screen.findByRole('option', { name: 'All this employee\'s numbers' }))

    await waitFor(() => expect(requested.some(url => url.includes('/api/bill-batches/batch/review/rows/bill/trend?') && url.includes('scope=Employee') && url.includes('months=12'))).toBe(true))
  })

  it('says when there is no history yet', async () => {
    vi.spyOn(globalThis, 'fetch').mockImplementation(async () => json({ ...trend, points: [point(9, 900, { isCurrent: true })], average: null, changePercent: null, isAboveUsual: false }))
    render(<BillTrendSection batchId="batch" rowId="bill" />)

    expect(await screen.findByText('First month, no history yet.')).toBeTruthy()
    expect(screen.queryByTestId('line-chart')).toBeNull()
  })
})
