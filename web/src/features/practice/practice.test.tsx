import { act, screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { session, signedInHandlers } from '../../test/fixtures'
import { renderRoute } from '../../test/renderRoute'
import { server } from '../../test/server'
import type { Session } from '../../shared/api/schemas'
import type { SpeechRecognitionLike } from './voice/useSpeechRecognition'

/** Controllable stand-in for the browser's SpeechRecognition. */
class FakeRecognition implements SpeechRecognitionLike {
  static last: FakeRecognition | null = null
  lang = ''
  continuous = false
  interimResults = false
  maxAlternatives = 1
  onresult: SpeechRecognitionLike['onresult'] = null
  onerror: SpeechRecognitionLike['onerror'] = null
  onend: SpeechRecognitionLike['onend'] = null
  onstart: SpeechRecognitionLike['onstart'] = null
  constructor() {
    FakeRecognition.last = this
  }
  start() {
    this.onstart?.()
  }
  stop() {
    this.onend?.()
  }
  abort() {}
  say(text: string, confidence: number) {
    const result = Object.assign([{ transcript: text, confidence }], { isFinal: true })
    this.onresult?.({ resultIndex: 0, results: Object.assign([result], { length: 1 }) })
  }
}

const win = window as unknown as { webkitSpeechRecognition?: unknown }

describe('conversation', () => {
  afterEach(() => {
    delete win.webkitSpeechRecognition
  })

  function withSession(current: Session) {
    server.use(
      ...signedInHandlers,
      http.get('*/api/v1/sessions/session-1', () => HttpResponse.json(current)),
    )
  }

  it('shows the coach opening and a clear microphone notice when voice is unsupported', async () => {
    withSession(session())

    renderRoute('/practice/session-1')

    expect(await screen.findByText('Hi! What did you do last weekend?')).toBeInTheDocument()
    expect(screen.getByText(/voice input isn't available in this browser/i)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /finish/i })).toBeDisabled()
  })

  it('sends a typed message and shows the coach reply', async () => {
    withSession(session())
    let sent: unknown
    server.use(
      http.post('*/api/v1/sessions/session-1/messages', async ({ request }) => {
        sent = await request.json()
        return HttpResponse.json(
          session({
            learnerTurnCount: 1,
            turns: [
              ...session().turns,
              {
                sequence: 2,
                speaker: 'Learner',
                text: 'I went hiking.',
                inputMethod: 'Text',
                createdAt: '2026-11-14T09:01:00Z',
              },
              {
                sequence: 3,
                speaker: 'Coach',
                text: 'Lovely! Where did you go?',
                inputMethod: 'Text',
                createdAt: '2026-11-14T09:01:05Z',
              },
            ],
          }),
        )
      }),
    )
    const { user } = renderRoute('/practice/session-1')

    await user.type(await screen.findByLabelText('Your message'), 'I went hiking.')
    await user.click(screen.getByRole('button', { name: 'Send' }))

    expect(await screen.findByText('Lovely! Where did you go?')).toBeInTheDocument()
    expect(sent).toEqual({ text: 'I went hiking.', inputMethod: 'Text', speechConfidence: null })
    expect(screen.getByLabelText('Your message')).toHaveValue('')
    expect(screen.getByRole('button', { name: /finish/i })).toBeEnabled()
  })

  it('keeps the message and offers retry when the coach is unavailable', async () => {
    withSession(session())
    server.use(
      http.post('*/api/v1/sessions/session-1/messages', () =>
        HttpResponse.json(
          { title: 'Your coach is having trouble responding.', code: 'ai.unavailable' },
          { status: 503 },
        ),
      ),
    )
    const { user } = renderRoute('/practice/session-1')

    await user.type(await screen.findByLabelText('Your message'), 'Hello there')
    await user.click(screen.getByRole('button', { name: 'Send' }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent(/trouble responding/i)
    expect(within(alert).getByRole('button', { name: 'Try again' })).toBeInTheDocument()
    expect(screen.getByLabelText('Your message')).toHaveValue('Hello there')
  })

  describe('with voice input', () => {
    beforeEach(() => {
      win.webkitSpeechRecognition = FakeRecognition
    })

    it('shows the live microphone state, fills the box from speech, and sends as voice', async () => {
      withSession(session())
      let sent: { inputMethod?: string; speechConfidence?: number } = {}
      server.use(
        http.post('*/api/v1/sessions/session-1/messages', async ({ request }) => {
          sent = (await request.json()) as typeof sent
          return HttpResponse.json(session())
        }),
      )
      const { user } = renderRoute('/practice/session-1')

      expect(await screen.findByText('Microphone off')).toBeInTheDocument()
      await user.click(screen.getByRole('button', { name: 'Start speaking' }))
      expect(screen.getByText(/microphone on: listening/i)).toBeInTheDocument()

      act(() => FakeRecognition.last!.say('I played football', 0.82))
      expect(screen.getByLabelText('Your message')).toHaveValue('I played football')

      await user.click(screen.getByRole('button', { name: 'Stop recording' }))
      expect(screen.getByText('Microphone off')).toBeInTheDocument()
      await user.click(screen.getByRole('button', { name: 'Send' }))

      await waitFor(() =>
        expect(sent).toMatchObject({ inputMethod: 'Voice', speechConfidence: 0.82 }),
      )
    })

    it('explains when microphone access is blocked', async () => {
      withSession(session())
      const { user } = renderRoute('/practice/session-1')

      await user.click(await screen.findByRole('button', { name: 'Start speaking' }))
      act(() => FakeRecognition.last!.onerror?.({ error: 'not-allowed' }))

      expect(screen.getByText('Microphone blocked')).toBeInTheDocument()
      expect(
        screen.getByText(/allow it in your browser settings, or type instead/i),
      ).toBeInTheDocument()
    })
  })

  it('finishing navigates to the feedback summary', async () => {
    const withMessage = session({
      learnerTurnCount: 1,
      turns: [
        ...session().turns,
        {
          sequence: 2,
          speaker: 'Learner',
          text: 'I go to park.',
          inputMethod: 'Text',
          createdAt: '2026-11-14T09:01:00Z',
        },
        {
          sequence: 3,
          speaker: 'Coach',
          text: 'Nice!',
          inputMethod: 'Text',
          createdAt: '2026-11-14T09:01:05Z',
        },
      ],
    })
    withSession(withMessage)
    server.use(
      http.post('*/api/v1/sessions/session-1/finish', () =>
        HttpResponse.json(
          session({
            status: 'Completed',
            transcriptCleared: true,
            turns: [],
            learnerTurnCount: 1,
            feedback: {
              summary: 'You kept the conversation going well.',
              corrections: [
                {
                  category: 'Grammar',
                  original: 'I go to park',
                  improved: 'I went to the park',
                  explanation: 'Use the past tense.',
                },
              ],
              usefulPhrases: ['What about you?'],
              repeatTask: 'Retell your weekend using the past tense.',
              wasHelpful: null,
              createdAt: '2026-11-14T09:05:00Z',
            },
          }),
        ),
      ),
    )
    const { user, router } = renderRoute('/practice/session-1')

    await user.click(await screen.findByRole('button', { name: /finish/i }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/practice/session-1/summary'))
    expect(await screen.findByText('I went to the park')).toBeInTheDocument()
    expect(screen.getByText('You kept the conversation going well.')).toBeInTheDocument()
    expect(screen.getByText(/conversation text was deleted after feedback/i)).toBeInTheDocument()
  })
})

describe('session summary', () => {
  it('offers to retry when feedback could not be generated', async () => {
    const completed = session({ status: 'Completed', learnerTurnCount: 1 })
    server.use(
      ...signedInHandlers,
      http.get('*/api/v1/sessions/session-1', () => HttpResponse.json(completed)),
      http.post('*/api/v1/sessions/session-1/feedback', () =>
        HttpResponse.json({
          ...completed,
          feedback: {
            summary: 'Second attempt worked.',
            corrections: [],
            usefulPhrases: [],
            repeatTask: 'Try again tomorrow.',
            wasHelpful: null,
            createdAt: '2026-11-14T09:06:00Z',
          },
        }),
      ),
    )
    const { user } = renderRoute('/practice/session-1/summary')

    await user.click(await screen.findByRole('button', { name: 'Get feedback' }))

    expect(await screen.findByText('Second attempt worked.')).toBeInTheDocument()
    expect(screen.getByText(/no important mistakes/i)).toBeInTheDocument()
  })
})
