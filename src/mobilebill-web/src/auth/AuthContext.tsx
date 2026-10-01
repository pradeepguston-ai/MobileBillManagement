import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'

import { fetchCurrentUser, login as apiLogin, type CurrentUser } from '../api/authApi'
import { clearToken, getToken, onUnauthorized, setToken } from '../api/tokenStore'

type AuthContextValue = {
  user: CurrentUser | null
  loading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => { onUnauthorized(() => setUser(null)) }, [])

  useEffect(() => {
    if (!getToken()) { setLoading(false); return }
    fetchCurrentUser()
      .then(setUser)
      .catch(() => clearToken())
      .finally(() => setLoading(false))
  }, [])

  const login = async (email: string, password: string) => {
    const result = await apiLogin(email, password)
    setToken(result.token)
    setUser(result.user)
  }

  const logout = () => { clearToken(); setUser(null) }

  return <AuthContext.Provider value={{ user, loading, login, logout }}>{children}</AuthContext.Provider>
}

const masterDataEditorRoles = ['Administrator', 'ITEngineer']

export function useCanEditMasterData() {
  const context = useContext(AuthContext)
  if (!context) return true
  return context.user !== null && masterDataEditorRoles.includes(context.user.role)
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used within an AuthProvider')
  return context
}
