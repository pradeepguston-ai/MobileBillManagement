import { Box, Stack, Tab, Tabs } from '@mui/material'
import { useState } from 'react'

import { PageHeader } from '../../components/common/PageHeader'
import { MasterDataPage } from './MasterDataPage'
import { employeesConfig } from './pageConfigs'
import { ResignationsPanel } from './ResignationsPanel'

// Only the open tab is mounted, so switching reloads it and shows a resignation just recorded on another tab.
export function EmployeesPage() {
  const [tab, setTab] = useState(0)
  return <Stack spacing={2}>
    <PageHeader title="Employees" subtitle="Employees, those serving notice before a resignation, and those who have resigned." />
    <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
      <Tabs value={tab} onChange={(_, value: number) => setTab(value)} aria-label="Employees">
        <Tab label="Employees" id="employees-tab-0" aria-controls="employees-panel-0" />
        <Tab label="Pending Resignation" id="employees-tab-1" aria-controls="employees-panel-1" />
        <Tab label="Resigned" id="employees-tab-2" aria-controls="employees-panel-2" />
      </Tabs>
    </Box>
    <Box role="tabpanel" id="employees-panel-0" aria-labelledby="employees-tab-0" hidden={tab !== 0}>
      {tab === 0 && <MasterDataPage config={employeesConfig} hideHeader />}
    </Box>
    <Box role="tabpanel" id="employees-panel-1" aria-labelledby="employees-tab-1" hidden={tab !== 1}>
      {tab === 1 && <ResignationsPanel status="Pending" />}
    </Box>
    <Box role="tabpanel" id="employees-panel-2" aria-labelledby="employees-tab-2" hidden={tab !== 2}>
      {tab === 2 && <ResignationsPanel status="Resigned" />}
    </Box>
  </Stack>
}
