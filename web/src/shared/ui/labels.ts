import type { CorrectionCategory, EnglishLevel, LearningGoal, PracticeMode } from '../api/schemas'

export const levelLabels: Record<EnglishLevel, { title: string; description: string }> = {
  Beginner: {
    title: 'Beginner',
    description: 'I understand some English but find it hard to speak.',
  },
  Intermediate: {
    title: 'Intermediate',
    description: 'I can hold simple conversations but make mistakes.',
  },
  Advanced: {
    title: 'Advanced',
    description: 'I speak comfortably and want to sound more natural.',
  },
}

export const goalLabels: Record<LearningGoal, string> = {
  EverydayConversation: 'Everyday conversation',
  Work: 'Work and meetings',
  Interviews: 'Job or study interviews',
  Travel: 'Travel',
  Study: 'Study',
  PronunciationClarity: 'Clearer pronunciation',
  Confidence: 'Speaking confidence',
}

export const modeLabels: Record<
  PracticeMode,
  { title: string; description: string; icon: string }
> = {
  DailyConversation: {
    title: 'Daily conversation',
    description: 'Friendly chat about everyday topics.',
    icon: '💬',
  },
  RolePlay: { title: 'Role-play', description: 'Restaurants, shops, travel and more.', icon: '🎭' },
  InterviewPractice: {
    title: 'Interview practice',
    description: 'Realistic questions, one at a time.',
    icon: '💼',
  },
  TopicChallenge: {
    title: 'Topic challenge',
    description: 'Speak at length about one topic.',
    icon: '🎯',
  },
  GuidedBeginner: {
    title: 'Guided beginner',
    description: 'Short, simple prompts at a slower pace.',
    icon: '🌱',
  },
}

export const categoryLabels: Record<CorrectionCategory, string> = {
  Grammar: 'Grammar',
  Vocabulary: 'Vocabulary',
  NaturalPhrasing: 'Natural phrasing',
  Fluency: 'Fluency',
  Pronunciation: 'Pronunciation',
}

/** Must match the backend's SupportLanguages list. */
export const supportLanguages: { code: string; name: string }[] = [
  { code: 'en', name: 'English only' },
  { code: 'hi', name: 'Hindi' },
  { code: 'bn', name: 'Bengali' },
  { code: 'ta', name: 'Tamil' },
  { code: 'te', name: 'Telugu' },
  { code: 'mr', name: 'Marathi' },
  { code: 'ur', name: 'Urdu' },
  { code: 'es', name: 'Spanish' },
  { code: 'pt', name: 'Portuguese' },
  { code: 'fr', name: 'French' },
  { code: 'de', name: 'German' },
  { code: 'it', name: 'Italian' },
  { code: 'ar', name: 'Arabic' },
  { code: 'tr', name: 'Turkish' },
  { code: 'ru', name: 'Russian' },
  { code: 'zh', name: 'Chinese' },
  { code: 'ja', name: 'Japanese' },
  { code: 'ko', name: 'Korean' },
  { code: 'id', name: 'Indonesian' },
  { code: 'vi', name: 'Vietnamese' },
  { code: 'th', name: 'Thai' },
  { code: 'fa', name: 'Persian' },
  { code: 'pl', name: 'Polish' },
  { code: 'uk', name: 'Ukrainian' },
]

export function formatDuration(seconds: number): string {
  if (seconds < 60) return `${Math.max(0, seconds)} sec`
  const minutes = Math.round(seconds / 60)
  return minutes < 60 ? `${minutes} min` : `${Math.floor(minutes / 60)} h ${minutes % 60} min`
}

export function formatDate(iso: string): string {
  return new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}
