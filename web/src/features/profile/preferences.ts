import type { Preferences } from '../../shared/api/endpoints'

export const MAX_GOALS = 3

export const defaultPreferences: Preferences = {
  level: 'Beginner',
  goals: ['EverydayConversation'],
  supportLanguage: 'en',
  dailyTargetMinutes: 10,
}
