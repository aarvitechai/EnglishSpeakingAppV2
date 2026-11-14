import { z } from 'zod'
import { request, requestJson } from './httpClient'
import {
  AuthResponse,
  ConsentStatus,
  Profile,
  ProgressSchema,
  Session,
  SessionHistory,
  TopicSuggestions,
  type ConsentType,
  type EnglishLevel,
  type InputMethod,
  type LearningGoal,
  type PracticeMode,
} from './schemas'

const base = '/api/v1'

export interface Preferences {
  level: EnglishLevel
  goals: LearningGoal[]
  supportLanguage: string
  dailyTargetMinutes: number
}

export const authApi = {
  register: (input: {
    email: string
    password: string
    displayName: string
    acceptTerms: boolean
  }) =>
    requestJson(`${base}/auth/register`, AuthResponse, {
      method: 'POST',
      body: input,
      anonymous: true,
    }),
  login: (input: { email: string; password: string }) =>
    requestJson(`${base}/auth/login`, AuthResponse, {
      method: 'POST',
      body: input,
      anonymous: true,
    }),
  refresh: () =>
    requestJson(`${base}/auth/refresh`, AuthResponse, { method: 'POST', anonymous: true }),
  logout: () => request(`${base}/auth/logout`, { method: 'POST', anonymous: true }),
}

export const meApi = {
  get: (signal?: AbortSignal) => requestJson(`${base}/me`, Profile, { signal }),
  completeOnboarding: (prefs: Preferences) =>
    requestJson(`${base}/me/onboarding`, Profile, { method: 'PUT', body: prefs }),
  updatePreferences: (prefs: Preferences) =>
    requestJson(`${base}/me/preferences`, Profile, { method: 'PUT', body: prefs }),
  rename: (displayName: string) =>
    requestJson(`${base}/me/display-name`, Profile, { method: 'PUT', body: { displayName } }),
  consents: (signal?: AbortSignal) =>
    requestJson(`${base}/me/consents`, z.array(ConsentStatus), { signal }),
  setConsent: (type: ConsentType, granted: boolean) =>
    requestJson(`${base}/me/consents/${type}`, ConsentStatus, { method: 'PUT', body: { granted } }),
  exportData: async () => (await request(`${base}/me/export`)).blob(),
  deleteAccount: (password: string) =>
    request(`${base}/me`, { method: 'DELETE', body: { password } }),
}

export const sessionsApi = {
  topics: (signal?: AbortSignal) =>
    requestJson(`${base}/sessions/topics`, TopicSuggestions, { signal }),
  start: (mode: PracticeMode, topic?: string) =>
    requestJson(`${base}/sessions`, Session, {
      method: 'POST',
      body: { mode, topic: topic || null },
    }),
  get: (id: string, signal?: AbortSignal) =>
    requestJson(`${base}/sessions/${id}`, Session, { signal }),
  send: (
    id: string,
    input: { text: string; inputMethod: InputMethod; speechConfidence: number | null },
  ) => requestJson(`${base}/sessions/${id}/messages`, Session, { method: 'POST', body: input }),
  finish: (id: string) => requestJson(`${base}/sessions/${id}/finish`, Session, { method: 'POST' }),
  retryFeedback: (id: string) =>
    requestJson(`${base}/sessions/${id}/feedback`, Session, { method: 'POST' }),
  abandon: (id: string) =>
    requestJson(`${base}/sessions/${id}/abandon`, Session, { method: 'POST' }),
  rate: (id: string, wasHelpful: boolean) =>
    requestJson(`${base}/sessions/${id}/feedback/rating`, Session, {
      method: 'PUT',
      body: { wasHelpful },
    }),
  history: (page: number, pageSize: number, signal?: AbortSignal) =>
    requestJson(`${base}/sessions?page=${page}&pageSize=${pageSize}`, SessionHistory, { signal }),
}

export const progressApi = {
  get: (signal?: AbortSignal) =>
    requestJson(
      `${base}/progress?utcOffsetMinutes=${-new Date().getTimezoneOffset()}`,
      ProgressSchema,
      { signal },
    ),
}

/** Query keys in one place, so cache invalidation stays consistent. */
export const queryKeys = {
  profile: ['profile'] as const,
  consents: ['consents'] as const,
  topics: ['topics'] as const,
  progress: ['progress'] as const,
  history: (page: number) => ['history', page] as const,
  session: (id: string) => ['session', id] as const,
}
