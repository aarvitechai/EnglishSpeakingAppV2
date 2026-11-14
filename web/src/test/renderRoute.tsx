import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { routes } from '../app/routes'
import { AuthProvider } from '../features/auth/AuthProvider'
import { setAccessToken } from '../shared/api/httpClient'

/** Renders the real app routes at a URL, with real providers and a fresh cache. Network is mocked by MSW. */
export function renderRoute(path: string) {
  setAccessToken(null)
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  const user = userEvent.setup()

  const utils = render(
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>,
  )

  return { ...utils, router, user, queryClient }
}
