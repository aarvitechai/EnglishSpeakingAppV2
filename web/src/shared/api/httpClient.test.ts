import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../../test/server'
import { ApiError, request } from './httpClient'

describe('request', () => {
  it('sends JSON bodies with a JSON content type', async () => {
    let received: unknown
    let contentType: string | null = null
    server.use(
      http.post('*/api/echo', async ({ request: req }) => {
        contentType = req.headers.get('Content-Type')
        received = await req.json()
        return HttpResponse.json({ ok: true })
      }),
    )

    await request('/api/echo', { method: 'POST', body: { hello: 'world' } })

    expect(contentType).toBe('application/json')
    expect(received).toEqual({ hello: 'world' })
  })

  it('throws ApiError with problem details on failure', async () => {
    server.use(
      http.get('*/api/broken', () =>
        HttpResponse.json(
          { title: 'Validation failed', detail: 'Email is required' },
          { status: 400 },
        ),
      ),
    )

    const error = await request('/api/broken').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({
      status: 400,
      message: 'Validation failed',
      detail: 'Email is required',
    })
  })

  it('falls back to a generic message when the error body is not JSON', async () => {
    server.use(http.get('*/api/down', () => new HttpResponse('oops', { status: 503 })))

    await expect(request('/api/down')).rejects.toMatchObject({
      status: 503,
      message: 'Request failed with status 503',
    })
  })
})
