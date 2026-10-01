import { Alert, Box, Card, CardContent, FormControl, InputLabel, MenuItem, Select, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, ToggleButton, ToggleButtonGroup, Typography } from '@mui/material'
import { BarChart, LineChart, PieChart } from '@mui/x-charts'
import { useCallback, useEffect, useState, type ReactNode } from 'react'

import { getBillingInsights, type BillingInsights, type InsightsBatchOption, type InsightsGroupRow, type InsightsKpis } from '../../api/insightsApi'
import { billingPeriod } from '../../billing/billingRoutes'
import { brand } from '../../theme/theme'
import { formatCurrency } from '../../utils/formatters'
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
      <ChartCard title="Where the money goes" subtitle="Charges on the PDF for this batch, by type.">
        <ChargeMixChart data={data} />
      </ChartCard>
      <ChartCard title="Who pays the excess" subtitle={`Calculated excess ${money(current.totalCalculatedExcess)}, split by responsibility.`}>
        <ExcessSplitChart data={data} />
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
  </Stack>
}

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

function ChargeMixChart({ data }: { data: BillingInsights }) {
  const charges = data.chargeMix.filter(item => item.amount > 0)
  const credits = data.chargeMix.filter(item => item.amount < 0)
  if (charges.length === 0) return <Typography color="text.secondary">No charges recorded.</Typography>
  return <Stack spacing={1}>
    <Box sx={{ width: '100%', height: 300 }}>
      <PieChart height={300} series={[{ innerRadius: 60, paddingAngle: 1, cornerRadius: 3, valueFormatter: item => money(item.value), data: charges.map((item, index) => ({ id: item.key, value: item.amount, label: item.label, color: palette[index % palette.length] })) }]} />
    </Box>
    {credits.length > 0 && <Typography variant="body2" color="text.secondary">Credits not shown in the chart: {credits.map(item => `${item.label} ${money(item.amount)}`).join(', ')}</Typography>}
  </Stack>
}

function ExcessSplitChart({ data }: { data: BillingInsights }) {
  const colors: Record<string, string> = { deducted: brand.purple, waived: brand.indigo, companyRoaming: brand.orange, company: brand.red, unassessed: '#9CA3AF' }
  if (data.excessSplit.length === 0) return <Typography color="text.secondary">No excess in this batch – every number stayed within its entitlement.</Typography>
  return <Box sx={{ width: '100%', height: 300 }}>
    <PieChart height={300} series={[{ innerRadius: 60, paddingAngle: 1, cornerRadius: 3, valueFormatter: item => money(item.value), data: data.excessSplit.map(item => ({ id: item.key, value: item.amount, label: item.label, color: colors[item.key] ?? brand.magenta })) }]} />
  </Box>
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
