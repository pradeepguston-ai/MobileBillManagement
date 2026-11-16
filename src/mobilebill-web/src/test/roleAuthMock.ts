import type { UserRole } from '../api/authApi'
import type { PermissionAction, PermissionModule } from '../api/permissionsApi'
import { defaultAllows } from '../auth/permissions'
import { billingPreparerRoles, masterDataEditorRoles } from '../auth/roles'

// A stand-in for auth/AuthContext in component tests: the hooks answer as the given role would with the default
// permissions. Use as vi.mock('…/auth/AuthContext', async () => (await import('…/test/roleAuthMock')).roleAuthMock(() => 'HrUser')).
export function roleAuthMock(role: () => string) {
  const current = () => role() as UserRole
  const can = (module: PermissionModule, action: PermissionAction = 'View') => defaultAllows(current(), module, action)
  return {
    useAuth: () => ({ user: { id: 'test-user', email: 'test@example.com', displayName: 'Test User', role: current(), status: 'Active' }, loading: false, can }),
    useCanEditMasterData: (roles: readonly string[] = masterDataEditorRoles) => roles.includes(current()),
    useCanPrepareBilling: () => (billingPreparerRoles as readonly string[]).includes(current()),
    usePermission: can,
    usePermissions: () => can,
  }
}
