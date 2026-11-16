import { Stack, Typography } from '@mui/material'
import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'

import type { PermissionModule } from '../api/permissionsApi'
import { PageHeader } from '../components/common/PageHeader'
import { useAuth, usePermissions } from './AuthContext'
import { canViewAny } from './permissions'

// Opens the screen only for roles allowed to view it (User Management > Permissions); others go to the start page.
// With several modules, viewing any of them opens it. The start page is the Dashboard, so a role that may not view the
// Dashboard gets a plain welcome there instead.
export function RequirePermission({ module, children }: { module: PermissionModule | readonly PermissionModule[]; children: ReactNode }) {
  const can = usePermissions()
  if (canViewAny(can, module)) return <>{children}</>
  return module === 'dashboard' ? <Welcome /> : <Navigate to="/" replace />
}

function Welcome() {
  const { user } = useAuth()
  return <Stack spacing={2}>
    <PageHeader title={`Welcome${user ? `, ${user.displayName}` : ''}`} subtitle="Mobile Billing System" />
    <Typography color="text.secondary">Use the menu to open the screens available to your role.</Typography>
  </Stack>
}
