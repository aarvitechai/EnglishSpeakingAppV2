import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, Navigate, useParams } from 'react-router'
import { queryKeys, sessionsApi } from '../../shared/api/endpoints'
import { errorMessage } from '../../shared/api/httpClient'
import type { Session } from '../../shared/api/schemas'
import { Alert } from '../../shared/ui/Alert'
import { Button } from '../../shared/ui/Form'
import { FullPageSpinner } from '../../shared/ui/Spinner'
import { categoryLabels, formatDate, modeLabels } from '../../shared/ui/labels'
import { useSpeechSynthesis } from './voice/useSpeechSynthesis'

export function SessionSummaryPage() {
  const { sessionId = '' } = useParams()
  const queryClient = useQueryClient()
  const tts = useSpeechSynthesis(0.9)
  const session = useQuery({
    queryKey: queryKeys.session(sessionId),
    queryFn: ({ signal }) => sessionsApi.get(sessionId, signal),
  })

  const setSession = (s: Session) => queryClient.setQueryData(queryKeys.session(s.id), s)
  const retry = useMutation({
    mutationFn: () => sessionsApi.retryFeedback(sessionId),
    onSuccess: setSession,
  })
  const rate = useMutation({
    mutationFn: (helpful: boolean) => sessionsApi.rate(sessionId, helpful),
    onSuccess: setSession,
  })

  if (session.isPending) return <FullPageSpinner label="Loading feedback…" />
  if (session.error) return <Alert tone="error">{errorMessage(session.error)}</Alert>

  const data = session.data
  if (data.status === 'Active') return <Navigate to={`/practice/${data.id}`} replace />
  const feedback = data.feedback

  return (
    <div className="stack-lg">
      <header>
        <p className="muted small">
          {modeLabels[data.mode].icon} {modeLabels[data.mode].title} · {formatDate(data.startedAt)}
        </p>
        <h1>{data.topic}</h1>
      </header>

      {data.status === 'Abandoned' && (
        <Alert tone="info">This session was ended without feedback.</Alert>
      )}

      {data.status === 'Completed' && !feedback && (
        <Alert
          tone="warning"
          action={
            <Button onClick={() => retry.mutate()} busy={retry.isPending}>
              Get feedback
            </Button>
          }
        >
          Your coach couldn't prepare feedback just now. Your conversation is saved for 24 hours so
          you can try again.
          {retry.error && <span className="block">{errorMessage(retry.error)}</span>}
        </Alert>
      )}

      {feedback && (
        <>
          <section className="card card--highlight" aria-labelledby="summary-title">
            <h2 id="summary-title">Summary</h2>
            <p>{feedback.summary}</p>
          </section>

          <section className="card" aria-labelledby="corrections-title">
            <h2 id="corrections-title">Top improvements</h2>
            {feedback.corrections.length === 0 ? (
              <p className="muted">No important mistakes this time. Excellent!</p>
            ) : (
              <ol className="corrections">
                {feedback.corrections.map((c, i) => (
                  <li key={i} className="correction">
                    <span className="badge">{categoryLabels[c.category]}</span>
                    <p className="correction__original">
                      <span className="visually-hidden">You said: </span>
                      <s>{c.original}</s>
                    </p>
                    <p className="correction__improved">
                      <span className="visually-hidden">Better: </span>
                      {c.improved}
                      {tts.supported && (
                        <button
                          type="button"
                          className="bubble__listen"
                          onClick={() => tts.speak(c.improved)}
                          aria-label="Listen"
                        >
                          🔊
                        </button>
                      )}
                    </p>
                    <p className="muted small">{c.explanation}</p>
                  </li>
                ))}
              </ol>
            )}
            <p className="muted small">
              AI feedback can be imperfect, especially for spoken input. Use your judgement.
            </p>
          </section>

          {feedback.usefulPhrases.length > 0 && (
            <section className="card" aria-labelledby="phrases-title">
              <h2 id="phrases-title">Useful phrases</h2>
              <ul className="phrase-list">
                {feedback.usefulPhrases.map((p) => (
                  <li key={p}>
                    {p}
                    {tts.supported && (
                      <button
                        type="button"
                        className="bubble__listen"
                        onClick={() => tts.speak(p)}
                        aria-label={`Listen to ${p}`}
                      >
                        🔊
                      </button>
                    )}
                  </li>
                ))}
              </ul>
            </section>
          )}

          <section className="card" aria-labelledby="task-title">
            <h2 id="task-title">Try this next</h2>
            <p>{feedback.repeatTask}</p>
            <Link to="/practice/new" className="btn btn--primary">
              Practise again
            </Link>
          </section>

          <section className="card row-between" aria-label="Rate this feedback">
            <span>Was this feedback helpful?</span>
            {feedback.wasHelpful === null ? (
              <span className="row">
                <Button variant="secondary" onClick={() => rate.mutate(true)} busy={rate.isPending}>
                  👍 Yes
                </Button>
                <Button
                  variant="secondary"
                  onClick={() => rate.mutate(false)}
                  busy={rate.isPending}
                >
                  👎 Not really
                </Button>
              </span>
            ) : (
              <span className="muted">Thanks for telling us!</span>
            )}
          </section>
        </>
      )}

      <section className="card" aria-labelledby="transcript-title">
        <h2 id="transcript-title">Transcript</h2>
        {data.transcriptCleared ? (
          <p className="muted">
            The conversation text was deleted after feedback, as you chose. You can keep transcripts
            in Settings.
          </p>
        ) : data.turns.length === 0 ? (
          <p className="muted">No messages.</p>
        ) : (
          <ol className="chat chat--compact">
            {data.turns.map((t) => (
              <li key={t.sequence} className={`bubble bubble--${t.speaker.toLowerCase()}`}>
                <p>{t.text}</p>
              </li>
            ))}
          </ol>
        )}
      </section>
    </div>
  )
}
