import { request } from './httpClient'

export type ApiHealth = 'healthy' | 'unavailable'

export async function fetchApiHealth(signal?: AbortSignal): Promise<ApiHealth> {
  try {
    const response = await request('/health/ready', {
      signal,
      headers: { Accept: 'text/plain' },
    })
    return (await response.text()) === 'Healthy' ? 'healthy' : 'unavailable'
  } catch {
    return 'unavailable'
  }
}
