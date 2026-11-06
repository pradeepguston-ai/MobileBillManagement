import { Alert, Box, Button, Stack, Tab, Tabs } from '@mui/material'
import DownloadIcon from '@mui/icons-material/Download'
import { useState } from 'react'

import { downloadMobileAllocationsReport } from '../../api/masterDataApi'
import { PageHeader } from '../../components/common/PageHeader'
import { MasterDataPage } from './MasterDataPage'
import { disconnectedNumbersConfig, mobileAllocationsConfig } from './pageConfigs'
import { SimPoolPanel } from './SimPoolPanel'

// Allocations, the SIM Pool and disconnected numbers. ?tab=pool opens the pool directly (used by the dashboard alert);
// ?tab=disconnected opens the disconnected numbers.
const tabs = ['allocations', 'pool', 'disconnected']
export function MobileAllocationsPage() {
  const [tab, setTab] = useState(() => Math.max(0, tabs.indexOf(new URLSearchParams(window.location.search).get('tab') ?? '')))
  const [downloading, setDownloading] = useState(false)
  const [downloadError, setDownloadError] = useState<string>()
  const download = async () => {
    setDownloading(true); setDownloadError(undefined)
    try { await downloadMobileAllocationsReport() } catch (reason) { setDownloadError(reason instanceof Error ? reason.message : 'Unable to download the Excel report.') } finally { setDownloading(false) }
  }
  // Remounting the open tab on each switch reloads it, so pool actions taken on Allocations show on SIM Pool.
  return <Stack spacing={2}>
    <PageHeader title="Mobile Allocations" subtitle="Who holds each number, numbers waiting in the SIM Pool after a holder resigns, and disconnected numbers."
      action={<Button variant="outlined" startIcon={<DownloadIcon />} disabled={downloading} onClick={() => void download()}>{downloading ? 'Downloading…' : 'Download Excel'}</Button>} />
    {downloadError && <Alert severity="error">{downloadError}</Alert>}
    <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
      <Tabs value={tab} onChange={(_, value: number) => setTab(value)} aria-label="Mobile allocations">
        <Tab label="Allocations" id="allocations-tab-0" aria-controls="allocations-panel-0" />
        <Tab label="SIM Pool" id="allocations-tab-1" aria-controls="allocations-panel-1" />
        <Tab label="Disconnected" id="allocations-tab-2" aria-controls="allocations-panel-2" />
      </Tabs>
    </Box>
    <Box role="tabpanel" id="allocations-panel-0" aria-labelledby="allocations-tab-0" hidden={tab !== 0}>
      {tab === 0 && <MasterDataPage config={mobileAllocationsConfig} hideHeader />}
    </Box>
    <Box role="tabpanel" id="allocations-panel-1" aria-labelledby="allocations-tab-1" hidden={tab !== 1}>
      {tab === 1 && <SimPoolPanel />}
    </Box>
    <Box role="tabpanel" id="allocations-panel-2" aria-labelledby="allocations-tab-2" hidden={tab !== 2}>
      {tab === 2 && <MasterDataPage config={disconnectedNumbersConfig} hideHeader />}
    </Box>
  </Stack>
}
