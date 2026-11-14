import { useCallback, useEffect, useState } from 'react'

/** Text-to-speech for coach replies, using the browser's built-in voices (no audio leaves the device). */
export function useSpeechSynthesis(rate = 0.95) {
  const supported = typeof window !== 'undefined' && 'speechSynthesis' in window
  const [speaking, setSpeaking] = useState(false)

  useEffect(
    () => () => {
      if (supported) window.speechSynthesis.cancel()
    },
    [supported],
  )

  const speak = useCallback(
    (text: string) => {
      if (!supported || !text) return
      window.speechSynthesis.cancel()
      const utterance = new SpeechSynthesisUtterance(text)
      utterance.lang = 'en-US'
      utterance.rate = rate
      const english = window.speechSynthesis.getVoices().find((v) => v.lang.startsWith('en'))
      if (english) utterance.voice = english
      utterance.onstart = () => setSpeaking(true)
      utterance.onend = () => setSpeaking(false)
      utterance.onerror = () => setSpeaking(false)
      window.speechSynthesis.speak(utterance)
    },
    [supported, rate],
  )

  const cancel = useCallback(() => {
    if (supported) window.speechSynthesis.cancel()
    setSpeaking(false)
  }, [supported])

  return { supported, speaking, speak, cancel }
}

const AUTO_SPEAK_KEY = 'speakmate.autoSpeak'

/** Per-device preference; storage can be unavailable (private mode), so failures fall back to the default. */
export function readAutoSpeak(): boolean {
  try {
    return window.localStorage.getItem(AUTO_SPEAK_KEY) !== 'false'
  } catch {
    return true
  }
}

export function writeAutoSpeak(value: boolean) {
  try {
    window.localStorage.setItem(AUTO_SPEAK_KEY, String(value))
  } catch {
    // Ignore: the preference just won't persist.
  }
}
