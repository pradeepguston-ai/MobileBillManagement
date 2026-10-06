import { Box, Stack, Tab, Tabs } from '@mui/material'
import { useState } from 'react'

import { PageHeader } from '../../components/common/PageHeader'
import { MasterDataPage } from './MasterDataPage'
import { departmentsConfig, sectionsConfig, subSectionsConfig } from './pageConfigs'

// Department → Section → Sub Section, each on its own tab. Only the open tab is mounted, so its
// dropdowns are reloaded on switching and show sections or departments just added on another tab.
const tabs = [
  { label: 'Departments', config: departmentsConfig },
  { label: 'Sections', config: sectionsConfig },
  { label: 'Sub Sections', config: subSectionsConfig },
]

export function DepartmentsPage() {
  const [tab, setTab] = useState(0)
  return <Stack spacing={2}>
    <PageHeader title="Departments" subtitle="Departments, their sections and sub sections." />
    <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
      <Tabs value={tab} onChange={(_, value: number) => setTab(value)} aria-label="Department structure">
        {tabs.map((item, index) => <Tab key={item.label} label={item.label} id={`departments-tab-${index}`} aria-controls={`departments-panel-${index}`} />)}
      </Tabs>
    </Box>
    {tabs.map((item, index) => <Box key={item.label} role="tabpanel" id={`departments-panel-${index}`} aria-labelledby={`departments-tab-${index}`} hidden={tab !== index}>
      {tab === index && <MasterDataPage config={item.config} hideHeader />}
    </Box>)}
  </Stack>
}
