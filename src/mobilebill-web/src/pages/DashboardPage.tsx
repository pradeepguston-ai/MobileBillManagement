import { Stack } from '@mui/material'

import { PageHeader } from '../components/common/PageHeader'
import { InsightsPanel } from '../components/insights/InsightsPanel'
import { DevicesToCollectAlert } from '../components/insights/DevicesToCollectAlert'
import { SimPoolAlert } from '../components/insights/SimPoolAlert'

// The Dashboard shows billing insights; batch progress lives on Billing › Monthly Bill Review.
export function DashboardPage() {
  return <Stack spacing={2}>
    <PageHeader title="Dashboard" subtitle="Billing insights for the current batch and comparison with past batches." />
    <SimPoolAlert />
    <DevicesToCollectAlert />
    <InsightsPanel />
  </Stack>
}
