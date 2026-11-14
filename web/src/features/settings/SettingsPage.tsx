import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { meApi, queryKeys } from '../../shared/api/endpoints'
import { errorMessage } from '../../shared/api/httpClient'
import type { ConsentStatus, ConsentType } from '../../shared/api/schemas'
import { Alert } from '../../shared/ui/Alert'
import { Button, TextField } from '../../shared/ui/Form'
import { Spinner } from '../../shared/ui/Spinner'
import { useAuth } from '../auth/useAuth'
import { PreferencesForm } from '../profile/PreferencesForm'

const consentText: Record<ConsentType, { title: string; description: string }> = {
  TermsAndPrivacy: {
    title: 'Terms and privacy policy',
    description: 'Required to use SpeakMate. To withdraw, delete your account below.',
  },
  TranscriptStorage: {
    title: 'Keep my conversation transcripts',
    description:
      'Off: the text of each conversation is deleted once your feedback is ready (or within 24 hours). On: transcripts stay in your history until you delete your account. Audio is never stored.',
  },
  ProductImprovement: {
    title: 'Help improve SpeakMate',
    description: 'Allow anonymised usage statistics to be used to improve the product.',
  },
}

function ConsentRow({ consent }: { consent: ConsentStatus }) {
  const queryClient = useQueryClient()
  const mutation = useMutation({
    mutationFn: (granted: boolean) => meApi.setConsent(consent.type, granted),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: queryKeys.consents }),
  })
  const text = consentText[consent.type]

  return (
    <li className="consent-row">
      <div>
        <strong>{text.title}</strong>
        <p className="muted small">{text.description}</p>
        {mutation.error && <p className="field__error">{errorMessage(mutation.error)}</p>}
      </div>
      <label className="switch">
        <input
          type="checkbox"
          role="switch"
          checked={consent.granted}
          disabled={consent.required || mutation.isPending}
          onChange={(e) => mutation.mutate(e.target.checked)}
          aria-label={text.title}
        />
        <span className="switch__track" aria-hidden="true" />
      </label>
    </li>
  )
}

function DangerZone() {
  const { clear } = useAuth()
  const [password, setPassword] = useState('')
  const [confirming, setConfirming] = useState(false)

  const exportData = useMutation({
    mutationFn: meApi.exportData,
    onSuccess: (blob) => {
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `speakmate-export-${new Date().toISOString().slice(0, 10)}.json`
      a.click()
      URL.revokeObjectURL(url)
    },
  })

  const remove = useMutation({
    mutationFn: () => meApi.deleteAccount(password),
    // Clearing the session sends the user to the login page, which shows this confirmation.
    onSuccess: () => clear('Your account and all your data have been permanently deleted.'),
  })

  function onDelete(event: FormEvent) {
    event.preventDefault()
    remove.mutate()
  }

  return (
    <section className="card" aria-labelledby="data-title">
      <h2 id="data-title">Your data</h2>
      <div className="row-between">
        <p className="muted">Download everything we store about you as a JSON file.</p>
        <Button variant="secondary" onClick={() => exportData.mutate()} busy={exportData.isPending}>
          Download my data
        </Button>
      </div>
      {exportData.error && <Alert tone="error">{errorMessage(exportData.error)}</Alert>}

      <hr />
      <h3>Delete account</h3>
      <p className="muted">
        This permanently deletes your account, profile, sessions, feedback and consent history. It
        cannot be undone.
      </p>
      {!confirming ? (
        <Button variant="danger" onClick={() => setConfirming(true)}>
          Delete my account…
        </Button>
      ) : (
        <form onSubmit={onDelete} className="stack">
          <TextField
            label="Confirm with your password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
          {remove.error && <Alert tone="error">{errorMessage(remove.error)}</Alert>}
          <div className="row">
            <Button type="submit" variant="danger" busy={remove.isPending} disabled={!password}>
              Permanently delete
            </Button>
            <Button variant="ghost" onClick={() => setConfirming(false)}>
              Cancel
            </Button>
          </div>
        </form>
      )}
    </section>
  )
}

export function SettingsPage() {
  const queryClient = useQueryClient()
  const profile = useQuery({
    queryKey: queryKeys.profile,
    queryFn: ({ signal }) => meApi.get(signal),
  })
  const consents = useQuery({
    queryKey: queryKeys.consents,
    queryFn: ({ signal }) => meApi.consents(signal),
  })
  const save = useMutation({
    mutationFn: meApi.updatePreferences,
    onSuccess: (updated) => {
      queryClient.setQueryData(queryKeys.profile, updated)
      void queryClient.invalidateQueries({ queryKey: queryKeys.progress })
    },
  })

  return (
    <div className="stack-lg">
      <h1>Settings</h1>

      <section className="card" aria-labelledby="prefs-title">
        <h2 id="prefs-title">Learning preferences</h2>
        {profile.isPending && <Spinner />}
        {profile.error && <Alert tone="error">{errorMessage(profile.error)}</Alert>}
        {save.isSuccess && <Alert tone="success">Preferences saved.</Alert>}
        {save.error && <Alert tone="error">{errorMessage(save.error)}</Alert>}
        {profile.data && (
          <PreferencesForm
            key={JSON.stringify(profile.data)}
            initial={profile.data}
            submitLabel="Save preferences"
            busy={save.isPending}
            onSubmit={(prefs) => save.mutate(prefs)}
          />
        )}
      </section>

      <section className="card" aria-labelledby="privacy-title">
        <h2 id="privacy-title">Privacy</h2>
        <p className="muted small">
          Your messages are sent to our AI provider (Google Gemini) to generate replies and
          feedback. Voice input is turned into text by your browser; we never receive or store
          audio.
        </p>
        {consents.isPending && <Spinner />}
        <ul className="consent-list">
          {consents.data?.map((c) => (
            <ConsentRow key={c.type} consent={c} />
          ))}
        </ul>
      </section>

      <DangerZone />
    </div>
  )
}
