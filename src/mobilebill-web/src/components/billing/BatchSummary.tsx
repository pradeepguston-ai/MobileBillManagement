import { Card, CardContent, Stack, Typography } from '@mui/material'

import type { BillBatch } from '../../api/billingApi'
import { billingPeriod } from '../../billing/billingRoutes'
import { BatchStatusChip } from './BatchStatusChip'
import { ValidationLevelChip } from './ValidationLevelChip'

export function BatchSummary({ batch }: { batch: BillBatch }) {
  return <Card variant="outlined"><CardContent>
    <Stack direction={{ xs: 'column', md: 'row' }} spacing={3} sx={{ alignItems: { md: 'center' } }}>
      <SummaryValue label="Billing Period" value={billingPeriod(batch.billingYear, batch.billingMonth)} />
      <SummaryValue label="Provider" value={batch.providerName ?? '—'} />
      <SummaryValue label="Corporate Code" value={batch.corporateCode} />
      <Stack spacing={0.5}><Typography variant="caption" color="text.secondary">Batch Status</Typography><BatchStatusChip status={batch.status} /></Stack>
      <Stack spacing={0.5}><Typography variant="caption" color="text.secondary">Validation Level</Typography><ValidationLevelChip level={batch.validationLevel} /></Stack>
    </Stack>
  </CardContent></Card>
}

function SummaryValue({ label, value }: { label: string; value: string }) {
  return <Stack spacing={0.25}><Typography variant="caption" color="text.secondary">{label}</Typography><Typography sx={{ fontWeight: 600 }}>{value}</Typography></Stack>
}
