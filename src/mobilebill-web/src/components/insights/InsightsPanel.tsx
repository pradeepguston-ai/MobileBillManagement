import { Alert, Box, Card, CardContent, FormControl, InputLabel, MenuItem, Select, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material'
import { BarChart, LineChart } from '@mui/x-charts'
import { useCallback, useEffect, useState, type ReactNode } from 'react'

import { getBillingInsights, type BillingInsights, type InsightsBatchOption, type InsightsCount, type InsightsGroupRow, type InsightsKpis, type InsightsRepeatOverLimit } from '../../api/insightsApi'
import { billingPeriod } from '../../billing/billingRoutes'
import { brand } from '../../theme/theme'
import { formatCurrency } from '../../utils/formatters'
import { DonutChart } from '../common/DonutChart'
import { EmptyState } from '../common/EmptyState'
import { ErrorState } from '../common/ErrorState'
import { LoadingState } from '../common/LoadingState'
import { StatusBadge } from '../common/StatusBadge'

const palette = [brand.red, brand.purple, brand.orange, brand.magenta, brand.indigo, brand.yellow, '#16823A', '#0E7490', '#9B111E', '#6B7280', '#B45309', '#1D4ED8']
const shortMonth = (year: number, month: number) => new Date(year, month - 1, 1).toLocaleString('en-US', { month: 'short', year: 'numeric' })
const optionLabel = (option: InsightsBatchOption) => `${billingPeriod(option.billingYear, option.billingMonth)} · ${option.provider}${option.isApproved ? '' : ' (provisional)'}`
const money = (value: number) => formatCurrency(value)

type GroupBy = 'factory' | 'department' | 'category'

// The Dashboard's Insights tab. Every figure comes from the server; this component only lays out and draws them.
export function InsightsPanel() {
  const [batchId, setBatchId] = useState<string>()
  const [months, setMonths] = useState(6)
  const [groupBy, setGroupBy] = useState<GroupBy>('factory')
  const [data, setData] = useState<BillingInsights>()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string>()

  const load = useCallback(async () => {
    setLoading(true); setError(undefined)
    try { setData(await getBillingInsights(batchId, months)) }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load insights.') }
    finally { setLoading(false) }
  }, [batchId, months])

  useEffect(() => { const timer = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(timer) }, [load])

  if (loading && !data) return <LoadingState label="Loading insights…" />
  if (error && !data) return <ErrorState message={error} onRetry={() => void load()} />
  if (!data?.selected || !data.current) return <EmptyState message="Insights appear once a billing batch has been matched to employees." />

  const { selected, current, previous, previousBatch } = data
  return <Stack spacing={3} sx={{ opacity: loading ? 0.6 : 1, transition: 'opacity 150ms' }} aria-busy={loading}>
    {error && <ErrorState message={error} />}
    <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ alignItems: { md: 'center' } }}>
      <FormControl size="small" sx={{ minWidth: 280 }}>
        <InputLabel id="insights-batch">Billing batch</InputLabel>
        <Select labelId="insights-batch" label="Billing batch" value={selected.id} onChange={event => setBatchId(event.target.value)}>
          {data.batches.map(option => <MenuItem key={option.id} value={option.id}>{optionLabel(option)}</MenuItem>)}
        </Select>
      </FormControl>
      <FormControl size="small" sx={{ minWidth: 200 }}>
        <InputLabel id="insights-months">Trend period</InputLabel>
        <Select labelId="insights-months" label="Trend period" value={months} onChange={event => setMonths(Number(event.target.value))}>
          {[3, 6, 12].map(value => <MenuItem key={value} value={value}>Last {value} approved batches</MenuItem>)}
        </Select>
      </FormControl>
      {!selected.isApproved && <StatusBadge label="Provisional – not yet approved" tone="warning" />}
    </Stack>

    <ComparisonCards current={current} previous={previous} previousBatch={previousBatch} />

    <ChartCard title="Monthly trend" subtitle={data.trend.length < 2 ? 'The trend fills in as more batches are approved.' : 'Approved batches; * marks a batch that is not yet approved.'}>
      <TrendChart data={data} />
    </ChartCard>

    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'repeat(2, minmax(0, 1fr))' }, gap: 3 }}>
      <ChartCard title="Deductions recovered per month" subtitle="How each month's calculated excess was settled; * marks a batch that is not yet approved.">
        <RecoveryChart data={data} />
      </ChartCard>
      <ChartCard title="Bill range" subtitle={`How far each of the ${current.accounts} numbers in this batch went over its entitlement.`}>
        <BillRangeChart data={data} />
      </ChartCard>
    </Box>

    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'repeat(2, minmax(0, 1fr))' }, gap: 3 }}>
      <ChartCard title="Where the money goes" subtitle="Charges on the PDF for this batch, by type.">
        <ChargeMixChart data={data} />
      </ChartCard>
      <ChartCard title="Who pays the excess" subtitle={`Calculated excess ${money(current.totalCalculatedExcess)}, split by responsibility.`}>
        <ExcessSplitChart data={data} />
      </ChartCard>
    </Box>

    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'repeat(2, minmax(0, 1fr))' }, gap: 3 }}>
      <ChartCard title="SIM type mix" subtitle="Active allocations today, pooled SIMs included.">
        <CountDonut ariaLabel="SIM type chart" centerLabel="active SIMs" items={data.assets.simTypes} colors={simTypeColors} empty="No active allocations." />
      </ChartCard>
      <ChartCard title="Device status" subtitle={`${data.assets.devices} company devices; ${money(data.assets.devicesWithEmployeesValue)} depreciated value still with employees (Issued and Return Pending).`}>
        <CountDonut ariaLabel="Device status chart" centerLabel="devices" items={data.assets.deviceStatuses} colors={deviceStatusColors} empty="No company devices recorded." />
      </ChartCard>
    </Box>

    <ChartCard
      title={`Cost by ${groupBy}`}
      subtitle="Actual bill against entitlement (credit limit + rental); the gap is the calculated excess."
      action={<ToggleButtonGroup size="small" exclusive value={groupBy} onChange={(_, value: GroupBy | null) => value && setGroupBy(value)} aria-label="Group cost by">
        <ToggleButton value="factory">Factory</ToggleButton>
        <ToggleButton value="department">Department</ToggleButton>
        <ToggleButton value="category">Category</ToggleButton>
      </ToggleButtonGroup>}
    >
      <GroupChart rows={data.groups[groupBy]} />
    </ChartCard>

    <ChartCard title="Top 10 over-limit numbers" subtitle="Highest calculated excess in this batch.">
      <TopOverLimitTable data={data} />
    </ChartCard>

    <ChartCard
      title="Repeat over-limit numbers"
      subtitle={`Over the limit in ${data.repeatOverLimit.minMonthsOver} or more of the last ${data.repeatOverLimit.windowMonths} billing months (approved batches and this one) – candidates for a bigger package or a reminder.`}
    >
      <RepeatOverLimitTable repeat={data.repeatOverLimit} />
    </ChartCard>
  </Stack>
}

