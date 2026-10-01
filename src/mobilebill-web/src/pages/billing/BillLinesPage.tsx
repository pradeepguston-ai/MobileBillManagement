import { Button, Pagination, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField } from '@mui/material'
import SearchIcon from '@mui/icons-material/Search'
import { type FormEvent, useCallback, useEffect, useState } from 'react'
import { useParams } from 'react-router-dom'

import { getBatch, getBillLines, type BillBatchStatus, type BillLine } from '../../api/billingApi'
import { billingPeriod } from '../../billing/billingRoutes'
import { BatchContextNavigation } from '../../components/billing/BatchContextNavigation'
import { formatCurrency } from '../../utils/formatters'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { LoadingState } from '../../components/common/LoadingState'
import { PageHeader } from '../../components/common/PageHeader'

const moneyColumns = new Set<keyof BillLine>(['previousDueAmount', 'payments', 'totalUsageCharges', 'idd', 'roaming', 'valueAddedServices', 'discounts', 'billAdjustmentsBalanceTransfers', 'commitmentCharges', 'latePaymentCharges', 'addToBill', 'instalmentPlans', 'governmentTaxesAndLevies', 'vat', 'chargesForBillPeriod', 'totalDueAmount'])
const columns: Array<[keyof BillLine, string]> = [
  ['mobileNumber', 'Mobile Account'], ['previousDueAmount', 'Previous Due'], ['payments', 'Payments'], ['totalUsageCharges', 'Total Usage'], ['idd', 'IDD'], ['roaming', 'Roaming'], ['valueAddedServices', 'VAS'], ['discounts', 'Discounts'], ['billAdjustmentsBalanceTransfers', 'Bill Adjustments'], ['commitmentCharges', 'Commitment Charges'], ['latePaymentCharges', 'Late Payment Charges'], ['addToBill', 'Add To Bill'], ['instalmentPlans', 'Instalment Plans'], ['governmentTaxesAndLevies', 'Government Taxes / Levies'], ['vat', 'VAT'], ['chargesForBillPeriod', 'Charges For Bill Period'], ['totalDueAmount', 'Total Due'], ['extractionStatus', 'Extraction Status'], ['pageNumber', 'Page Number'], ['extractionError', 'Extraction Error'],
]

export function BillLinesPage() {
  const { batchId = '' } = useParams()
  const [period, setPeriod] = useState('Billing period')
  const [batchStatus, setBatchStatus] = useState<BillBatchStatus>('Draft')
  const [items, setItems] = useState<BillLine[]>([])
  const [page, setPage] = useState(1)
  const [totalPages, setTotalPages] = useState(0)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string>()
  const load = useCallback(async () => {
    setLoading(true); setError(undefined)
    try { const result = await getBillLines(batchId, page, 20, search); setItems(result.items); setTotalPages(result.totalPages) }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load extracted bill lines.') }
    finally { setLoading(false) }
  }, [batchId, page, search])
  useEffect(() => { void getBatch(batchId).then(batch => { setPeriod(billingPeriod(batch.billingYear, batch.billingMonth)); setBatchStatus(batch.status) }).catch(() => undefined) }, [batchId])
  useEffect(() => { const timer = window.setTimeout(() => { void load() }, 0); return () => window.clearTimeout(timer) }, [load])
  const submitSearch = (event: FormEvent) => { event.preventDefault(); setPage(1); setSearch(searchInput.trim()) }

  return <Stack spacing={2}>
    <PageHeader title="Extracted Bill Lines" subtitle={`${period} · Values shown exactly as persisted from the PDF extraction.`} />
    <BatchContextNavigation batchId={batchId} status={batchStatus} />
    <Stack component="form" direction={{ xs: 'column', sm: 'row' }} spacing={1} onSubmit={submitSearch}><TextField size="small" label="Search mobile" value={searchInput} onChange={event => setSearchInput(event.target.value)} /><Button type="submit" variant="outlined" startIcon={<SearchIcon />}>Search</Button></Stack>
    {error && <ErrorState message={error} />}{loading && <LoadingState label="Loading extracted lines…" />}
    {!loading && items.length === 0 && <EmptyState message="No extracted lines match this search." />}
    {!loading && items.length > 0 && <TableContainer sx={{ maxWidth: '100%', overflowX: 'auto' }}><Table stickyHeader size="small" aria-label="Extracted bill lines" sx={{ minWidth: 2600 }}>
      <TableHead><TableRow>{columns.map(([key, label]) => <TableCell key={key} align={moneyColumns.has(key) ? 'right' : 'left'} sx={{ whiteSpace: 'nowrap' }}>{label}</TableCell>)}</TableRow></TableHead>
      <TableBody>{items.map(line => <TableRow hover key={line.id} sx={{ bgcolor: line.extractionStatus !== 'Extracted' ? 'rgba(211, 47, 47, 0.08)' : undefined }}>{columns.map(([key]) => <TableCell key={key} align={moneyColumns.has(key) ? 'right' : 'left'} sx={{ whiteSpace: 'nowrap' }}>{moneyColumns.has(key) ? formatCurrency(Number(line[key] ?? 0)) : String(line[key] ?? '—')}</TableCell>)}</TableRow>)}</TableBody>
    </Table></TableContainer>}
    {totalPages > 0 && <Pagination count={totalPages} page={page} onChange={(_, value) => setPage(value)} />}
  </Stack>
}
