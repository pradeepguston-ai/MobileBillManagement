import { Alert, Button, Card, CardContent, CircularProgress, Grid, Stack, Step, StepLabel, Stepper, Typography } from '@mui/material'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link as RouterLink, useNavigate, useParams } from 'react-router-dom'

import { getBatch, matchBillBatch, parseBill, uploadBill, validateBill, type BillBatch } from '../../api/billingApi'
import { ApiError } from '../../api/http'
import { BatchSummary } from '../../components/billing/BatchSummary'
import { BatchContextNavigation } from '../../components/billing/BatchContextNavigation'
import { formatCurrency } from '../../utils/formatters'
import { PdfDropZone } from '../../components/billing/PdfDropZone'
import { ErrorState } from '../../components/common/ErrorState'
import { KpiCard } from '../../components/common/KpiCard'
import { LoadingState } from '../../components/common/LoadingState'
import { PageHeader } from '../../components/common/PageHeader'

const steps = ['Create Batch', 'Upload PDF', 'Parse Bill', 'Validate', 'Match', 'Review']

export function BillingProcessingPage() {
  const { batchId = '' } = useParams()
  const navigate = useNavigate()
  const [batch, setBatch] = useState<BillBatch>()
  const [file, setFile] = useState<File>()
  const [loading, setLoading] = useState(true)
  const [working, setWorking] = useState(false)
  const [error, setError] = useState<string>()
  const load = useCallback(async () => { try { setError(undefined); setBatch(await getBatch(batchId)) } catch (reason) { setError(message(reason, 'Unable to load billing batch.')) } finally { setLoading(false) } }, [batchId])
  useEffect(() => { const timer = window.setTimeout(() => { void load() }, 0); return () => window.clearTimeout(timer) }, [load])
  const activeStep = useMemo(() => stepForStatus(batch?.status), [batch?.status])
  const run = async (operation: () => Promise<BillBatch>) => { setWorking(true); setError(undefined); try { setBatch(await operation()) } catch (reason) { setError(message(reason, 'Unable to continue processing.')) } finally { setWorking(false) } }
  const upload = () => { if (!file) { setError('Select a PDF file before uploading.'); return } if (!file.name.toLowerCase().endsWith('.pdf') || (file.type && file.type !== 'application/pdf')) { setError('Only PDF files can be uploaded.'); return } void run(() => uploadBill(batchId, file)) }
  const matchAndReview = async () => {
    setWorking(true); setError(undefined)
    try { await matchBillBatch(batchId) }
    catch (reason) {
      // A 409 here means this batch was already matched (e.g. a previous click succeeded but the
      // page didn't navigate) — that's not a failure, so continue on to the review screen.
      if (!(reason instanceof ApiError && reason.status === 409)) { setError(message(reason, 'Unable to match bill lines to employees.')); setWorking(false); return }
    }
    navigate(`/billing/${batchId}/review`)
  }

  const batchIdForActions = batchId
  if (loading) return <LoadingState label="Loading billing batch…" />
  if (!batch) return <ErrorState message={error ?? 'Billing batch was not found.'} />
  return <Stack spacing={3}>
    <PageHeader title="Process Billing Batch" subtitle="Upload, extract and validate the provider bill." />
    <BatchSummary batch={batch} />
    <BatchContextNavigation batchId={batchId} status={batch.status} />
    <Stepper activeStep={activeStep} alternativeLabel sx={{ overflowX: 'auto' }}>{steps.map(step => <Step key={step}><StepLabel>{step}</StepLabel></Step>)}</Stepper>
    {error && <ErrorState message={error} />}
    {batch.validationWarning && <Alert severity="warning">{batch.validationWarning}</Alert>}
    {batch.warnings?.filter(warning => warning !== batch.validationWarning).map(warning => <Alert severity="warning" key={warning}>{warning}</Alert>)}
    {batch.status === 'Draft' && <Card variant="outlined"><CardContent><Stack spacing={2}><Typography variant="h6">Upload PDF</Typography><PdfDropZone file={file} disabled={working} onChange={setFile} /><Button variant="contained" disabled={working || !file} onClick={upload}>{working ? 'Uploading…' : 'Upload PDF'}</Button></Stack></CardContent></Card>}
    {batch.status === 'Uploaded' && <ActionCard title="Parse Bill" description="Extract every account row and its PDF charge fields." button="Parse Bill" working={working} onClick={() => void run(() => parseBill(batchIdForActions))} />}
    {hasParseResult(batch) && <ParseSummary batch={batch} />}
    {(batch.status === 'Parsed' || batch.status === 'ValidationFailed') && <ActionCard title="Validate Bill" description="Validate the persisted extraction result. Totals are supplied by the server." button={batch.status === 'ValidationFailed' ? 'Retry Validation' : 'Validate Bill'} working={working} onClick={() => void run(() => validateBill(batchIdForActions))} />}
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
      <Button component={RouterLink} to={`/billing/${batchId}/lines`} variant="outlined" disabled={batch.status === 'Draft' || batch.status === 'Uploaded'}>View Extracted Lines</Button>
      {isReviewReady(batch.status) && <Button component={RouterLink} to={`/billing/${batchId}/exceptions`} variant="outlined">Review Exceptions</Button>}
      {batch.status === 'Validated' && <Button variant="contained" disabled={working} onClick={() => void matchAndReview()}>{working ? 'Matching…' : 'Match Records & Open Review'}</Button>}
      {isReviewReady(batch.status) && batch.status !== 'Validated' && <Button component={RouterLink} to={`/billing/${batchId}/review`} variant="contained">Open Bill Review</Button>}
    </Stack>
  </Stack>
}

function ActionCard({ title, description, button, working, onClick }: { title: string; description: string; button: string; working: boolean; onClick: () => void }) {
  return <Card variant="outlined"><CardContent><Stack spacing={1} sx={{ alignItems: 'flex-start' }}><Typography variant="h6">{title}</Typography><Typography color="text.secondary">{description}</Typography><Button variant="contained" onClick={onClick} disabled={working}>{working && <CircularProgress size={18} sx={{ mr: 1 }} />}{button}</Button></Stack></CardContent></Card>
}

function ParseSummary({ batch }: { batch: BillBatch }) {
  const values = [['Total candidates', String(batch.totalCandidates)], ['Successfully parsed', String(batch.successfulCount)], ['Failed', String(batch.failedCount)], ['Calculated grand total', money(batch.calculatedGrandTotal)], ...(batch.statedGrandTotal == null ? [] : [['Stated grand total', money(batch.statedGrandTotal)]]), ...(batch.difference == null ? [] : [['Difference', money(batch.difference)]]), ['Validation level', batch.validationLevel]]
  return <Grid container spacing={2}>{values.map(([label, value]) => <Grid key={label} size={{ xs: 12, sm: 6, md: 3 }}><KpiCard label={label} value={value} /></Grid>)}</Grid>
}

function stepForStatus(status?: string) { if (!status || status === 'Draft') return 1; if (status === 'Uploaded') return 2; if (status === 'Parsed' || status === 'ValidationFailed') return 3; if (status === 'Validated') return 4; return 5 }
function hasParseResult(batch: BillBatch) { return !['Draft', 'Uploaded'].includes(batch.status) || batch.totalCandidates > 0 }
function isReviewReady(status: string) { return ['Validated', 'ITReview', 'HRApproval', 'FinanceApproval', 'Completed', 'Locked'].includes(status) }
function money(value: number | null) { return formatCurrency(value) }
function message(reason: unknown, fallback: string) { return reason instanceof Error ? reason.message : fallback }
