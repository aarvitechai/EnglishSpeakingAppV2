/**
 * Thin fetch wrapper. All requests go to our own backend (same origin, proxied in dev);
 * the frontend never talks to the AI provider directly and never holds provider keys.
 */
export class ApiError extends Error {
  readonly status: number
  readonly detail: string | undefined

  constructor(status: number, message: string, detail?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.detail = detail
  }
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
  headers?: Record<string, string>
}

interface ProblemDetails {
  title?: string
  detail?: string
}

async function readProblem(response: Response): Promise<ProblemDetails> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return {}
  }
}

export async function request(path: string, options: RequestOptions = {}): Promise<Response> {
  const headers: Record<string, string> = { Accept: 'application/json', ...options.headers }
  let body: string | undefined
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
    body = JSON.stringify(options.body)
  }

  const response = await fetch(path, {
    method: options.method ?? 'GET',
    headers,
    body,
    signal: options.signal,
    credentials: 'same-origin',
  })

  if (!response.ok) {
    const problem = await readProblem(response)
    throw new ApiError(
      response.status,
      problem.title ?? `Request failed with status ${response.status}`,
      problem.detail,
    )
  }

  return response
}
