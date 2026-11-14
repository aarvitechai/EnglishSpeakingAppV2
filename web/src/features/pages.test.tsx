import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { authResponse, profile, signedInHandlers } from '../test/fixtures'
import { renderRoute } from '../test/renderRoute'
import { server } from '../test/server'

describe('home dashboard', () => {
  it('shows today, streak and focus areas from the progress API', async () => {
    server.use(...signedInHandlers)

    renderRoute('/home')

    expect(await screen.findByText('Completed sessions')).toBeInTheDocument()
    expect(screen.getByText('/ 15 min')).toBeInTheDocument()
    expect(screen.getByRole('progressbar', { name: 'Daily goal' })).toHaveAttribute(
      'aria-valuenow',
      '40',
    )
    expect(screen.getByText('Grammar')).toBeInTheDocument()
    expect(screen.getByText(/no sessions yet/i)).toBeInTheDocument()
  })
})

describe('onboarding', () => {
  it('limits goals to three and saves preferences', async () => {
    let saved: { goals?: string[]; supportLanguage?: string } = {}
    server.use(
      http.post('*/api/v1/auth/refresh', () =>
        HttpResponse.json(authResponse({ onboardingCompleted: false })),
      ),
      ...signedInHandlers,
      http.put('*/api/v1/me/onboarding', async ({ request }) => {
        saved = (await request.json()) as typeof saved
        return HttpResponse.json({ ...profile, ...saved })
      }),
    )
    const { user, router } = renderRoute('/onboarding')

    await user.click(await screen.findByLabelText('Work and meetings'))
    await user.click(screen.getByLabelText('Travel'))
    await user.click(screen.getByLabelText('Study'))

    expect(screen.getByRole('alert')).toHaveTextContent('Choose up to 3 goals.')
    expect(screen.getByLabelText('Study')).not.toBeChecked()

    await user.selectOptions(screen.getByLabelText(/language for short explanations/i), 'ta')
    await user.click(screen.getByRole('button', { name: 'Start practising' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/home'))
    expect(saved.goals).toEqual(['EverydayConversation', 'Work', 'Travel'])
    expect(saved.supportLanguage).toBe('ta')
  })
})

describe('settings', () => {
  it('toggles optional consent but not the required one', async () => {
    let consentGranted = false
    server.use(
      ...signedInHandlers,
      http.get('*/api/v1/me/consents', () =>
        HttpResponse.json([
          {
            type: 'TermsAndPrivacy',
            granted: true,
            policyVersion: '2026-11',
            updatedAt: null,
            required: true,
          },
          {
            type: 'TranscriptStorage',
            granted: consentGranted,
            policyVersion: null,
            updatedAt: null,
            required: false,
          },
          {
            type: 'ProductImprovement',
            granted: false,
            policyVersion: null,
            updatedAt: null,
            required: false,
          },
        ]),
      ),
      http.put('*/api/v1/me/consents/TranscriptStorage', async ({ request }) => {
        consentGranted = ((await request.json()) as { granted: boolean }).granted
        return HttpResponse.json({
          type: 'TranscriptStorage',
          granted: consentGranted,
          policyVersion: '2026-11',
          updatedAt: null,
          required: false,
        })
      }),
    )
    const { user } = renderRoute('/settings')

    const terms = await screen.findByRole('switch', { name: 'Terms and privacy policy' })
    const transcripts = screen.getByRole('switch', { name: 'Keep my conversation transcripts' })
    expect(terms).toBeDisabled()
    expect(transcripts).not.toBeChecked()

    await user.click(transcripts)

    await waitFor(() =>
      expect(
        screen.getByRole('switch', { name: 'Keep my conversation transcripts' }),
      ).toBeChecked(),
    )
  })

  it('requires the password before deleting the account', async () => {
    let deleted = false
    server.use(
      ...signedInHandlers,
      http.get('*/api/v1/me/consents', () => HttpResponse.json([])),
      http.delete('*/api/v1/me', async ({ request }) => {
        const body = (await request.json()) as { password: string }
        if (body.password !== 'right-password') {
          return HttpResponse.json({ title: 'Password is incorrect.' }, { status: 400 })
        }
        deleted = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { user, router } = renderRoute('/settings')

    await user.click(await screen.findByRole('button', { name: /delete my account/i }))
    const deleteButton = screen.getByRole('button', { name: 'Permanently delete' })
    expect(deleteButton).toBeDisabled()

    await user.type(screen.getByLabelText(/confirm with your password/i), 'wrong')
    await user.click(deleteButton)
    expect(await screen.findByText('Password is incorrect.')).toBeInTheDocument()

    await user.clear(screen.getByLabelText(/confirm with your password/i))
    await user.type(screen.getByLabelText(/confirm with your password/i), 'right-password')
    await user.click(screen.getByRole('button', { name: 'Permanently delete' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'))
    expect(deleted).toBe(true)
    expect(
      await screen.findByText(/all your data have been permanently deleted/i),
    ).toBeInTheDocument()
  })
})
