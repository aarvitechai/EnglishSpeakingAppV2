import { http, HttpResponse } from 'msw'
import type { AuthResponse, Profile, Progress, Session } from '../shared/api/schemas'

export const user = {
  id: '0195f0c8-0000-7000-8000-000000000001',
  email: 'asha@example.test',
  displayName: 'Asha',
  onboardingCompleted: true,
}

export function authResponse(overrides: Partial<AuthResponse['user']> = {}): AuthResponse {
  return {
    accessToken: 'test-access-token',
    expiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
    user: { ...user, ...overrides },
  }
}

export const profile: Profile = {
  displayName: 'Asha',
  level: 'Intermediate',
  goals: ['Work'],
  supportLanguage: 'hi',
  dailyTargetMinutes: 15,
  onboardingCompleted: true,
}

export const progress: Progress = {
  completedSessions: 4,
  totalPracticeMinutes: 42,
  todayMinutes: 6,
  dailyTargetMinutes: 15,
  currentStreakDays: 3,
  last7Days: Array.from({ length: 7 }, (_, i) => ({
    date: `2026-11-${String(8 + i).padStart(2, '0')}`,
    minutes: i,
  })),
  focusAreas: [{ category: 'Grammar', count: 3 }],
}

export function session(overrides: Partial<Session> = {}): Session {
  return {
    id: 'session-1',
    mode: 'DailyConversation',
    level: 'Intermediate',
    topic: 'Your weekend',
    status: 'Active',
    saveTranscript: false,
    transcriptCleared: false,
    startedAt: '2026-11-14T09:00:00Z',
    endedAt: null,
    learnerTurnCount: 0,
    maxLearnerTurns: 30,
    turns: [
      {
        sequence: 1,
        speaker: 'Coach',
        text: 'Hi! What did you do last weekend?',
        inputMethod: 'Text',
        createdAt: '2026-11-14T09:00:00Z',
      },
    ],
    feedback: null,
    ...overrides,
  }
}

/** Handlers for a signed-in, onboarded learner. Tests override individual endpoints as needed. */
export const signedInHandlers = [
  http.post('*/api/v1/auth/refresh', () => HttpResponse.json(authResponse())),
  http.get('*/health/ready', () => HttpResponse.text('Healthy')),
  http.get('*/api/v1/me', () => HttpResponse.json(profile)),
  http.get('*/api/v1/progress', () => HttpResponse.json(progress)),
  http.get('*/api/v1/sessions', () =>
    HttpResponse.json({ items: [], page: 1, pageSize: 5, totalCount: 0, totalPages: 0 }),
  ),
]

export const anonymousHandlers = [
  http.post('*/api/v1/auth/refresh', () =>
    HttpResponse.json(
      { title: 'Your session has expired.', code: 'auth.invalid_refresh_token' },
      { status: 401 },
    ),
  ),
  http.get('*/health/ready', () => HttpResponse.text('Healthy')),
]
