import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router'
import { meApi, queryKeys } from '../../shared/api/endpoints'
import { errorMessage } from '../../shared/api/httpClient'
import { Alert } from '../../shared/ui/Alert'
import { useAuth } from '../auth/useAuth'
import { defaultPreferences } from './preferences'
import { PreferencesForm } from './PreferencesForm'

export function OnboardingPage() {
  const { user, updateUser } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: meApi.completeOnboarding,
    onSuccess: (profile) => {
      queryClient.setQueryData(queryKeys.profile, profile)
      updateUser({ onboardingCompleted: true })
      navigate('/home', { replace: true })
    },
  })

  return (
    <section className="card" aria-labelledby="onboarding-title">
      <h1 id="onboarding-title">Hi {user?.displayName}, let's set up your practice</h1>
      <p className="muted">Three quick questions so your coach can adapt to you.</p>
      {mutation.error && <Alert tone="error">{errorMessage(mutation.error)}</Alert>}
      <PreferencesForm
        initial={defaultPreferences}
        submitLabel="Start practising"
        busy={mutation.isPending}
        onSubmit={(prefs) => mutation.mutate(prefs)}
      />
    </section>
  )
}
