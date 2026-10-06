import { Alert, Box, Button, Card, CardContent, Chip, FormControlLabel, MenuItem, Stack, Switch, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from '@mui/material'
import DownloadIcon from '@mui/icons-material/Download'
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdf'
import { useEffect, useMemo, useState } from 'react'

import { downloadVasReport, getVasBatches, getVasReport, type VasBatch, type VasFilters, type VasGroup, type VasReport } from '../api/vasReportApi'
import { billingPeriod } from '../billing/billingRoutes'
import { ReportCategorySelect, ReportFactorySelect, ReportSectionSelect } from '../components/billing/ReportFactorySelect'
import { EmptyState } from '../components/common/EmptyState'
import { ErrorState } from '../components/common/ErrorState'
import { KpiCard } from '../components/common/KpiCard'
import { LoadingState } from '../components/common/LoadingState'
import { PageHeader } from '../components/common/PageHeader'
import { formatCurrency } from '../utils/formatters'

// Numbers with Value Added Services charges in a matched batch: who has them, how much, and who keeps having them.
export function VasReportPage() {
  const [batches, setBatches] = useState<VasBatch[]>()
  const [batchId, setBatchId] = useState('')
  const [factoryCodes, setFactoryCodes] = useState<string[]>([])
  const [categoryCodes, setCategoryCodes] = useState<string[]>([])
  const [sectionCodes, setSectionCodes] = useState<string[]>([])
  const [minimumVas, setMinimumVas] = useState('')
  const [repeatOnly, setRepeatOnly] = useState(false)
  const [report, setReport] = useState<VasReport>()
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string>()
  const [downloading, setDownloading] = useState(false)
  const filters = useMemo<VasFilters>(() => ({ factoryCodes, categoryCodes, sectionCodes, repeatOnly, minimumVas: minimumVas.trim() !== '' && Number.isFinite(Number(minimumVas)) ? Number(minimumVas) : undefined }), [factoryCodes, categoryCodes, sectionCodes, repeatOnly, minimumVas])

  useEffect(() => {
    let active = true
    getVasBatches()
      .then(items => { if (active) { setBatches(items); setBatchId(current => current || items[0]?.batchId || '') } })
      .catch(reason => { if (active) { setBatches([]); setError(message(reason, 'Unable to load billing batches.')) } })
    return () => { active = false }
  }, [])

  useEffect(() => {
    if (!batchId) return
    let active = true
    const timer = window.setTimeout(() => {
      setLoading(true); setError(undefined)
      getVasReport(batchId, filters)
        .then(result => { if (active) setReport(result) })
        .catch(reason => { if (active) { setReport(undefined); setError(message(reason, 'Unable to load the VAS report.')) } })
        .finally(() => { if (active) setLoading(false) })
    }, 250)
    return () => { active = false; window.clearTimeout(timer) }
  }, [batchId, filters])

  const download = async (format: 'excel' | 'pdf') => {
    setDownloading(true); setError(undefined)
    try { await downloadVasReport(batchId, format, filters) } catch (reason) { setError(message(reason, 'Unable to download the VAS report.')) } finally { setDownloading(false) }
  }

  return <Stack spacing={2}>
    <PageHeader title="VAS Report" subtitle="Numbers with Value Added Services charges, how much of the bill they are, and repeat users (VAS in 2 or more of the last 3 months)." />
    {batches?.length === 0 && !error && <EmptyState message="No matched billing batch yet. The VAS report is available once a batch has been matched to employees." />}
    {batches && batches.length > 0 && <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ flexWrap: 'wrap', rowGap: 1.5, alignItems: { md: 'center' } }}>
      <TextField select size="small" label="Billing period" value={batchId} onChange={event => setBatchId(event.target.value)} sx={{ minWidth: 240 }}>
        {batches.map(batch => <MenuItem key={batch.batchId} value={batch.batchId}>{billingPeriod(batch.billingYear, batch.billingMonth)} · {batch.provider}{batch.isPreliminary ? ' (Preliminary)' : ''}</MenuItem>)}
      </TextField>
      <ReportFactorySelect value={factoryCodes} onChange={setFactoryCodes} />
      <ReportCategorySelect value={categoryCodes} onChange={setCategoryCodes} />
      <ReportSectionSelect value={sectionCodes} onChange={setSectionCodes} />
      <TextField size="small" type="number" label="Minimum VAS" value={minimumVas} onChange={event => setMinimumVas(event.target.value)} sx={{ width: 140 }} slotProps={{ htmlInput: { min: 0, step: '0.01' } }} />
      <FormControlLabel control={<Switch checked={repeatOnly} onChange={event => setRepeatOnly(event.target.checked)} />} label="Repeat users only" />
    </Stack>}
    {batches && batches.length > 0 && <Stack direction="row" spacing={1.5} sx={{ flexWrap: 'wrap', rowGap: 1.5 }}>
      <Button variant="contained" startIcon={<DownloadIcon />} disabled={!batchId || downloading} onClick={() => void download('excel')}>Download Excel</Button>
      <Button variant="outlined" startIcon={<PictureAsPdfIcon />} disabled={!batchId || downloading} onClick={() => void download('pdf')}>Download PDF</Button>
    </Stack>}
    {error && <ErrorState message={error} />}
    {report?.isPreliminary && <Alert severity="info">Preliminary: this batch is {report.batchStatus === 'Validated' ? 'matched but not yet submitted' : 'still being approved'}, so responsibilities and deductions can still change.</Alert>}
    {loading && !report && <LoadingState label="Loading VAS report…" />}
    {report && <>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'repeat(2, minmax(0, 1fr))', md: 'repeat(3, minmax(0, 1fr))' }, gap: 2 }}>
        <KpiCard label="Numbers with VAS" value={<>{report.users}{change(report.users, report.previousUsers, value => String(value))}</>} />
        <KpiCard label="Total VAS" value={<>{formatCurrency(report.totalVas)}{change(report.totalVas, report.previousTotalVas, formatCurrency)}</>} />
        <KpiCard label="Repeat users" value={report.repeatUsers} />
      </Box>
      {report.rows.length > 0 && <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' }, gap: 2 }}>
        <GroupTable title="By Factory" groups={report.byFactory} />
        <GroupTable title="By Department" groups={report.byDepartment} />
      </Box>}
      {report.rows.length === 0 ? <EmptyState message="No numbers with VAS match the selected filters." /> : <TableContainer sx={{ maxHeight: 640, overflow: 'auto' }}>
        <Table stickyHeader size="small" aria-label="Numbers with VAS">
          <TableHead><TableRow>{['Mobile', 'EPF', 'Name', 'Factory', 'Department', 'Section', 'VAS', 'Actual Bill', 'VAS %', 'Responsibility', 'Months with VAS', 'Note'].map(heading => <TableCell key={heading} align={['VAS', 'Actual Bill', 'VAS %'].includes(heading) ? 'right' : 'left'} sx={{ whiteSpace: 'nowrap' }}>{heading}</TableCell>)}</TableRow></TableHead>
          <TableBody>{report.rows.map(row => <TableRow hover key={row.mobileNumber}>
            <TableCell>{row.mobileNumber}</TableCell>
            <TableCell>{row.epf ?? '—'}</TableCell>
            <TableCell>{row.employeeName ?? '—'}</TableCell>
            <TableCell>{row.factory ?? '—'}</TableCell>
            <TableCell>{row.department ?? '—'}</TableCell>
            <TableCell>{row.section ?? '—'}</TableCell>
            <TableCell align="right" sx={{ fontWeight: 700 }}>{formatCurrency(row.vas)}</TableCell>
            <TableCell align="right">{formatCurrency(row.actualBill)}</TableCell>
            <TableCell align="right">{row.vasShareOfBill.toFixed(1)}%</TableCell>
            <TableCell>{row.responsibility === 'ByUser' ? 'By User' : row.responsibility === 'ByCompany' ? 'By Company' : 'Unassessed'}</TableCell>
            <TableCell sx={{ whiteSpace: 'nowrap' }}>{row.monthsWithVas} of {row.monthsConsidered}{row.isRepeat && <Chip size="small" color="warning" label="Repeat" sx={{ ml: 1 }} />}</TableCell>
            <TableCell>{!row.isMatched ? <Chip size="small" label="Not matched" /> : row.isPooled ? <Chip size="small" label="SIM Pool" /> : '—'}</TableCell>
          </TableRow>)}</TableBody>
        </Table>
      </TableContainer>}
    </>}
  </Stack>
}

