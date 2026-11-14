import { createContext } from 'react'
import type { AuthenticatedUser } from '../../shared/api/schemas'

export type AuthStatus = 'loading' | 'authenticated' | 'anonymous'

export interface AuthContextValue {
  status: AuthStatus
  user: AuthenticatedUser | null
  login: (email: string, password: string) => Promise<void>
  register: (input: {
    email: string
    password: string
    displayName: string
    acceptTerms: boolean
  }) => Promise<void>
  logout: () => Promise<void>
  /** Update the cached user after profile changes (e.g. onboarding completed). */
  updateUser: (changes: Partial<AuthenticatedUser>) => void
  /** Forget the session locally (e.g. after account deletion), optionally with a message for the login page. */
  clear: (notice?: string) => void
  /** One-off message shown on the login page, then dismissed. */
  notice: string | null
  dismissNotice: () => void
}

export const AuthContext = createContext<AuthContextValue | null>(null)
