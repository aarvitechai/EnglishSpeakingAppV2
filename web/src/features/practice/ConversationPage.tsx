import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  useEffect,
  useEffectEvent,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent,
} from 'react'
import { Link, Navigate, useNavigate, useParams } from 'react-router'
import { queryKeys, sessionsApi } from '../../shared/api/endpoints'
import { errorMessage } from '../../shared/api/httpClient'
import type { InputMethod, Session } from '../../shared/api/schemas'
import { Alert } from '../../shared/ui/Alert'
import { Button } from '../../shared/ui/Form'
import { FullPageSpinner } from '../../shared/ui/Spinner'
import { modeLabels } from '../../shared/ui/labels'
import { MicrophonePanel } from './MicrophonePanel'
import { readAutoSpeak, useSpeechSynthesis, writeAutoSpeak } from './voice/useSpeechSynthesis'
import { useSpeechRecognition } from './voice/useSpeechRecognition'

export const MAX_MESSAGE_LENGTH = 2000

export function ConversationPage() {
  const { sessionId = '' } = useParams()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const session = useQuery({
    queryKey: queryKeys.session(sessionId),
    queryFn: ({ signal }) => sessionsApi.get(sessionId, signal),
  })

  const [text, setText] = useState('')
  const [inputMethod, setInputMethod] = useState<InputMethod>('Text')
  const [autoSpeak, setAutoSpeak] = useState(readAutoSpeak)
  // Live speech fills the message box from the recognition event; the learner can edit before sending.
  const speech = useSpeechRecognition({
    onTranscript: (spoken) => {
      setText(spoken)
      if (spoken) setInputMethod('Voice')
    },
  })
  const tts = useSpeechSynthesis()
  const listEnd = useRef<HTMLLIElement>(null)
  const lastSpokenSequence = useRef<number>(0)

  const setSession = (s: Session) => queryClient.setQueryData(queryKeys.session(s.id), s)

  const send = useMutation({
    mutationFn: () =>
      sessionsApi.send(sessionId, {
        text: text.trim(),
        inputMethod,
        speechConfidence: inputMethod === 'Voice' ? speech.confidence : null,
      }),
    onSuccess: (updated) => {
      setSession(updated)
      setText('')
      setInputMethod('Text')
      speech.reset()
    },
  })

  const finish = useMutation({
    mutationFn: () => sessionsApi.finish(sessionId),
    onSuccess: (updated) => {
      setSession(updated)
      void queryClient.invalidateQueries({ queryKey: queryKeys.progress })
      void queryClient.invalidateQueries({ queryKey: ['history'] })
      navigate(`/practice/${sessionId}/summary`, { replace: true })
    },
  })

  const abandon = useMutation({
    mutationFn: () => sessionsApi.abandon(sessionId),
    onSuccess: (updated) => {
      setSession(updated)
      void queryClient.invalidateQueries({ queryKey: ['history'] })
      navigate('/home', { replace: true })
    },
  })

  const turns = session.data?.turns ?? []
  const lastTurn = turns.at(-1)
  const speakNewCoachTurn = useEffectEvent((text: string) => {
    if (autoSpeak) tts.speak(text)
  })

  // Keep the newest message in view (only when the conversation changes, not on every keystroke).
  useEffect(() => {
    listEnd.current?.scrollIntoView?.({ behavior: 'smooth', block: 'end' })
  }, [turns.length, send.isPending])

  // Read each new coach message aloud exactly once, if voice output is on.
  useEffect(() => {
    if (lastTurn?.speaker === 'Coach' && lastTurn.sequence > lastSpokenSequence.current) {
      lastSpokenSequence.current = lastTurn.sequence
      speakNewCoachTurn(lastTurn.text)
    }
  }, [lastTurn?.speaker, lastTurn?.sequence, lastTurn?.text])

  if (session.isPending) return <FullPageSpinner label="Loading conversation…" />
  if (session.error) return <Alert tone="error">{errorMessage(session.error)}</Alert>

  const data = session.data
  if (data.status !== 'Active') return <Navigate to={`/practice/${data.id}/summary`} replace />

  const remaining = data.maxLearnerTurns - data.learnerTurnCount
  const atLimit = remaining <= 0
  const coachThinking = send.isPending
  const canSend =
    text.trim().length > 0 && !coachThinking && !atLimit && speech.status !== 'listening'

  function onSubmit(event: FormEvent) {
    event.preventDefault()
    if (canSend) send.mutate()
  }

  function onKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      if (canSend) send.mutate()
    }
  }

  function toggleAutoSpeak() {
    const next = !autoSpeak
    setAutoSpeak(next)
    writeAutoSpeak(next)
    if (!next) tts.cancel()
  }

  return (
    <div className="conversation">
      <header className="conversation__header">
        <div>
          <p className="muted small">
            {modeLabels[data.mode].icon} {modeLabels[data.mode].title} · {data.level}
          </p>
          <h1 className="conversation__topic">{data.topic}</h1>
        </div>
        <div className="row">
          {tts.supported && (
            <Button variant="ghost" onClick={toggleAutoSpeak} aria-pressed={autoSpeak}>
              {autoSpeak ? '🔊 Voice on' : '🔇 Voice off'}
            </Button>
          )}
          <Button
            variant="secondary"
            onClick={() => finish.mutate()}
            busy={finish.isPending}
            disabled={data.learnerTurnCount === 0 || coachThinking}
            title={data.learnerTurnCount === 0 ? 'Send at least one message first' : undefined}
          >
            Finish &amp; get feedback
          </Button>
        </div>
      </header>

      <ol className="chat" aria-live="polite" aria-label="Conversation">
        {turns.map((t) => (
          <li key={t.sequence} className={`bubble bubble--${t.speaker.toLowerCase()}`}>
            <span className="visually-hidden">
              {t.speaker === 'Coach' ? 'Coach said:' : 'You said:'}
            </span>
            <p>{t.text}</p>
            {t.speaker === 'Coach' && tts.supported && (
              <button
                type="button"
                className="bubble__listen"
                onClick={() => tts.speak(t.text)}
                aria-label="Listen again"
              >
                🔊
              </button>
            )}
            {t.speaker === 'Learner' && t.inputMethod === 'Voice' && (
              <span className="bubble__meta">🎤 spoken</span>
            )}
          </li>
        ))}
        {coachThinking && (
          <li className="bubble bubble--coach bubble--thinking" aria-label="Coach is thinking">
            <span className="typing">
              <span />
              <span />
              <span />
            </span>
          </li>
        )}
        <li ref={listEnd} aria-hidden="true" />
      </ol>

      {send.error && (
        <Alert
          tone="error"
          action={
            <Button variant="secondary" onClick={() => send.mutate()} disabled={!canSend}>
              Try again
            </Button>
          }
        >
          {errorMessage(send.error)} Your message is still in the box.
        </Alert>
      )}
      {finish.error && <Alert tone="error">{errorMessage(finish.error)}</Alert>}
      {atLimit && (
        <Alert tone="info">
          You reached this session's message limit. Great effort! Finish to see your feedback.
        </Alert>
      )}

      <form className="composer" onSubmit={onSubmit}>
        <MicrophonePanel speech={speech} disabled={coachThinking || atLimit} />
        <label htmlFor="message" className="visually-hidden">
          Your message
        </label>
        <textarea
          id="message"
          className="composer__input"
          rows={2}
          maxLength={MAX_MESSAGE_LENGTH}
          placeholder={
            speech.status === 'listening'
              ? 'Listening… speak now'
              : 'Type your answer, or use the microphone'
          }
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={onKeyDown}
          disabled={atLimit}
        />
        <Button type="submit" disabled={!canSend} busy={coachThinking}>
          Send
        </Button>
      </form>

      <footer className="conversation__footer muted small">
        <span>
          {remaining} of {data.maxLearnerTurns} messages left ·{' '}
          {data.saveTranscript
            ? 'Transcript will be saved'
            : 'Transcript is deleted after feedback'}
        </span>
        <span>
          <button
            type="button"
            className="link-button"
            onClick={() => abandon.mutate()}
            disabled={abandon.isPending}
          >
            End without feedback
          </button>
          {' · '}
          <Link to="/home">Leave for now</Link>
        </span>
      </footer>
    </div>
  )
}
