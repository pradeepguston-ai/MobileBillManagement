import type { UserRole } from '../api/authApi'
import type { ModulePermission, PermissionAction, PermissionModule } from '../api/permissionsApi'
import { masterDataEditorRoles, organisationMasterEditorRoles } from './roles'

// The built-in access rules, the same as the API's defaults. Used only until the signed-in user's own permissions
// have loaded (or if they cannot be loaded); the API enforces the saved permissions either way.
const itEditors = masterDataEditorRoles
const organisationEditors = organisationMasterEditorRoles
const defaults: Record<PermissionModule, { view?: readonly UserRole[]; change?: readonly UserRole[] }> = {
  'dashboard': {}, 'monthly-bill-review': {}, 'billing-batches': {}, 'exception-review': {},
  'employees': { change: organisationEditors }, 'mobile-allocations': { change: itEditors }, 'mobile-devices': { change: itEditors },
  'factories': { change: organisationEditors }, 'departments': { change: organisationEditors }, 'designations': { change: organisationEditors },
  'categories': { change: organisationEditors }, 'mobile-packages': { change: itEditors }, 'providers': { change: itEditors },
  'third-party-companies': { change: organisationEditors },
  'monthly-bill-report': {}, 'vas-report': {}, 'report-distribution': { view: itEditors, change: itEditors }, 'activity-log': {}, 'ai-assistant': {},
  // Administrator only until granted (Administrator is always allowed).
  'user-management': { view: [], change: [] }, 'role-permissions': { view: [], change: [] },
}

// The User Management page holds the user tabs and the Permissions tab, so either permission opens it.
export const userManagementPage: readonly PermissionModule[] = ['user-management', 'role-permissions']

export function canViewAny(can: (module: PermissionModule, action: PermissionAction) => boolean, modules: PermissionModule | readonly PermissionModule[]) {
  return (typeof modules === 'string' ? [modules] : modules).some(module => can(module, 'View'))
}

export function defaultAllows(role: UserRole, module: PermissionModule, action: PermissionAction) {
  if (role === 'Administrator') return true
  const rule = defaults[module]
  if (!rule) return false
  return action === 'View' ? !rule.view || rule.view.includes(role) : Boolean(rule.change?.includes(role))
}

export function allows(permissions: ModulePermission[] | null, role: UserRole, module: PermissionModule, action: PermissionAction) {
  if (role === 'Administrator') return true
  const entry = permissions?.find(item => item.module === module)
  if (!entry) return defaultAllows(role, module, action)
  return { View: entry.canView, Create: entry.canCreate, Edit: entry.canEdit, Delete: entry.canDelete }[action]
}

// The screen each menu path opens, so the menu and the routes can hide what a role may not view. A path listing several
// opens for a role that may view any of them.
export const modulesByPath: Record<string, PermissionModule | readonly PermissionModule[]> = {
  '/': 'dashboard',
  '/monthly-bill-review': 'monthly-bill-review', '/billing': 'billing-batches', '/exception-review': 'exception-review',
  '/employees': 'employees', '/mobile-allocations': 'mobile-allocations', '/devices': 'mobile-devices', '/factories': 'factories',
  '/departments': 'departments', '/designations': 'designations', '/categories': 'categories', '/packages': 'mobile-packages',
  '/providers': 'providers', '/third-party-companies': 'third-party-companies',
  '/reports/monthly-bill': 'monthly-bill-report', '/reports/vas': 'vas-report',
  '/admin/report-distribution': 'report-distribution', '/admin/email-history': 'report-distribution', '/activity-log': 'activity-log',
  '/admin/users': userManagementPage, '/admin/password-resets': 'user-management',
}
