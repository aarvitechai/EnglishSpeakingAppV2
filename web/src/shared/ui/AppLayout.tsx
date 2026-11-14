import { NavLink, Outlet } from 'react-router'
import { useAuth } from '../../features/auth/useAuth'
import { ApiStatusBadge } from './ApiStatusBadge'

const links = [
  { to: '/home', label: 'Home' },
  { to: '/practice/new', label: 'Practise' },
  { to: '/history', label: 'History' },
  { to: '/settings', label: 'Settings' },
]

export function AppLayout() {
  const { user, logout } = useAuth()

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main">
        Skip to content
      </a>
      <header className="app-header">
        <span className="brand">SpeakMate AI</span>
        {user?.onboardingCompleted && (
          <nav aria-label="Main" className="app-nav">
            {links.map((link) => (
              <NavLink key={link.to} to={link.to} className="app-nav__link">
                {link.label}
              </NavLink>
            ))}
          </nav>
        )}
        <div className="app-header__end">
          <ApiStatusBadge />
          {user && (
            <button type="button" className="btn btn--ghost" onClick={() => void logout()}>
              Sign out
            </button>
          )}
        </div>
      </header>
      <main id="main" className="app-main">
        <Outlet />
      </main>
    </div>
  )
}

export function PublicLayout() {
  return (
    <div className="app-shell">
      <header className="app-header">
        <span className="brand">SpeakMate AI</span>
        <ApiStatusBadge />
      </header>
      <main id="main" className="app-main app-main--narrow">
        <Outlet />
      </main>
    </div>
  )
}
