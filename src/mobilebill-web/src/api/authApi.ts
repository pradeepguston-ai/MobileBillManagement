import { apiFetch } from './http'

export type UserRole = 'Administrator' | 'ITEngineer' | 'HeadOfIt' | 'GroupHrManager' | 'Cfo'
export type UserStatus = 'PendingActivation' | 'Active' | 'Deactivated'

export type CurrentUser = {
  id: string
  email: string
  displayName: string
  role: UserRole
  status: UserStatus
}

export type AuthResult = {
  token: string
  expiresAtUtc: string
  user: CurrentUser
}

export type RegisterInput = {
  email: string
  password: string
  displayName: string
  requestedRole: UserRole
}

export type PendingUser = {
  id: string
  email: string
  displayName: string
  requestedRole: UserRole
  registeredAtUtc: string
}

export function login(email: string, password: string) {
  return apiFetch<AuthResult>('/api/auth/login', { method: 'POST', body: JSON.stringify({ email, password }) })
}

export function register(input: RegisterInput) {
  return apiFetch<CurrentUser>('/api/auth/register', { method: 'POST', body: JSON.stringify(input) })
}

export function fetchCurrentUser() {
  return apiFetch<CurrentUser>('/api/auth/me')
}

export function listPendingUsers() {
  return apiFetch<PendingUser[]>('/api/users/pending')
}

export function activateUser(id: string, roleOverride?: UserRole) {
  return apiFetch<CurrentUser>(`/api/users/${id}/activate`, { method: 'POST', body: JSON.stringify({ roleOverride: roleOverride ?? null }) })
}

export function deactivateUser(id: string) {
  return apiFetch<CurrentUser>(`/api/users/${id}/deactivate`, { method: 'POST' })
}

export const roleLabels: Record<UserRole, string> = {
  Administrator: 'Administrator',
  ITEngineer: 'IT Engineer',
  HeadOfIt: 'Head of IT',
  GroupHrManager: 'Group HR Manager',
  Cfo: 'CFO',
}

export const selfRegisterableRoles: { value: UserRole; label: string }[] = [
  { value: 'ITEngineer', label: roleLabels.ITEngineer },
  { value: 'HeadOfIt', label: roleLabels.HeadOfIt },
  { value: 'GroupHrManager', label: roleLabels.GroupHrManager },
  { value: 'Cfo', label: roleLabels.Cfo },
]
