import { Button, Stack } from '@mui/material'
import { Link as RouterLink } from 'react-router-dom'

import type { BillBatchStatus } from '../../api/billingApi'
import { canDownloadReport } from '../../billing/billingRoutes'

const linesStatuses = new Set<BillBatchStatus>(['Parsed', 'ValidationFailed', 'Validated', 'ITReview', 'HRApproval', 'FinanceApproval', 'Completed', 'Locked'])
const reviewStatuses = new Set<BillBatchStatus>(['Validated', 'ITReview', 'HRApproval', 'FinanceApproval', 'Completed', 'Locked'])

export function BatchContextNavigation({ batchId, status }: { batchId: string; status: BillBatchStatus }) {
  return <Stack component="nav" aria-label="Billing batch navigation" direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { sm: 'center' } }}>
    <Button component={RouterLink} to={`/billing/${batchId}/process`}>Processing</Button>
    {linesStatuses.has(status) && <Button component={RouterLink} to={`/billing/${batchId}/lines`}>Extracted Lines</Button>}
    {reviewStatuses.has(status) && <Button component={RouterLink} to={`/billing/${batchId}/exceptions`}>Exceptions</Button>}
    {reviewStatuses.has(status) && <Button component={RouterLink} to={`/billing/${batchId}/review`}>Review</Button>}
    {canDownloadReport(status) && <Button component={RouterLink} to="/reports/monthly-bill">Report</Button>}
  </Stack>
}
