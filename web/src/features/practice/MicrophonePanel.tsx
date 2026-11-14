import type { SpeechRecognitionState } from './voice/useSpeechRecognition'

/**
 * Microphone control with an always-visible state, so the learner never has to guess whether
 * the microphone is on. Speech is transcribed by the browser; SpeakMate never stores audio.
 */
export function MicrophonePanel({
  speech,
  disabled,
}: {
  speech: SpeechRecognitionState
  disabled: boolean
}) {
  if (speech.status === 'unsupported') {
    return (
      <p className="mic-note muted small">
        Voice input isn't available in this browser. Typing works everywhere (try Chrome or Edge for
        voice).
      </p>
    )
  }

  const listening = speech.status === 'listening'

  return (
    <div className="mic">
      <button
        type="button"
        className={`mic__button${listening ? ' mic__button--live' : ''}`}
        onClick={listening ? speech.stop : speech.start}
        disabled={disabled || speech.status === 'denied'}
        aria-pressed={listening}
        aria-label={listening ? 'Stop recording' : 'Start speaking'}
      >
        <span aria-hidden="true">{listening ? '■' : '🎤'}</span>
      </button>
      <span
        className={`mic__state${listening ? ' mic__state--live' : ''}`}
        role="status"
        aria-live="polite"
      >
        {listening
          ? '● Microphone on: listening'
          : speech.status === 'denied'
            ? 'Microphone blocked'
            : 'Microphone off'}
      </span>
      {speech.errorMessage && <span className="mic__error small">{speech.errorMessage}</span>}
    </div>
  )
}