const simTypeColors: Record<string, string> = { Voice: brand.purple, VoiceData: brand.red, Data: brand.orange, ESim: brand.indigo, NotSet: '#9CA3AF' }
const deviceStatusColors: Record<string, string> = { InStock: '#16823A', Issued: brand.indigo, ReturnPending: brand.orange, UnderRepair: brand.yellow, Damaged: brand.red, Lost: '#9B111E', Retired: '#9CA3AF' }

function ChartCard({ title, subtitle, action, children }: { title: string; subtitle?: string; action?: ReactNode; children: ReactNode }) {
  return <Card variant="outlined" aria-label={title}><CardContent>
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ justifyContent: 'space-between', alignItems: { sm: 'flex-start' }, mb: 1.5 }}>
      <Box><Typography component="h3" variant="h6">{title}</Typography>{subtitle && <Typography variant="body2" color="text.secondary">{subtitle}</Typography>}</Box>
      {action}
    </Stack>
    {children}
  </CardContent></Card>
}

// "This batch vs the previous approved batch" cards. For cost figures a rise is shown in red and a fall in green.
function ComparisonCards({ current, previous, previousBatch }: { current: InsightsKpis; previous: InsightsKpis | null; previousBatch: InsightsBatchOption | null }) {
  const cards: Array<{ label: string; value: number; previous?: number; isMoney: boolean }> = [
    { label: 'Total Actual Bill', value: current.totalActualBill, previous: previous?.totalActualBill, isMoney: true },
    { label: 'Calculated Excess', value: current.totalCalculatedExcess, previous: previous?.totalCalculatedExcess, isMoney: true },
    { label: 'Deducted from Employees', value: current.deductedFromEmployees, previous: previous?.deductedFromEmployees, isMoney: true },
    { label: 'Borne by Company', value: current.borneByCompany, previous: previous?.borneByCompany, isMoney: true },
    { label: 'Roaming Charges', value: current.totalRoaming, previous: previous?.totalRoaming, isMoney: true },
    { label: 'Numbers over Limit', value: current.overLimitAccounts, previous: previous?.overLimitAccounts, isMoney: false },
  ]
  const against = previousBatch ? `vs ${billingPeriod(previousBatch.billingYear, previousBatch.billingMonth)}` : 'No earlier approved batch to compare'
  return <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'repeat(2, minmax(0, 1fr))', md: 'repeat(3, minmax(0, 1fr))', xl: 'repeat(6, minmax(0, 1fr))' }, gap: 2 }}>
    {cards.map(card => <Card key={card.label} variant="outlined" aria-label={card.label}><CardContent>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>{card.label}</Typography>
      <Typography variant="h6">{card.isMoney ? money(card.value) : card.value}</Typography>
      <Change value={card.value} previous={card.previous} isMoney={card.isMoney} />
      <Typography variant="caption" color="text.secondary">{against}</Typography>
    </CardContent></Card>)}
  </Box>
}

