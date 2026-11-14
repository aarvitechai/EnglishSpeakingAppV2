import { useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router'
import { errorMessage } from '../../shared/api/httpClient'
import { Alert } from '../../shared/ui/Alert'
import { Button, TextField } from '../../shared/ui/Form'
import { useAuth } from './useAuth'

export function LoginPage() {
  const { login, notice, dismissNotice } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const from = (location.state as { from?: string } | null)?.from ?? '/home'

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setBusy(true)
    try {
      dismissNotice()
      await login(email.trim(), password)
      navigate(from, { replace: true })
    } catch (e) {
      setError(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card auth-card" aria-labelledby="login-title">
      <h1 id="login-title">Welcome back</h1>
      <p className="muted">Sign in to continue practising.</p>
      {notice && <Alert tone="success">{notice}</Alert>}
      {error && <Alert tone="error">{error}</Alert>}
      <form onSubmit={onSubmit} noValidate>
        <TextField
          label="Email"
          type="email"
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
        />
        <TextField
          label="Password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
        />
        <Button type="submit" busy={busy} className="btn--block">
          Sign in
        </Button>
      </form>
      <p className="muted center">
        New here? <Link to="/register">Create an account</Link>
      </p>
    </section>
  )
}
