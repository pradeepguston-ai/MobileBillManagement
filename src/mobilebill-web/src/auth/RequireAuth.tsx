import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'

import type { UserRole } from '../api/authApi'
import { ChangePasswordPage } from '../pages/auth/ChangePasswordPage'
import { useAuth } from './AuthContext'

export function RequireAuth({ children, roles }: { children: ReactNode; roles?: UserRole[] }) {
  const { user, loading } = useAuth()
  const location = useLocation()

  if (loading) return null
  if (!user) return <Navigate to="/login" state={{ from: location }} replace />
  // Signed in with a one-time password: the page asked for opens once the user has chosen their own password.
  if (user.mustChangePassword) return <ChangePasswordPage />
  if (roles && !roles.includes(user.role)) return <Navigate to="/" replace />
  return <>{children}</>
}
