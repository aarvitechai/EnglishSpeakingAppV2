import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { z } from 'zod'
import { server } from '../../test/server'
import {
  ApiError,
  errorMessage,
  getAccessToken,
  request,
  requestJson,
  setAccessToken,
  setRefreshHandler,
} from './httpClient'

afterEach(() => {
  setAccessToken(null)
  setRefreshHandler(null)
})

describe('request', () => {
  it('sends JSON bodies with a JSON content type and the bearer token', async () => {
    let received: unknown
    let headers: Headers | undefined
    server.use(
      http.post('*/api/echo', async ({ request: req }) => {
        headers = req.headers
        received = await req.json()
        return HttpResponse.json({ ok: true })
      }),
    )
    setAccessToken('abc')

    await request('/api/echo', { method: 'POST', body: { hello: 'world' } })

    expect(headers?.get('Content-Type')).toBe('application/json')
    expect(headers?.get('Authorization')).toBe('Bearer abc')
    expect(received).toEqual({ hello: 'world' })
  })

  it('does not attach the token to anonymous requests', async () => {
    let authorization: string | null = 'unset'
    server.use(
      http.post('*/api/v1/auth/login', ({ request: req }) => {
        authorization = req.headers.get('Authorization')
        return HttpResponse.json({})
      }),
    )
    setAccessToken('abc')

    await request('/api/v1/auth/login', { method: 'POST', anonymous: true })

    expect(authorization).toBeNull()
  })

  it('throws ApiError with problem details, code and field errors', async () => {
    server.use(
      http.get('*/api/broken', () =>
        HttpResponse.json(
          {
            title: 'Some fields need attention.',
            code: 'request.invalid',
            errors: { email: ['Required'] },
          },
          { status: 400 },
        ),
      ),
    )

    const error = await request('/api/broken').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({
      status: 400,
      code: 'request.invalid',
      fieldErrors: { email: ['Required'] },
    })
  })

  it('falls back to a generic message when the error body is not JSON', async () => {
    server.use(http.get('*/api/down', () => new HttpResponse('oops', { status: 503 })))

    await expect(request('/api/down')).rejects.toMatchObject({
      status: 503,
      message: 'Request failed with status 503',
    })
  })

  it('on 401, refreshes once and retries with the new token', async () => {
    const seen: (string | null)[] = []
    server.use(
      http.get('*/api/secure', ({ request: req }) => {
        const auth = req.headers.get('Authorization')
        seen.push(auth)
        return auth === 'Bearer fresh'
          ? HttpResponse.json({ ok: true })
          : new HttpResponse(null, { status: 401 })
      }),
    )
    setAccessToken('stale')
    setRefreshHandler(async () => {
      setAccessToken('fresh')
      return 'fresh'
    })

    const response = await request('/api/secure')

    expect(response.status).toBe(200)
    expect(seen).toEqual(['Bearer stale', 'Bearer fresh'])
    expect(getAccessToken()).toBe('fresh')
  })

  it('shares one refresh between concurrent 401s (refresh tokens are single-use)', async () => {
    server.use(
      http.get('*/api/secure', ({ request: req }) =>
        req.headers.get('Authorization') === 'Bearer fresh'
          ? HttpResponse.json({ ok: true })
          : new HttpResponse(null, { status: 401 }),
      ),
    )
    const refresh = vi.fn(async () => {
      await new Promise((r) => setTimeout(r, 10))
      return 'fresh'
    })
    setRefreshHandler(refresh)

    await Promise.all([request('/api/secure'), request('/api/secure'), request('/api/secure')])

    expect(refresh).toHaveBeenCalledTimes(1)
  })

  it('surfaces the 401 when refresh fails', async () => {
    server.use(
      http.get('*/api/secure', () => HttpResponse.json({ title: 'Unauthorized' }, { status: 401 })),
    )
    setRefreshHandler(async () => null)

    await expect(request('/api/secure')).rejects.toMatchObject({ status: 401 })
  })
})

describe('requestJson', () => {
  it('rejects responses that do not match the schema', async () => {
    server.use(http.get('*/api/thing', () => HttpResponse.json({ name: 42 })))

    await expect(requestJson('/api/thing', z.object({ name: z.string() }))).rejects.toMatchObject({
      code: 'client.contract_mismatch',
    })
  })
})

describe('errorMessage', () => {
  it('maps network failures to a friendly message', () => {
    expect(errorMessage(new TypeError('Failed to fetch'))).toMatch(/network/i)
    expect(errorMessage(new ApiError(400, 'Bad input'))).toBe('Bad input')
    expect(errorMessage('weird')).toMatch(/something went wrong/i)
  })
})
