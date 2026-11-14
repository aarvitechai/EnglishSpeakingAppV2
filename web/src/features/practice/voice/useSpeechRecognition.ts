import { useCallback, useEffect, useRef, useState } from 'react'

/**
 * Turn-based speech-to-text using the browser's Web Speech API.
 *
 * Privacy: Chrome/Edge send audio to the browser vendor's speech service to transcribe it.
 * SpeakMate itself never receives or stores audio; only the final text the learner chooses to send.
 */

// Minimal typings: the Web Speech API is not in TypeScript's DOM lib.
interface RecognitionAlternative {
  transcript: string
  confidence: number
}
interface RecognitionResult {
  readonly isFinal: boolean
  readonly length: number
  [index: number]: RecognitionAlternative
}
interface RecognitionEvent {
  readonly resultIndex: number
  readonly results: { readonly length: number; [index: number]: RecognitionResult }
}
interface RecognitionErrorEvent {
  readonly error: string
}
export interface SpeechRecognitionLike {
  lang: string
  continuous: boolean
  interimResults: boolean
  maxAlternatives: number
  start(): void
  stop(): void
  abort(): void
  onresult: ((event: RecognitionEvent) => void) | null
  onerror: ((event: RecognitionErrorEvent) => void) | null
  onend: (() => void) | null
  onstart: (() => void) | null
}
type RecognitionConstructor = new () => SpeechRecognitionLike

export type RecognitionStatus = 'unsupported' | 'idle' | 'listening' | 'denied' | 'error'

export function getRecognitionConstructor(): RecognitionConstructor | null {
  if (typeof window === 'undefined') return null
  const w = window as unknown as {
    SpeechRecognition?: RecognitionConstructor
    webkitSpeechRecognition?: RecognitionConstructor
  }
  return w.SpeechRecognition ?? w.webkitSpeechRecognition ?? null
}

export interface SpeechRecognitionState {
  status: RecognitionStatus
  /** Final text so far plus the live (interim) part. */
  transcript: string
  /** Average recogniser confidence for final results (0-1), or null if not reported. */
  confidence: number | null
  errorMessage: string | null
  start: () => void
  stop: () => void
  reset: () => void
}

export interface SpeechRecognitionOptions {
  lang?: string
  /** Called from the recognition event with the full transcript so far (final + live part). */
  onTranscript?: (text: string) => void
}

export function useSpeechRecognition({
  lang = 'en-US',
  onTranscript,
}: SpeechRecognitionOptions = {}): SpeechRecognitionState {
  const Ctor = getRecognitionConstructor()
  const onTranscriptRef = useRef(onTranscript)
  const finalRef = useRef('')
  useEffect(() => {
    onTranscriptRef.current = onTranscript
  }, [onTranscript])
  const [status, setStatus] = useState<RecognitionStatus>(Ctor ? 'idle' : 'unsupported')
  const [finalText, setFinalText] = useState('')
  const [interimText, setInterimText] = useState('')
  const [confidence, setConfidence] = useState<number | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const recognitionRef = useRef<SpeechRecognitionLike | null>(null)
  const confidences = useRef<number[]>([])

  useEffect(() => () => recognitionRef.current?.abort(), [])

  const start = useCallback(() => {
    if (!Ctor) return
    recognitionRef.current?.abort()
    const recognition = new Ctor()
    recognition.lang = lang
    recognition.continuous = true
    recognition.interimResults = true
    recognition.maxAlternatives = 1

    recognition.onstart = () => {
      setStatus('listening')
      setErrorMessage(null)
    }
    recognition.onresult = (event) => {
      let interim = ''
      let appended = ''
      for (let i = event.resultIndex; i < event.results.length; i++) {
        const result = event.results[i]
        const alternative = result?.[0]
        if (!result || !alternative) continue
        if (result.isFinal) {
          appended += alternative.transcript
          if (alternative.confidence > 0) confidences.current.push(alternative.confidence)
        } else {
          interim += alternative.transcript
        }
      }
      if (appended) finalRef.current = `${finalRef.current} ${appended}`.trim()
      setFinalText(finalRef.current)
      setInterimText(interim)
      onTranscriptRef.current?.(`${finalRef.current} ${interim}`.trim())
      const c = confidences.current
      setConfidence(c.length ? c.reduce((a, b) => a + b, 0) / c.length : null)
    }
    recognition.onerror = (event) => {
      if (event.error === 'not-allowed' || event.error === 'service-not-allowed') {
        setStatus('denied')
        setErrorMessage(
          'Microphone access is blocked. Allow it in your browser settings, or type instead.',
        )
      } else if (event.error === 'no-speech') {
        setErrorMessage("We didn't hear anything. Try again a little closer to the microphone.")
      } else if (event.error !== 'aborted') {
        setStatus('error')
        setErrorMessage(
          'Speech recognition stopped unexpectedly. You can try again or type your answer.',
        )
      }
    }
    recognition.onend = () => {
      setInterimText('')
      setStatus((s) => (s === 'listening' ? 'idle' : s))
    }

    recognitionRef.current = recognition
    try {
      recognition.start()
    } catch {
      setStatus('error')
      setErrorMessage('Could not start the microphone. Please type your answer instead.')
    }
  }, [Ctor, lang])

  const stop = useCallback(() => recognitionRef.current?.stop(), [])

  const reset = useCallback(() => {
    recognitionRef.current?.abort()
    confidences.current = []
    finalRef.current = ''
    setFinalText('')
    setInterimText('')
    setConfidence(null)
    setErrorMessage(null)
    setStatus(Ctor ? 'idle' : 'unsupported')
  }, [Ctor])

  return {
    status,
    transcript: `${finalText} ${interimText}`.trim(),
    confidence,
    errorMessage,
    start,
    stop,
    reset,
  }
}
