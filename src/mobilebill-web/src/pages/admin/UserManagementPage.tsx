import { Box, Stack, Tab, Tabs } from '@mui/material'
import { useState } from 'react'

import { PageHeader } from '../../components/common/PageHeader'
import { AllUsersPanel } from './AllUsersPanel'
import { PendingUsersPanel } from './PendingUsersPanel'

export function UserManagementPage() {
  const [tab, setTab] = useState(0)
  // Activating or rejecting a registration changes the full user list too.
  const [refreshKey, setRefreshKey] = useState(0)
  return <Stack spacing={2}>
    <PageHeader title="User Management" subtitle="Approve new registrations and manage the roles and access of every account." />
    <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
      <Tabs value={tab} onChange={(_, value: number) => setTab(value)} aria-label="User management sections">
        <Tab label="Pending Users" id="users-tab-0" aria-controls="users-panel-0" />
        <Tab label="All Users" id="users-tab-1" aria-controls="users-panel-1" />
      </Tabs>
    </Box>
    <Box role="tabpanel" id="users-panel-0" aria-labelledby="users-tab-0" hidden={tab !== 0}>
      {tab === 0 && <PendingUsersPanel onChanged={() => setRefreshKey(key => key + 1)} />}
    </Box>
    <Box role="tabpanel" id="users-panel-1" aria-labelledby="users-tab-1" hidden={tab !== 1}>
      {tab === 1 && <AllUsersPanel refreshKey={refreshKey} />}
    </Box>
  </Stack>
}