function Change({ value, previous, isMoney }: { value: number; previous?: number; isMoney: boolean }) {
  if (previous == null) return <Typography variant="body2" color="text.secondary">—</Typography>
  const delta = value - previous
  if (delta === 0) return <Typography variant="body2" color="text.secondary">No change</Typography>
  const percent = previous === 0 ? null : Math.abs(delta / previous) * 100
  const amount = isMoney ? money(Math.abs(delta)) : String(Math.abs(delta))
  return <Typography variant="body2" sx={{ color: delta > 0 ? 'error.main' : 'success.main', fontWeight: 600 }}>
    {delta > 0 ? '▲' : '▼'} {amount}{percent == null ? '' : ` (${percent.toFixed(1)}%)`}
  </Typography>
}

function TrendChart({ data }: { data: BillingInsights }) {
  const labels = data.trend.map(point => `${shortMonth(point.billingYear, point.billingMonth)}${point.isApproved ? '' : '*'}`)
  return <Box sx={{ width: '100%', height: 320 }}>
    <LineChart
      height={320}
      xAxis={[{ scaleType: 'point', data: labels }]}
      yAxis={[{ width: 80, valueFormatter: (value: number) => value.toLocaleString('en-US') }]}
      series={[
        { label: 'Total actual bill', data: data.trend.map(point => point.totalActualBill), color: brand.red, valueFormatter: value => money(value ?? 0) },
        { label: 'Calculated excess', data: data.trend.map(point => point.totalCalculatedExcess), color: brand.orange, valueFormatter: value => money(value ?? 0) },
        { label: 'Deducted from employees', data: data.trend.map(point => point.deductedFromEmployees), color: brand.purple, valueFormatter: value => money(value ?? 0) },
        { label: 'Borne by company', data: data.trend.map(point => point.borneByCompany), color: brand.magenta, valueFormatter: value => money(value ?? 0) },
      ]}
    />
  </Box>
}

