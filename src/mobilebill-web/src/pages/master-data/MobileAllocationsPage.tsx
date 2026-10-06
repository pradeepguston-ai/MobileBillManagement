import { Box, Stack, Tab, Tabs } from '@mui/material'
import { useState } from 'react'

import { PageHeader } from '../../components/common/PageHeader'
import { MasterDataPage } from './MasterDataPage'
import { mobileAllocationsConfig } from './pageConfigs'
import { SimPoolPanel } from './SimPoolPanel'

// Allocations and the SIM Pool. ?tab=pool opens the pool directly (used by the dashboard alert).
export function MobileAllocationsPage() {
  const [tab, setTab] = useState(() => new URLSearchParams(window.location.search).get('tab') === 'pool' ? 1 : 0)
  // Remounting the open tab on each switch reloads it, so pool actions taken on Allocations show on SIM Pool.
  return <Stack spacing={2}>
    <PageHeader title="Mobile Allocations" subtitle="Who holds each number, and numbers waiting in the SIM Pool after a holder resigns." />
    <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
      <Tabs value={tab} onChange={(_, value: number) => setTab(value)} aria-label="Mobile allocations">
        <Tab label="Allocations" id="allocations-tab-0" aria-controls="allocations-panel-0" />
        <Tab label="SIM Pool" id="allocations-tab-1" aria-controls="allocations-panel-1" />
      </Tabs>
    </Box>
    <Box role="tabpanel" id="allocations-panel-0" aria-labelledby="allocations-tab-0" hidden={tab !== 0}>
      {tab === 0 && <MasterDataPage config={mobileAllocationsConfig} hideHeader />}
    </Box>
    <Box role="tabpanel" id="allocations-panel-1" aria-labelledby="allocations-tab-1" hidden={tab !== 1}>
      {tab === 1 && <SimPoolPanel />}
    </Box>
  </Stack>
}
