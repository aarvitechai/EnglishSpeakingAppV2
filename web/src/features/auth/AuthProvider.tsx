import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { authApi } from '../../shared/api/endpoints'
import { setAccessToken, setRefreshHandler } from '../../shared/api/httpClient'
import type { AuthenticatedUser, AuthResponse } from '../../shared/api/schemas'
import { AuthContext, type AuthContextValue, type AuthStatus } from './authContext'

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<AuthStatus>('loading')
  const [user, setUser] = useState<AuthenticatedUser | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const restoreStarted = useRef(false)

  const apply = useCallback((auth: AuthResponse) => {
    setAccessToken(auth.accessToken)
    setUser(auth.user)
    setStatus('authenticated')
  }, [])

  const clear = useCallback(
    (message?: string) => {
      setNotice(message ?? null)
      setAccessToken(null)
      setUser(null)
      setStatus('anonymous')
      queryClient.clear()
    },
    [queryClient],
  )

  // The HTTP client calls this when a request gets 401: rotate the refresh cookie once.
  useEffect(() => {
    setRefreshHandler(async () => {
      try {
        const auth = await authApi.refresh()
        apply(auth)
        return auth.accessToken
      } catch {
        clear()
        return null
      }
    })
    return () => setRefreshHandler(null)
  }, [apply, clear])

  // Restore the session on page load from the HttpOnly refresh cookie (ref guard: StrictMode runs effects twice).
  useEffect(() => {
    if (restoreStarted.current) return
    restoreStarted.current = true
    authApi
      .refresh()
      .then(apply)
      .catch(() => {
        setAccessToken(null)
        setStatus('anonymous')
      })
  }, [apply])

  const value = useMemo<AuthContextValue>(
    () => ({
      status,
      user,
      login: async (email, password) => apply(await authApi.login({ email, password })),
      register: async (input) => apply(await authApi.register(input)),
      logout: async () => {
        try {
          await authApi.logout()
        } finally {
          clear()
        }
      },
      updateUser: (changes) =>
        setUser((current) => (current ? { ...current, ...changes } : current)),
      clear,
      notice,
      dismissNotice: () => setNotice(null),
    }),
    [status, user, notice, apply, clear],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
