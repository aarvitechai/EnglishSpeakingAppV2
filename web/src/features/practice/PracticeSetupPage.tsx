import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'
import { queryKeys, sessionsApi } from '../../shared/api/endpoints'
import { errorMessage } from '../../shared/api/httpClient'
import { practiceModes, type PracticeMode } from '../../shared/api/schemas'
import { Alert } from '../../shared/ui/Alert'
import { Button, TextField } from '../../shared/ui/Form'
import { modeLabels } from '../../shared/ui/labels'

export function PracticeSetupPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [mode, setMode] = useState<PracticeMode>('DailyConversation')
  const [topic, setTopic] = useState('')
  const topics = useQuery({
    queryKey: queryKeys.topics,
    queryFn: ({ signal }) => sessionsApi.topics(signal),
    staleTime: Infinity,
  })

  const start = useMutation({
    mutationFn: () => sessionsApi.start(mode, topic.trim() || undefined),
    onSuccess: (session) => {
      queryClient.setQueryData(queryKeys.session(session.id), session)
      void queryClient.invalidateQueries({ queryKey: ['history'] })
      navigate(`/practice/${session.id}`)
    },
  })

  const suggestions = topics.data?.find((t) => t.mode === mode)?.topics ?? []

  function onSubmit(event: FormEvent) {
    event.preventDefault()
    start.mutate()
  }

  return (
    <form className="stack-lg" onSubmit={onSubmit}>
      <h1>Choose your practice</h1>

      <fieldset className="fieldset">
        <legend>Practice mode</legend>
        <div className="option-grid">
          {practiceModes.map((m) => (
            <label key={m} className={`option-card${mode === m ? ' option-card--selected' : ''}`}>
              <input
                type="radio"
                name="mode"
                value={m}
                checked={mode === m}
                onChange={() => setMode(m)}
              />
              <span className="option-card__icon" aria-hidden="true">
                {modeLabels[m].icon}
              </span>
              <strong>{modeLabels[m].title}</strong>
              <span className="muted">{modeLabels[m].description}</span>
            </label>
          ))}
        </div>
      </fieldset>

      <fieldset className="fieldset">
        <legend>Topic</legend>
        <div className="chip-group" role="group" aria-label="Suggested topics">
          {suggestions.map((s) => (
            <button
              key={s}
              type="button"
              className={`chip${topic === s ? ' chip--selected' : ''}`}
              aria-pressed={topic === s}
              onClick={() => setTopic(topic === s ? '' : s)}
            >
              {s}
            </button>
          ))}
        </div>
        <TextField
          label="Or type your own topic"
          value={topic}
          maxLength={120}
          placeholder="Leave empty for a surprise topic"
          onChange={(e) => setTopic(e.target.value)}
        />
      </fieldset>

      {start.error && <Alert tone="error">{errorMessage(start.error)}</Alert>}

      <div>
        <Button type="submit" busy={start.isPending} className="btn--lg">
          Start conversation
        </Button>
      </div>
    </form>
  )
}