function GroupTable({ title, groups }: { title: string; groups: VasGroup[] }) {
  return <Card variant="outlined"><CardContent>
    <Typography variant="subtitle1" sx={{ mb: 1 }}>{title}</Typography>
    <Box sx={{ maxHeight: 240, overflow: 'auto' }}>
      <Table size="small" aria-label={`VAS ${title.toLowerCase()}`}>
        <TableHead><TableRow><TableCell>Name</TableCell><TableCell align="right">Numbers</TableCell><TableCell align="right">Total VAS</TableCell></TableRow></TableHead>
        <TableBody>{groups.map(group => <TableRow key={group.name}><TableCell>{group.name}</TableCell><TableCell align="right">{group.users}</TableCell><TableCell align="right">{formatCurrency(group.totalVas)}</TableCell></TableRow>)}</TableBody>
      </Table>
    </Box>
  </CardContent></Card>
}

// The change against the previous matched month, shown under the KPI value.
function change(current: number, previous: number | null, format: (value: number) => string) {
  if (previous === null) return null
  const difference = current - previous
  const label = difference === 0 ? 'no change from last month' : `${difference > 0 ? '+' : '−'}${format(Math.abs(difference))} vs last month`
  return <Typography component="span" variant="caption" color="text.secondary" sx={{ display: 'block' }}>{label}</Typography>
}

function message(reason: unknown, fallback: string) { return reason instanceof Error ? reason.message : fallback }
