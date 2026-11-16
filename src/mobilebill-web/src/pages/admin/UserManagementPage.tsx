import { Box, Stack, Tab, Tabs } from '@mui/material'
import { useState } from 'react'

import { usePermission } from '../../auth/AuthContext'
import { PageHeader } from '../../components/common/PageHeader'
import { AllUsersPanel } from './AllUsersPanel'
import { CreateUserPanel } from './CreateUserPanel'
import { PendingUsersPanel } from './PendingUsersPanel'
import { PermissionsPanel } from './PermissionsPanel'

// The user tabs need the User Management permission (Create User needs Create); the Permissions tab needs Role
// Permissions. A role with either one opens the page and sees only its tabs.
export function UserManagementPage() {
  const canViewUsers = usePermission('user-management', 'View')
  const canCreate = usePermission('user-management', 'Create')
  const canViewPermissions = usePermission('role-permissions', 'View')
  const visible = [canViewUsers, canViewUsers, canCreate, canViewPermissions].flatMap((shown, index) => shown ? [index] : [])
  const [chosen, setChosen] = useState(0)
  // The first available tab until one is chosen (or if the chosen one is no longer available).
  const tab = visible.includes(chosen) ? chosen : visible[0] ?? 0
  // Activating, rejecting or creating an account changes the full user list too.
  const [refreshKey, setRefreshKey] = useState(0)
  const refresh = () => setRefreshKey(key => key + 1)
  return <Stack spacing={2}>
    <PageHeader title="User Management" subtitle="Create accounts, approve new registrations, manage roles and set which screens each role can use." />
    <Box sx={{ borderBottom: 1, borderColor: 'divider' }}>
      <Tabs value={tab} onChange={(_, value: number) => setChosen(value)} aria-label="User management sections">
        {canViewUsers && <Tab label="All Users" id="users-tab-0" aria-controls="users-panel-0" value={0} />}
        {canViewUsers && <Tab label="Pending Users" id="users-tab-1" aria-controls="users-panel-1" value={1} />}
        {canCreate && <Tab label="Create User" id="users-tab-2" aria-controls="users-panel-2" value={2} />}
        {canViewPermissions && <Tab label="Permissions" id="users-tab-3" aria-controls="users-panel-3" value={3} />}
      </Tabs>
    </Box>
    {canViewUsers && <Box role="tabpanel" id="users-panel-0" aria-labelledby="users-tab-0" hidden={tab !== 0}>
      {tab === 0 && <AllUsersPanel refreshKey={refreshKey} />}
    </Box>}
    {canViewUsers && <Box role="tabpanel" id="users-panel-1" aria-labelledby="users-tab-1" hidden={tab !== 1}>
      {tab === 1 && <PendingUsersPanel onChanged={refresh} />}
    </Box>}
    {canCreate && <Box role="tabpanel" id="users-panel-2" aria-labelledby="users-tab-2" hidden={tab !== 2}>
      {tab === 2 && <CreateUserPanel onCreated={refresh} />}
    </Box>}
    {canViewPermissions && <Box role="tabpanel" id="users-panel-3" aria-labelledby="users-tab-3" hidden={tab !== 3}>
      {tab === 3 && <PermissionsPanel />}
    </Box>}
  </Stack>
}
