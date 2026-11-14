import { useQuery } from '@tanstack/react-query'
import { fetchApiHealth } from '../api/health'

export function ApiStatusBadge() {
  const { data, isPending } = useQuery({
    queryKey: ['api-health'],
    queryFn: ({ signal }) => fetchApiHealth(signal),
    refetchInterval: 30_000,
  })

  const label = isPending
    ? 'Checking service…'
    : data === 'healthy'
      ? 'Service online'
      : 'Service unavailable'
  const tone = isPending ? 'pending' : data === 'healthy' ? 'ok' : 'error'

  return (
    <span className={`status-badge status-badge--${tone}`} role="status">
      {label}
    </span>
  )
}
