import { Navigate, type RouteObject } from 'react-router'
import { LoginPage } from '../features/auth/LoginPage'
import { RegisterPage } from '../features/auth/RegisterPage'
import { RedirectIfAuthenticated, RequireAuth } from '../features/auth/RouteGuards'
import { HistoryPage } from '../features/history/HistoryPage'
import { HomePage } from '../features/home/HomePage'
import { ConversationPage } from '../features/practice/ConversationPage'
import { PracticeSetupPage } from '../features/practice/PracticeSetupPage'
import { SessionSummaryPage } from '../features/practice/SessionSummaryPage'
import { OnboardingPage } from '../features/profile/OnboardingPage'
import { SettingsPage } from '../features/settings/SettingsPage'
import { AppLayout, PublicLayout } from '../shared/ui/AppLayout'
import { NotFoundPage } from '../shared/ui/NotFoundPage'

export const routes: RouteObject[] = [
  {
    element: <RedirectIfAuthenticated />,
    children: [
      {
        element: <PublicLayout />,
        children: [
          { path: '/login', element: <LoginPage /> },
          { path: '/register', element: <RegisterPage /> },
        ],
      },
    ],
  },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { path: '/onboarding', element: <OnboardingPage /> },
          { path: '/home', element: <HomePage /> },
          { path: '/practice/new', element: <PracticeSetupPage /> },
          { path: '/practice/:sessionId', element: <ConversationPage /> },
          { path: '/practice/:sessionId/summary', element: <SessionSummaryPage /> },
          { path: '/history', element: <HistoryPage /> },
          { path: '/settings', element: <SettingsPage /> },
        ],
      },
    ],
  },
  { path: '/', element: <Navigate to="/home" replace /> },
  { path: '*', element: <NotFoundPage /> },
]
