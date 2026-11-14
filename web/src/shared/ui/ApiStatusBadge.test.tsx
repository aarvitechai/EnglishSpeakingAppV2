import { screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { renderWithProviders } from '../../test/render'
import { server } from '../../test/server'
import { ApiStatusBadge } from './ApiStatusBadge'

describe('ApiStatusBadge', () => {
  it('shows online when the readiness check is healthy', async () => {
    server.use(http.get('*/health/ready', () => HttpResponse.text('Healthy')))

    renderWithProviders(<ApiStatusBadge />)

    expect(await screen.findByText('Service online')).toBeInTheDocument()
  })

  it('shows unavailable when the API is down', async () => {
    server.use(http.get('*/health/ready', () => new HttpResponse('Unhealthy', { status: 503 })))

    renderWithProviders(<ApiStatusBadge />)

    expect(await screen.findByText('Service unavailable')).toBeInTheDocument()
  })
})
