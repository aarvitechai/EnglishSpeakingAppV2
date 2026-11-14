import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { ApiError, errorMessage } from '../../shared/api/httpClient'
import { Alert } from '../../shared/ui/Alert'
import { Button, TextField } from '../../shared/ui/Form'
import { fieldError } from '../../shared/ui/fieldError'
import { useAuth } from './useAuth'

export const MIN_PASSWORD_LENGTH = 10

export function RegisterPage() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ displayName: '', email: '', password: '', acceptTerms: false })
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({})
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const update = (changes: Partial<typeof form>) => setForm((f) => ({ ...f, ...changes }))

  function validate(): Record<string, string[]> {
    const errors: Record<string, string[]> = {}
    if (!form.displayName.trim()) errors.displayName = ['Tell us what to call you.']
    if (!/^\S+@\S+\.\S+$/.test(form.email.trim())) errors.email = ['Enter a valid email address.']
    if (form.password.length < MIN_PASSWORD_LENGTH)
      errors.password = [
        `Use at least ${MIN_PASSWORD_LENGTH} characters. A short phrase works well.`,
      ]
    if (!form.acceptTerms) errors.acceptTerms = ['Please accept the terms to continue.']
    return errors
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    const errors = validate()
    setFieldErrors(errors)
    if (Object.keys(errors).length > 0) return

    setBusy(true)
    try {
      await register({ ...form, email: form.email.trim(), displayName: form.displayName.trim() })
      navigate('/onboarding', { replace: true })
    } catch (e) {
      if (e instanceof ApiError && Object.keys(e.fieldErrors).length > 0)
        setFieldErrors(e.fieldErrors)
      setError(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card auth-card" aria-labelledby="register-title">
      <h1 id="register-title">Create your account</h1>
      <p className="muted">Practise speaking English with a patient AI coach.</p>
      {error && <Alert tone="error">{error}</Alert>}
      <form onSubmit={onSubmit} noValidate>
        <TextField
          label="What should we call you?"
          autoComplete="nickname"
          value={form.displayName}
          onChange={(e) => update({ displayName: e.target.value })}
          error={fieldError(fieldErrors, 'displayName')}
          maxLength={80}
        />
        <TextField
          label="Email"
          type="email"
          autoComplete="email"
          value={form.email}
          onChange={(e) => update({ email: e.target.value })}
          error={fieldError(fieldErrors, 'email')}
        />
        <TextField
          label="Password"
          type="password"
          autoComplete="new-password"
          value={form.password}
          onChange={(e) => update({ password: e.target.value })}
          error={fieldError(fieldErrors, 'password')}
          hint={`At least ${MIN_PASSWORD_LENGTH} characters.`}
        />
        <div className="field">
          <label className="checkbox">
            <input
              type="checkbox"
              checked={form.acceptTerms}
              onChange={(e) => update({ acceptTerms: e.target.checked })}
              aria-invalid={fieldError(fieldErrors, 'acceptTerms') ? true : undefined}
            />
            <span>
              I agree to the terms of use and privacy policy. My practice conversations are
              processed by an AI provider to generate replies and feedback.
            </span>
          </label>
          {fieldError(fieldErrors, 'acceptTerms') && (
            <p className="field__error">{fieldError(fieldErrors, 'acceptTerms')}</p>
          )}
        </div>
        <Button type="submit" busy={busy} className="btn--block">
          Create account
        </Button>
      </form>
      <p className="muted center">
        Already have an account? <Link to="/login">Sign in</Link>
      </p>
    </section>
  )
}
