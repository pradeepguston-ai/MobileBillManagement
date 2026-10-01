import { Box, Button, Card, CardContent, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import { useCallback, useEffect, useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'

import { getBatches, type BillBatchListItem } from '../api/billingApi'
import { getReviewSummary, type ReviewSummary } from '../api/billReviewApi'
import { batchContinuationRoute, billingPeriod } from '../billing/billingRoutes'
import { BatchStatusChip } from '../components/billing/BatchStatusChip'
import { CurrencyDisplay } from '../components/billing/CurrencyDisplay'
import { ValidationLevelChip } from '../components/billing/ValidationLevelChip'
import { WorkflowStepper } from '../components/billing/WorkflowStepper'
import { EmptyState } from '../components/common/EmptyState'
import { ErrorState } from '../components/common/ErrorState'
import { KpiCard } from '../components/common/KpiCard'
import { LoadingState } from '../components/common/LoadingState'
import { PageHeader } from '../components/common/PageHeader'

export function DashboardPage() {
  const [batches, setBatches] = useState<BillBatchListItem[]>([])
  const [summary, setSummary] = useState<ReviewSummary>()
  const [loading, setLoading] = useState(true)
  const [listError, setListError] = useState<string>()
  const [summaryError, setSummaryError] = useState<string>()

  const load = useCallback(async () => {
    setLoading(true); setListError(undefined); setSummaryError(undefined); setSummary(undefined)
    try {
      const result = await getBatches({ page: 1, pageSize: 5, sortBy: 'billingYear', sortDirection: 'desc' })
      setBatches(result.items)
      if (result.items[0]) {
        try { setSummary(await getReviewSummary(result.items[0].id)) }
        catch (reason) { setSummaryError(message(reason, 'The latest bill summary is unavailable.')) }
      }
    } catch (reason) { setBatches([]); setListError(message(reason, 'Unable to load the dashboard.')) }
    finally { setLoading(false) }
  }, [])

  useEffect(() => { const timer = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(timer) }, [load])

  const pageTitle = <PageHeader title="Dashboard" subtitle="Current billing progress and recent monthly batches." />
  if (loading) return <LoadingState label="Loading dashboard…" />
  if (listError) return <ErrorState message={listError} onRetry={() => void load()} />
  if (batches.length === 0) return <Stack spacing={2}>{pageTitle}<EmptyState message="No billing batches available yet." action={<Button component={RouterLink} to="/billing/new" variant="contained" startIcon={<AddIcon />}>Create Billing Batch</Button>} /></Stack>

  const latest = batches[0]
  return <Stack spacing={3}>
    {pageTitle}
    <Card><CardContent><Stack spacing={2}>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={1} sx={{ justifyContent: 'space-between', alignItems: { md: 'center' } }}><Box><Typography variant="overline" color="text.secondary">Current / Latest Batch</Typography><Typography component="h2" variant="h4">{billingPeriod(latest.billingYear, latest.billingMonth)}</Typography><Typography color="text.secondary">{latest.provider}</Typography></Box><Stack direction="row" spacing={1}><ValidationLevelChip level={latest.validationLevel} /><BatchStatusChip status={latest.status} /></Stack></Stack>
      {summaryError && <ErrorState message={summaryError} />}
      {summary && <><Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: 2 }}><KpiCard label="Total Actual Bill" value={<CurrencyDisplay value={summary.totalActualBill} />} /><KpiCard label="Final User Deduction" value={<CurrencyDisplay value={summary.totalFinalDeduction} />} /><KpiCard label="Exception Count" value={summary.exceptionCount} /><KpiCard label="Unassessed Count" value={summary.unassessedCount} /></Box><WorkflowStepper status={summary.batchStatus} compact /></>}
      <Button component={RouterLink} to={batchContinuationRoute(latest.id, latest.status)} variant="contained" sx={{ alignSelf: 'flex-start' }}>Open</Button>
    </Stack></CardContent></Card>
    <Box><Typography component="h2" variant="h5" sx={{ mb: 1 }}>Recent Billing Batches</Typography><TableContainer sx={{ overflowX: 'auto' }}><Table size="small" stickyHeader aria-label="Recent billing batches"><TableHead><TableRow><TableCell>Billing Period</TableCell><TableCell>Provider</TableCell><TableCell align="right">Total</TableCell><TableCell>Status</TableCell><TableCell /></TableRow></TableHead><TableBody>{batches.map(batch => <TableRow hover key={batch.id}><TableCell sx={{ whiteSpace: 'nowrap' }}>{billingPeriod(batch.billingYear, batch.billingMonth)}</TableCell><TableCell>{batch.provider}</TableCell><TableCell align="right"><CurrencyDisplay value={batch.calculatedGrandTotal} /></TableCell><TableCell><BatchStatusChip status={batch.status} /></TableCell><TableCell><Button component={RouterLink} to={batchContinuationRoute(batch.id, batch.status)}>Open</Button></TableCell></TableRow>)}</TableBody></Table></TableContainer></Box>
  </Stack>
}

function message(reason: unknown, fallback: string) { return reason instanceof Error ? reason.message : fallback }
