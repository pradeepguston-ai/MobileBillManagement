import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'

import { changePassword as apiChangePassword, fetchCurrentUser, login as apiLogin, type CurrentUser, type UserRole } from '../api/authApi'
import { getMyPermissions, type ModulePermission, type PermissionAction, type PermissionModule } from '../api/permissionsApi'
import { allows } from './permissions'
import { billingPreparerRoles, masterDataEditorRoles } from './roles'
import { clearToken, getToken, onUnauthorized, setToken } from '../api/tokenStore'

type AuthContextValue = {
  user: CurrentUser | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
  // Replaces the password and switches to the fresh token (ends the one-time password restriction).
  changePassword: (currentPassword: string, newPassword: string) => Promise<void>
  // Whether the signed-in user's role may do this on the screen (User Management > Permissions).
  can: (module: PermissionModule, action: PermissionAction) => boolean
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [loading, setLoading] = useState(true)
  // Kept with the account they belong to, so a different sign-in never sees the previous user's permissions.
  const [loaded, setLoaded] = useState<{ key: string; list: ModulePermission[] }>()

  useEffect(() => { onUnauthorized(() => setUser(null)) }, [])

  useEffect(() => {
    if (!getToken()) { setLoading(false); return }
    fetchCurrentUser()
      .then(setUser)
      .catch(() => clearToken())
      .finally(() => setLoading(false))
  }, [])

  // Reloaded whenever someone signs in; until then (or if it fails) the built-in defaults apply.
  const role = user?.role
  const userId = user?.id
  // A one-time password's token may only change the password, so nothing else is loaded until that is done.
  const restricted = Boolean(user?.mustChangePassword)
  const key = `${userId}:${role}`
  const permissions = loaded?.key === key ? loaded.list : null
  useEffect(() => {
    if (!userId || role === 'Administrator' || restricted) return
    let current = true
    getMyPermissions()
      .then(result => { if (current && Array.isArray(result)) setLoaded({ key: `${userId}:${role}`, list: result }) })
      .catch(() => { /* the defaults stay in place; the API still enforces the saved permissions */ })
    return () => { current = false }
  }, [userId, role, restricted])

  const login = async (email: string, password: string) => {
    const result = await apiLogin(email, password)
    setToken(result.token)
    setUser(result.user)
  }

  const logout = () => { clearToken(); setUser(null) }

  const changePassword = async (currentPassword: string, newPassword: string) => {
    const result = await apiChangePassword(currentPassword, newPassword)
    setToken(result.token)
    setUser(result.user)
  }

  const can = useCallback((module: PermissionModule, action: PermissionAction) => Boolean(user && allows(permissions, user.role, module, action)), [user, permissions])

  return <AuthContext.Provider value={{ user, loading, login, logout, changePassword, can }}>{children}</AuthContext.Provider>
}

// Billing preparation, assessment and approvals follow the fixed approval chain, not the screen permissions.
export function useCanPrepareBilling() {
  return useCanEditMasterData(billingPreparerRoles)
}

export function useCanEditMasterData(roles: readonly UserRole[] = masterDataEditorRoles) {
  const context = useContext(AuthContext)
  if (!context) return true
  return context.user !== null && roles.includes(context.user.role)
}

// Outside an AuthProvider (isolated component tests) everything is allowed, as with useCanEditMasterData.
export function usePermission(module: PermissionModule, action: PermissionAction = 'View') {
  const context = useContext(AuthContext)
  if (!context) return true
  return context.can(module, action)
}

// The signed-in user, or null (also outside an AuthProvider, unlike useAuth).
export function useCurrentUser() {
  return useContext(AuthContext)?.user ?? null
}

export function usePermissions() {
  const context = useContext(AuthContext)
  return useCallback((module: PermissionModule, action: PermissionAction) => !context || context.can(module, action), [context])
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used within an AuthProvider')
  return context
}
