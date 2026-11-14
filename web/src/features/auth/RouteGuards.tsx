import { Navigate, Outlet, useLocation } from 'react-router'
import { FullPageSpinner } from '../../shared/ui/Spinner'
import { useAuth } from './useAuth'

/** Signed-in area. Sends visitors to login, and new learners to onboarding first. */
export function RequireAuth() {
  const { status, user } = useAuth()
  const location = useLocation()

  if (status === 'loading') return <FullPageSpinner label="Loading your account…" />
  if (status === 'anonymous' || !user)
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  if (!user.onboardingCompleted && location.pathname !== '/onboarding')
    return <Navigate to="/onboarding" replace />
  return <Outlet />
}

/** Login/register pages: signed-in users skip straight to the app. */
export function RedirectIfAuthenticated() {
  const { status } = useAuth()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from ?? '/home'

  if (status === 'loading') return <FullPageSpinner label="Loading…" />
  if (status === 'authenticated') return <Navigate to={from} replace />
  return <Outlet />
}
