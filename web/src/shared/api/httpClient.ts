import type { z } from 'zod'

/**
 * HTTP client for our own API (same origin; proxied to the backend in development).
 *
 * Security model:
 * - The access token lives only in this module's memory: never localStorage, so XSS can't read it from storage.
 * - The refresh token is an HttpOnly cookie the browser sends to /api/v1/auth only; JS never sees it.
 * - On a 401, one shared refresh attempt runs, then the original request is retried once.
 */

export class ApiError extends Error {
  readonly status: number
  readonly code: string | undefined
  readonly detail: string | undefined
  readonly fieldErrors: Record<string, string[]>

  constructor(
    status: number,
    message: string,
    options: { code?: string; detail?: string; fieldErrors?: Record<string, string[]> } = {},
  ) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = options.code
    this.detail = options.detail
    this.fieldErrors = options.fieldErrors ?? {}
  }
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
  headers?: Record<string, string>
  /** Skip attaching the bearer token and the refresh-and-retry logic (used by auth endpoints). */
  anonymous?: boolean
}

interface ProblemDetails {
  title?: string
  detail?: string
  code?: string
  errors?: Record<string, string[]>
}

let accessToken: string | null = null
let refreshHandler: (() => Promise<string | null>) | null = null
let refreshInFlight: Promise<string | null> | null = null

export function setAccessToken(token: string | null) {
  accessToken = token
}

export function getAccessToken() {
  return accessToken
}

/** Registered by the auth layer; returns a new access token or null if the session is over. */
export function setRefreshHandler(handler: (() => Promise<string | null>) | null) {
  refreshHandler = handler
}

/** Concurrent 401s share one refresh call (refresh tokens are single-use). */
function refreshOnce(): Promise<string | null> {
  if (!refreshHandler) return Promise.resolve(null)
  refreshInFlight ??= refreshHandler().finally(() => {
    refreshInFlight = null
  })
  return refreshInFlight
}

async function readProblem(response: Response): Promise<ProblemDetails> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return {}
  }
}

async function send(
  path: string,
  options: RequestOptions,
  token: string | null,
): Promise<Response> {
  const headers: Record<string, string> = { Accept: 'application/json', ...options.headers }
  let body: string | undefined
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
    body = JSON.stringify(options.body)
  }
  if (token && !options.anonymous) {
    headers.Authorization = `Bearer ${token}`
  }

  return fetch(path, {
    method: options.method ?? 'GET',
    headers,
    body,
    signal: options.signal,
    credentials: 'same-origin',
  })
}

export async function request(path: string, options: RequestOptions = {}): Promise<Response> {
  let response = await send(path, options, accessToken)

  if (response.status === 401 && !options.anonymous && refreshHandler) {
    const newToken = await refreshOnce()
    if (newToken) {
      response = await send(path, options, newToken)
    }
  }

  if (!response.ok) {
    const problem = await readProblem(response)
    throw new ApiError(
      response.status,
      problem.title ?? `Request failed with status ${response.status}`,
      {
        code: problem.code,
        detail: problem.detail,
        fieldErrors: problem.errors,
      },
    )
  }

  return response
}

/** Request + JSON parse + schema validation. */
export async function requestJson<T extends z.ZodType>(
  path: string,
  schema: T,
  options: RequestOptions = {},
): Promise<z.infer<T>> {
  const response = await request(path, options)
  const json: unknown = await response.json()
  const parsed = schema.safeParse(json)
  if (!parsed.success) {
    throw new ApiError(500, 'The server sent an unexpected response.', {
      code: 'client.contract_mismatch',
    })
  }
  return parsed.data
}

/** Human-friendly message for any thrown value. */
export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) return error.message
  if (error instanceof TypeError) return 'Network problem. Check your connection and try again.'
  return 'Something went wrong. Please try again.'
}
