import { apiFetch } from './http'
import type { UserRole } from './authApi'

// Which role may open and change which screen (User Management > Permissions). Keys match PermissionModules on the API.
export type PermissionModule =
  | 'dashboard' | 'monthly-bill-review' | 'billing-batches' | 'exception-review'
  | 'employees' | 'mobile-allocations' | 'mobile-devices' | 'factories' | 'departments' | 'designations' | 'categories'
  | 'mobile-packages' | 'providers' | 'third-party-companies'
  | 'monthly-bill-report' | 'vas-report' | 'report-distribution' | 'activity-log' | 'ai-assistant' | 'user-management'
export type PermissionAction = 'View' | 'Create' | 'Edit' | 'Delete'
export type ModulePermission = { module: PermissionModule; canView: boolean; canCreate: boolean; canEdit: boolean; canDelete: boolean }
export type PermissionModuleInfo = { key: PermissionModule; label: string; group: string; note: string | null; actions: PermissionAction[] }
export type RolePermissions = { role: UserRole; locked: boolean; customised: boolean; permissions: ModulePermission[] }
export type PermissionMatrix = { modules: PermissionModuleInfo[]; roles: RolePermissions[] }

export function getMyPermissions() { return apiFetch<ModulePermission[]>('/api/permissions/mine') }
export function getPermissionMatrix() { return apiFetch<PermissionMatrix>('/api/permissions') }
export function saveRolePermissions(role: UserRole, permissions: ModulePermission[]) {
  return apiFetch<RolePermissions>(`/api/permissions/roles/${role}`, { method: 'PUT', body: JSON.stringify({ permissions }) })
}
export function resetRolePermissions(role: UserRole) {
  return apiFetch<RolePermissions>(`/api/permissions/roles/${role}/reset`, { method: 'POST' })
}
