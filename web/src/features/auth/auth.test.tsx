import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { anonymousHandlers, authResponse, signedInHandlers } from '../../test/fixtures'
import { renderRoute } from '../../test/renderRoute'
import { server } from '../../test/server'

describe('authentication flow', () => {
  it('restores the session from the refresh cookie on load', async () => {
    server.use(...signedInHandlers)

    renderRoute('/home')

    expect(await screen.findByRole('heading', { name: /hello, asha/i })).toBeInTheDocument()
  })

  it('sends anonymous visitors to the login page', async () => {
    server.use(...anonymousHandlers)

    const { router } = renderRoute('/home')

    expect(await screen.findByRole('heading', { name: /welcome back/i })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })

  it('logs in and returns to the page the user wanted', async () => {
    server.use(
      ...anonymousHandlers,
      http.post('*/api/v1/auth/login', async ({ request }) => {
        const body = (await request.json()) as { email: string; password: string }
        return body.password === 'correct-password'
          ? HttpResponse.json(authResponse())
          : HttpResponse.json({ title: 'Email or password is incorrect.' }, { status: 401 })
      }),
      ...signedInHandlers.slice(1),
    )
    const { user, router } = renderRoute('/history')

    await user.type(await screen.findByLabelText('Email'), 'asha@example.test')
    await user.type(screen.getByLabelText('Password'), 'wrong')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.')

    await user.clear(screen.getByLabelText('Password'))
    await user.type(screen.getByLabelText('Password'), 'correct-password')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/history'))
  })

  it('sends new learners to onboarding before anything else', async () => {
    server.use(
      http.post('*/api/v1/auth/refresh', () =>
        HttpResponse.json(authResponse({ onboardingCompleted: false })),
      ),
      ...signedInHandlers,
    )

    const { router } = renderRoute('/practice/new')

    expect(
      await screen.findByRole('heading', { name: /let's set up your practice/i }),
    ).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/onboarding')
  })

  it('signed-in users skip the login page', async () => {
    server.use(...signedInHandlers)

    const { router } = renderRoute('/login')

    await waitFor(() => expect(router.state.location.pathname).toBe('/home'))
  })
})

describe('registration', () => {
  it('validates fields before calling the API', async () => {
    server.use(...anonymousHandlers)
    const { user } = renderRoute('/register')

    await user.click(await screen.findByRole('button', { name: 'Create account' }))

    expect(screen.getByText('Tell us what to call you.')).toBeInTheDocument()
    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument()
    expect(screen.getByText(/at least 10 characters\. a short phrase/i)).toBeInTheDocument()
    expect(screen.getByText('Please accept the terms to continue.')).toBeInTheDocument()
  })

  it('shows server-side errors next to the right field', async () => {
    server.use(
      ...anonymousHandlers,
      http.post('*/api/v1/auth/register', () =>
        HttpResponse.json(
          { title: 'An account with this email already exists.', code: 'auth.email_taken' },
          { status: 409 },
        ),
      ),
    )
    const { user } = renderRoute('/register')

    await user.type(await screen.findByLabelText(/what should we call you/i), 'Asha')
    await user.type(screen.getByLabelText('Email'), 'asha@example.test')
    await user.type(screen.getByLabelText('Password'), 'a-long-enough-password')
    await user.click(screen.getByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'An account with this email already exists.',
    )
  })

  it('registers and continues to onboarding', async () => {
    server.use(
      ...anonymousHandlers,
      http.post('*/api/v1/auth/register', () =>
        HttpResponse.json(authResponse({ onboardingCompleted: false }), { status: 201 }),
      ),
    )
    const { user, router } = renderRoute('/register')

    await user.type(await screen.findByLabelText(/what should we call you/i), 'Asha')
    await user.type(screen.getByLabelText('Email'), 'asha@example.test')
    await user.type(screen.getByLabelText('Password'), 'a-long-enough-password')
    await user.click(screen.getByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/onboarding'))
  })
})