// Stacked: the four parts add up to the month's calculated excess.
function RecoveryChart({ data }: { data: BillingInsights }) {
  const labels = data.trend.map(point => `${shortMonth(point.billingYear, point.billingMonth)}${point.isApproved ? '' : '*'}`)
  const series = [
    { label: 'Deducted from employees', data: data.trend.map(point => point.deductedFromEmployees), color: brand.purple },
    { label: 'Waived for employees', data: data.trend.map(point => point.waivedForEmployees), color: brand.magenta },
    { label: 'By Company', data: data.trend.map(point => point.borneByCompany), color: brand.orange },
    { label: 'Not yet assigned', data: data.trend.map(point => point.unassessedExcess), color: '#9CA3AF' },
  ].filter(item => item.data.some(value => value !== 0))
  if (series.length === 0) return <Typography color="text.secondary">No excess in these months.</Typography>
  return <Box sx={{ width: '100%', height: 320 }}>
    <BarChart
      height={320}
      xAxis={[{ scaleType: 'band', data: labels }]}
      yAxis={[{ width: 80, valueFormatter: (value: number) => value.toLocaleString('en-US') }]}
      series={series.map(item => ({ ...item, stack: 'excess', valueFormatter: (value: number | null) => money(value ?? 0) }))}
    />
  </Box>
}

function BillRangeChart({ data }: { data: BillingInsights }) {
  if (data.billRanges.every(band => band.accounts === 0)) return <Typography color="text.secondary">No bills in this batch.</Typography>
  return <Box sx={{ width: '100%', height: 320 }}>
    <BarChart
      height={320}
      xAxis={[{ scaleType: 'band', data: data.billRanges.map(band => band.label) }]}
      yAxis={[{ width: 50, tickMinStep: 1 }]}
      series={[{ label: 'Numbers', data: data.billRanges.map(band => band.accounts), color: brand.red, valueFormatter: (value, { dataIndex }) => `${value ?? 0} numbers · excess ${money(data.billRanges[dataIndex].calculatedExcess)}` }]}
      hideLegend
    />
  </Box>
}

function RepeatOverLimitTable({ repeat }: { repeat: InsightsRepeatOverLimit }) {
  if (repeat.accounts.length === 0) return <Alert severity="success">No number has gone over its limit in {repeat.minMonthsOver} or more of the last {repeat.windowMonths} months.</Alert>
  return <Stack spacing={1}>
    {repeat.totalCount > repeat.accounts.length && <Typography variant="body2" color="text.secondary">Showing the {repeat.accounts.length} with the most months over the limit, of {repeat.totalCount}.</Typography>}
    <TableContainer sx={{ overflowX: 'auto' }}>
      <Table size="small" aria-label="Repeat over-limit numbers">
        <TableHead><TableRow>
          <TableCell>Mobile</TableCell><TableCell>EPF</TableCell><TableCell>Employee</TableCell><TableCell>Factory</TableCell><TableCell>Package</TableCell>
          <TableCell align="right">Months over Limit</TableCell><TableCell align="right">Total Excess</TableCell><TableCell align="right">Average Excess</TableCell><TableCell align="right">This Batch</TableCell>
        </TableRow></TableHead>
        <TableBody>{repeat.accounts.map(item => <TableRow key={item.mobileNumber} hover>
          <TableCell>{item.mobileNumber}</TableCell>
          <TableCell>{item.epf}</TableCell>
          <TableCell>{item.employeeName}</TableCell>
          <TableCell>{item.factory}</TableCell>
          <TableCell>{item.packageCode ?? '—'}</TableCell>
          <TableCell align="right" sx={{ fontWeight: 700 }}>{item.monthsOverLimit} of {item.monthsBilled}</TableCell>
          <TableCell align="right" sx={{ fontWeight: 700, color: 'warning.main' }}>{money(item.totalExcess)}</TableCell>
          <TableCell align="right">{money(item.averageExcess)}</TableCell>
          <TableCell align="right">{item.selectedBatchExcess > 0 ? money(item.selectedBatchExcess) : 'Within limit'}</TableCell>
        </TableRow>)}</TableBody>
      </Table>
    </TableContainer>
  </Stack>
}

function CountDonut({ items, colors, ariaLabel, centerLabel, empty }: { items: InsightsCount[]; colors: Record<string, string>; ariaLabel: string; centerLabel: string; empty: string }) {
  if (items.length === 0) return <Typography color="text.secondary">{empty}</Typography>
  return <DonutChart ariaLabel={ariaLabel} centerLabel={centerLabel} formatTotal={value => String(value)} data={items.map((item, index) => ({ value: item.count, label: item.label, color: colors[item.key] ?? palette[index % palette.length] }))} />
}

