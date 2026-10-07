import { Box, Button, MenuItem, Stack, Tab, Tabs, TextField } from '@mui/material'
import DownloadIcon from '@mui/icons-material/Download'
import PictureAsPdfIcon from '@mui/icons-material/PictureAsPdf'
import { useEffect, useState } from 'react'

import { downloadDeviceRegister } from '../../api/devicesApi'
import { listAllMasterData } from '../../api/masterDataApi'
import type { EntityRow } from '../../api/types'
import { ErrorState } from '../../components/master-data/ErrorState'
import { PageHeader } from '../../components/common/PageHeader'
import { deviceIssuesConfig, mobileDevicesConfig } from './deviceConfigs'
import { DevicesToCollectPanel } from './DevicesToCollectPanel'
import { MasterDataPage } from './MasterDataPage'

// Company mobile devices: the register, devices to collect from leavers, and every handover.
// ?tab=collect opens the To Collect tab directly (used by the dashboard alert).
export function MobileDevicesPage() {
  const [tab, setTab] = useState(() => new URLSearchParams(window.location.search).get('tab') === 'collect' ? 1 : 0)
  return <Stack spacing={2}>
    <PageHeader title="Mobile Devices" subtitle="Company phones: who holds each one, replacements, repairs, and devices to collect from employees who have left." />
    <RegisterDownloads />
    <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
      <Tabs value={tab} onChange={(_, value: number) => setTab(value)} aria-label="Mobile devices">
        <Tab label="Devices" id="devices-tab-0" aria-controls="devices-panel-0" />
        <Tab label="To Collect" id="devices-tab-1" aria-controls="devices-panel-1" />
        <Tab label="History" id="devices-tab-2" aria-controls="devices-panel-2" />
      </Tabs>
    </Box>
    <Box role="tabpanel" id="devices-panel-0" aria-labelledby="devices-tab-0" hidden={tab !== 0}>{tab === 0 && <MasterDataPage config={mobileDevicesConfig} hideHeader />}</Box>
    <Box role="tabpanel" id="devices-panel-1" aria-labelledby="devices-tab-1" hidden={tab !== 1}>{tab === 1 && <DevicesToCollectPanel />}</Box>
    <Box role="tabpanel" id="devices-panel-2" aria-labelledby="devices-tab-2" hidden={tab !== 2}>{tab === 2 && <MasterDataPage config={deviceIssuesConfig} hideHeader />}</Box>
  </Stack>
}

// Device Register report (Excel or PDF); a factory or department lists only the devices held by employees there.
function RegisterDownloads() {
  const [factories, setFactories] = useState<EntityRow[]>([])
  const [departments, setDepartments] = useState<EntityRow[]>([])
  const [factoryCode, setFactoryCode] = useState('')
  const [departmentCode, setDepartmentCode] = useState('')
  const [downloading, setDownloading] = useState(false)
  const [error, setError] = useState<string>()
  useEffect(() => {
    let active = true
    Promise.all([listAllMasterData<EntityRow>('/api/factories', '', true), listAllMasterData<EntityRow>('/api/departments', '', true)])
      .then(([factoryRows, departmentRows]) => { if (active) { setFactories(factoryRows); setDepartments(departmentRows) } })
      .catch(() => { /* the filters are optional; the full register still downloads */ })
    return () => { active = false }
  }, [])
  const download = async (format: 'excel' | 'pdf') => {
    setDownloading(true); setError(undefined)
    try { await downloadDeviceRegister(format, { factoryCode, departmentCode }) } catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to download the device register.') } finally { setDownloading(false) }
  }
  return <Stack spacing={1}>
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ alignItems: { sm: 'center' }, flexWrap: 'wrap', rowGap: 1.5 }}>
      <TextField select size="small" label="Register factory" value={factoryCode} onChange={event => setFactoryCode(event.target.value)} sx={{ minWidth: 200 }}>
        <MenuItem value="">All factories</MenuItem>
        {factories.map(item => <MenuItem key={String(item.id)} value={String(item.code)}>{String(item.code)} — {String(item.name)}</MenuItem>)}
      </TextField>
      <TextField select size="small" label="Register department" value={departmentCode} onChange={event => setDepartmentCode(event.target.value)} sx={{ minWidth: 200 }}>
        <MenuItem value="">All departments</MenuItem>
        {departments.map(item => <MenuItem key={String(item.id)} value={String(item.code)}>{String(item.code)} — {String(item.name)}</MenuItem>)}
      </TextField>
      <Button variant="contained" startIcon={<DownloadIcon />} disabled={downloading} onClick={() => void download('excel')}>Device Register Excel</Button>
      <Button variant="outlined" startIcon={<PictureAsPdfIcon />} disabled={downloading} onClick={() => void download('pdf')}>Device Register PDF</Button>
    </Stack>
    {error && <ErrorState message={error} />}
  </Stack>
}
