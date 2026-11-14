import { z } from 'zod'

/**
 * Runtime schemas for every API response. The backend is trusted, but validating at the
 * boundary turns contract drift into a clear error instead of a confusing UI bug.
 */

export const englishLevels = ['Beginner', 'Intermediate', 'Advanced'] as const
export const learningGoals = [
  'EverydayConversation',
  'Work',
  'Interviews',
  'Travel',
  'Study',
  'PronunciationClarity',
  'Confidence',
] as const
export const practiceModes = [
  'DailyConversation',
  'RolePlay',
  'InterviewPractice',
  'TopicChallenge',
  'GuidedBeginner',
] as const
export const correctionCategories = [
  'Grammar',
  'Vocabulary',
  'NaturalPhrasing',
  'Fluency',
  'Pronunciation',
] as const
export const consentTypes = ['TermsAndPrivacy', 'TranscriptStorage', 'ProductImprovement'] as const

export const EnglishLevel = z.enum(englishLevels)
export const LearningGoal = z.enum(learningGoals)
export const PracticeMode = z.enum(practiceModes)
export const CorrectionCategory = z.enum(correctionCategories)
export const ConsentType = z.enum(consentTypes)
export const SessionStatus = z.enum(['Active', 'Completed', 'Abandoned'])
export const Speaker = z.enum(['Coach', 'Learner'])
export const InputMethod = z.enum(['Text', 'Voice'])

export const AuthenticatedUser = z.object({
  id: z.string(),
  email: z.string(),
  displayName: z.string(),
  onboardingCompleted: z.boolean(),
})

export const AuthResponse = z.object({
  accessToken: z.string().min(1),
  expiresAt: z.string(),
  user: AuthenticatedUser,
})

export const Profile = z.object({
  displayName: z.string(),
  level: EnglishLevel,
  goals: z.array(LearningGoal),
  supportLanguage: z.string(),
  dailyTargetMinutes: z.number().int(),
  onboardingCompleted: z.boolean(),
})

export const ConsentStatus = z.object({
  type: ConsentType,
  granted: z.boolean(),
  policyVersion: z.string().nullable(),
  updatedAt: z.string().nullable(),
  required: z.boolean(),
})

export const Turn = z.object({
  sequence: z.number().int(),
  speaker: Speaker,
  text: z.string(),
  inputMethod: InputMethod,
  createdAt: z.string(),
})

export const Correction = z.object({
  category: CorrectionCategory,
  original: z.string(),
  improved: z.string(),
  explanation: z.string(),
})

export const Feedback = z.object({
  summary: z.string(),
  corrections: z.array(Correction),
  usefulPhrases: z.array(z.string()),
  repeatTask: z.string(),
  wasHelpful: z.boolean().nullable(),
  createdAt: z.string(),
})

export const Session = z.object({
  id: z.string(),
  mode: PracticeMode,
  level: EnglishLevel,
  topic: z.string(),
  status: SessionStatus,
  saveTranscript: z.boolean(),
  transcriptCleared: z.boolean(),
  startedAt: z.string(),
  endedAt: z.string().nullable(),
  learnerTurnCount: z.number().int(),
  maxLearnerTurns: z.number().int(),
  turns: z.array(Turn),
  feedback: Feedback.nullable(),
})

export const SessionSummary = z.object({
  id: z.string(),
  mode: PracticeMode,
  topic: z.string(),
  status: SessionStatus,
  startedAt: z.string(),
  endedAt: z.string().nullable(),
  durationSeconds: z.number().int(),
  learnerTurnCount: z.number().int(),
  hasFeedback: z.boolean(),
})

export const pagedSchema = <T extends z.ZodType>(item: T) =>
  z.object({
    items: z.array(item),
    page: z.number().int(),
    pageSize: z.number().int(),
    totalCount: z.number().int(),
    totalPages: z.number().int(),
  })

export const SessionHistory = pagedSchema(SessionSummary)

export const TopicSuggestions = z.array(
  z.object({ mode: PracticeMode, topics: z.array(z.string()) }),
)

export const ProgressSchema = z.object({
  completedSessions: z.number().int(),
  totalPracticeMinutes: z.number().int(),
  todayMinutes: z.number().int(),
  dailyTargetMinutes: z.number().int(),
  currentStreakDays: z.number().int(),
  last7Days: z.array(z.object({ date: z.string(), minutes: z.number().int() })),
  focusAreas: z.array(z.object({ category: CorrectionCategory, count: z.number().int() })),
})

export type EnglishLevel = z.infer<typeof EnglishLevel>
export type LearningGoal = z.infer<typeof LearningGoal>
export type PracticeMode = z.infer<typeof PracticeMode>
export type CorrectionCategory = z.infer<typeof CorrectionCategory>
export type ConsentType = z.infer<typeof ConsentType>
export type InputMethod = z.infer<typeof InputMethod>
export type AuthenticatedUser = z.infer<typeof AuthenticatedUser>
export type AuthResponse = z.infer<typeof AuthResponse>
export type Profile = z.infer<typeof Profile>
export type ConsentStatus = z.infer<typeof ConsentStatus>
export type Turn = z.infer<typeof Turn>
export type Feedback = z.infer<typeof Feedback>
export type Session = z.infer<typeof Session>
export type SessionSummary = z.infer<typeof SessionSummary>
export type SessionHistory = z.infer<typeof SessionHistory>
export type TopicSuggestions = z.infer<typeof TopicSuggestions>
export type Progress = z.infer<typeof ProgressSchema>