function ChargeMixChart({ data }: { data: BillingInsights }) {
  const charges = data.chargeMix.filter(item => item.amount > 0)
  const credits = data.chargeMix.filter(item => item.amount < 0)
  if (charges.length === 0) return <Typography color="text.secondary">No charges recorded.</Typography>
  return <Stack spacing={1}>
    <DonutChart ariaLabel="Charge mix chart" centerLabel="LKR total charges" formatValue={money} data={charges.map((item, index) => ({ value: item.amount, label: item.label, color: palette[index % palette.length] }))} />
    {credits.length > 0 && <Typography variant="body2" color="text.secondary">Credits not shown in the chart: {credits.map(item => `${item.label} ${money(item.amount)}`).join(', ')}</Typography>}
  </Stack>
}

function ExcessSplitChart({ data }: { data: BillingInsights }) {
  const colors: Record<string, string> = { deducted: brand.purple, company: brand.orange, unassessed: '#9CA3AF' }
  if (data.excessSplit.length === 0) return <Typography color="text.secondary">No excess in this batch – every number stayed within its entitlement.</Typography>
  return <DonutChart ariaLabel="Excess split chart" centerLabel="LKR total excess" formatValue={money} data={data.excessSplit.map(item => ({ value: item.amount, label: item.label, color: colors[item.key] ?? brand.magenta }))} />
}

function GroupChart({ rows }: { rows: InsightsGroupRow[] }) {
  if (rows.length === 0) return <Typography color="text.secondary">No bills in this batch.</Typography>
  const height = Math.max(260, rows.length * 44 + 80)
  return <Box sx={{ width: '100%', height }}>
    <BarChart
      height={height}
      layout="horizontal"
      yAxis={[{ scaleType: 'band', data: rows.map(row => row.name), width: 170 }]}
      xAxis={[{ valueFormatter: (value: number) => value.toLocaleString('en-US') }]}
      series={[
        { label: 'Actual bill', data: rows.map(row => row.actualBill), color: brand.red, valueFormatter: value => money(value ?? 0) },
        { label: 'Entitlement', data: rows.map(row => row.entitlement), color: '#9CA3AF', valueFormatter: value => money(value ?? 0) },
        { label: 'Calculated excess', data: rows.map(row => row.calculatedExcess), color: brand.orange, valueFormatter: value => money(value ?? 0) },
      ]}
    />
  </Box>
}

function TopOverLimitTable({ data }: { data: BillingInsights }) {
  if (data.topOverLimit.length === 0) return <Alert severity="success">No number went over its entitlement in this batch.</Alert>
  return <TableContainer sx={{ overflowX: 'auto' }}>
    <Table size="small" aria-label="Top over-limit numbers">
      <TableHead><TableRow>
        <TableCell>#</TableCell><TableCell>Mobile</TableCell><TableCell>EPF</TableCell><TableCell>Employee</TableCell><TableCell>Calling Name</TableCell><TableCell>Factory</TableCell>
        <TableCell align="right">Actual Bill</TableCell><TableCell align="right">Entitlement</TableCell><TableCell align="right">Calculated Excess</TableCell>
        <TableCell>Responsibility</TableCell><TableCell>Reason</TableCell>
      </TableRow></TableHead>
      <TableBody>{data.topOverLimit.map((item, index) => <TableRow key={item.mobileNumber} hover>
        <TableCell>{index + 1}</TableCell>
        <TableCell>{item.mobileNumber}</TableCell>
        <TableCell>{item.epf}</TableCell>
        <TableCell>{item.employeeName}</TableCell>
        <TableCell>{item.callingName ?? '—'}</TableCell>
        <TableCell>{item.factory}</TableCell>
        <TableCell align="right">{money(item.actualBill)}</TableCell>
        <TableCell align="right">{money(item.entitlement)}</TableCell>
        <TableCell align="right" sx={{ fontWeight: 700, color: 'warning.main' }}>{money(item.calculatedExcess)}</TableCell>
        <TableCell>{item.responsibility === 'ByUser' ? 'By User' : item.responsibility === 'ByCompany' ? 'By Company' : 'Unassessed'}</TableCell>
        <TableCell>{item.remark ?? '—'}</TableCell>
      </TableRow>)}</TableBody>
    </Table>
  </TableContainer>
}
