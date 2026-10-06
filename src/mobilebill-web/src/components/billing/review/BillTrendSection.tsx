import { Alert, Box, Button, Chip, Collapse, MenuItem, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material'
import { LineChart } from '@mui/x-charts'
import { useEffect, useState } from 'react'

import { getBillTrend, type BillTrend, type BillTrendPoint, type BillTrendScope } from '../../../api/billReviewApi'
import { brand } from '../../../theme/theme'
import { formatCurrency } from '../../../utils/formatters'

const scopeLabels: Record<BillTrendScope, string> = { ThisNumber: 'This number, this employee', Employee: 'All this employee\'s numbers', AllHolders: 'This number, all holders' }
const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

// Month-by-month history behind the reviewed bill: actual bill against entitlement, with the final deduction.
export function BillTrendSection({ batchId, rowId }: { batchId: string; rowId: string }) {
  const [scope, setScope] = useState<BillTrendScope>('ThisNumber')
  const [months, setMonths] = useState(12)
  const [trend, setTrend] = useState<BillTrend>()
  const [error, setError] = useState<string>()
  const [showTable, setShowTable] = useState(false)

  useEffect(() => {
    let active = true
    setError(undefined)
    getBillTrend(batchId, rowId, scope, months)
      .then(result => { if (!Array.isArray(result?.points)) throw new Error('Unable to load the bill trend.'); if (active) setTrend(result) })
      .catch(reason => { if (active) { setTrend(undefined); setError(reason instanceof Error ? reason.message : 'Unable to load the bill trend.') } })
    return () => { active = false }
  }, [batchId, rowId, scope, months])

  return <Box>
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { sm: 'center' }, mb: 1 }}>
      <Typography variant="subtitle1" sx={{ flexGrow: 1 }}>Bill Trend</Typography>
      <TextField select size="small" label="History of" value={scope} onChange={event => setScope(event.target.value as BillTrendScope)} sx={{ minWidth: 230 }}>
        {(Object.keys(scopeLabels) as BillTrendScope[]).map(key => <MenuItem key={key} value={key}>{scopeLabels[key]}</MenuItem>)}
      </TextField>
      <TextField select size="small" label="Months" value={months} onChange={event => setMonths(Number(event.target.value))} sx={{ width: 100 }}>
        {[6, 12, 24].map(value => <MenuItem key={value} value={value}>{value}</MenuItem>)}
      </TextField>
    </Stack>
    {error && <Alert severity="error">{error}</Alert>}
    {!trend && !error && <Typography variant="body2" color="text.secondary">Loading bill trend…</Typography>}
    {trend && <TrendBody trend={trend} showTable={showTable} onToggleTable={() => setShowTable(value => !value)} />}
  </Box>
}

function TrendBody({ trend, showTable, onToggleTable }: { trend: BillTrend; showTable: boolean; onToggleTable: () => void }) {
  const labels = trend.points.map(pointLabel)
  return <Stack spacing={1}>
    <Summary trend={trend} />
    {trend.points.length > 1 && <Box aria-label="Bill trend chart">
      <LineChart
        height={260}
        margin={{ left: 8, right: 8 }}
        xAxis={[{ scaleType: 'point', data: labels }]}
        yAxis={[{ width: 70, valueFormatter: (value: number) => value.toLocaleString('en-US') }]}
        series={[
          { id: 'actual', label: 'Actual Bill', data: trend.points.map(point => point.actualBill), color: brand.red, valueFormatter: value => formatCurrency(value ?? 0) },
          { id: 'entitlement', label: 'Entitlement', data: trend.points.map(point => point.entitlement), color: '#9CA3AF', curve: 'stepAfter', showMark: false, valueFormatter: value => formatCurrency(value ?? 0) },
          { id: 'deduction', label: 'Final Deduction', data: trend.points.map(point => point.finalDeduction), color: brand.purple, area: true, showMark: false, valueFormatter: value => formatCurrency(value ?? 0) },
        ]}
      />
      <Typography variant="caption" color="text.secondary">● marks the month being reviewed. * Preliminary: that batch is not yet fully approved.{trend.points.some(point => point.isOtherHolder) ? ' † held by another person.' : ''}</Typography>
    </Box>}
    {trend.points.length > 0 && <Box>
      <Button size="small" onClick={onToggleTable}>{showTable ? 'Hide monthly figures' : 'Show monthly figures'}</Button>
      <Collapse in={showTable} unmountOnExit>
        <Box sx={{ overflowX: 'auto' }}>
          <Table size="small" aria-label="Bill trend by month">
            <TableHead><TableRow>{['Month', 'Actual Bill', 'Entitlement', 'Excess', 'VAS', 'Responsibility', 'Final Deduction', 'Holder'].map(heading => <TableCell key={heading} sx={{ whiteSpace: 'nowrap' }}>{heading}</TableCell>)}</TableRow></TableHead>
            <TableBody>{[...trend.points].reverse().map(point => <TableRow key={`${point.billingYear}-${point.billingMonth}`} selected={point.isCurrent}>
              <TableCell sx={{ whiteSpace: 'nowrap' }}>{pointLabel(point)}</TableCell>
              <TableCell sx={{ fontWeight: point.isOverLimit ? 700 : undefined, color: point.isOverLimit ? 'error.main' : undefined }}>{formatCurrency(point.actualBill)}</TableCell>
              <TableCell>{formatCurrency(point.entitlement)}</TableCell>
              <TableCell>{formatCurrency(point.calculatedExcess)}</TableCell>
              <TableCell>{formatCurrency(point.vas)}</TableCell>
              <TableCell>{responsibilityLabel(point.responsibility)}</TableCell>
              <TableCell>{formatCurrency(point.finalDeduction)}</TableCell>
              <TableCell>{point.holderEpf} — {point.holderName}{point.numbers > 1 ? ` (${point.numbers} numbers)` : ''}</TableCell>
            </TableRow>)}</TableBody>
          </Table>
        </Box>
      </Collapse>
    </Box>}
  </Stack>
}

function Summary({ trend }: { trend: BillTrend }) {
  if (trend.average === null) return <Typography variant="body2" color="text.secondary">First month, no history yet.</Typography>
  const change = trend.changePercent === null ? '' : ` (${trend.changePercent > 0 ? '+' : ''}${trend.changePercent.toFixed(1)}%)`
  const highest = trend.highestYear && trend.highestMonth ? ` · Highest ${monthNames[trend.highestMonth - 1]} ${trend.highestYear}` : ''
  return <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap', rowGap: 0.5 }}>
    <Typography variant="body2">
      Average of earlier months {formatCurrency(trend.average)} · This month {formatCurrency(trend.currentActualBill)}{change} · Over limit {trend.monthsOverLimit} of {trend.points.length} months{highest}
    </Typography>
    {trend.isAboveUsual && <Chip size="small" color="warning" label={`Above usual (more than ${trend.aboveUsualThresholdPercent}% over average)`} />}
  </Stack>
}

function pointLabel(point: BillTrendPoint) {
  return `${monthNames[point.billingMonth - 1]} ${String(point.billingYear).slice(2)}${point.isCurrent ? ' ●' : ''}${point.isPreliminary ? '*' : ''}${point.isOtherHolder ? '†' : ''}`
}

function responsibilityLabel(value: string | null) {
  return value === 'ByUser' ? 'By User' : value === 'ByCompany' ? 'By Company' : value === 'Mixed' ? 'Mixed' : 'Unassessed'
}
