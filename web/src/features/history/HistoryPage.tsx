import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { queryKeys, sessionsApi } from '../../shared/api/endpoints'
import { errorMessage } from '../../shared/api/httpClient'
import { Alert } from '../../shared/ui/Alert'
import { Button } from '../../shared/ui/Form'
import { Spinner } from '../../shared/ui/Spinner'
import { formatDate, formatDuration, modeLabels } from '../../shared/ui/labels'

export const HISTORY_PAGE_SIZE = 10

export function HistoryPage() {
  const [page, setPage] = useState(1)
  const history = useQuery({
    queryKey: queryKeys.history(page),
    queryFn: ({ signal }) => sessionsApi.history(page, HISTORY_PAGE_SIZE, signal),
    placeholderData: keepPreviousData,
  })

  return (
    <div className="stack-lg">
      <h1>Practice history</h1>
      {history.isPending && <Spinner label="Loading history…" />}
      {history.error && <Alert tone="error">{errorMessage(history.error)}</Alert>}
      {history.data && history.data.totalCount === 0 && (
        <div className="card center">
          <p>You haven't practised yet.</p>
          <Link to="/practice/new" className="btn btn--primary">
            Start your first session
          </Link>
        </div>
      )}

      {history.data && history.data.totalCount > 0 && (
        <section className="card">
          <ul className="session-list">
            {history.data.items.map((s) => (
              <li key={s.id}>
                <Link
                  to={s.status === 'Active' ? `/practice/${s.id}` : `/practice/${s.id}/summary`}
                  className="session-list__item"
                >
                  <span aria-hidden="true">{modeLabels[s.mode].icon}</span>
                  <span className="session-list__main">
                    <strong>{s.topic}</strong>
                    <span className="muted small">
                      {modeLabels[s.mode].title} · {formatDate(s.startedAt)} ·{' '}
                      {formatDuration(s.durationSeconds)} · {s.learnerTurnCount} messages
                    </span>
                  </span>
                  <span className={`badge badge--${s.status.toLowerCase()}`}>
                    {s.status === 'Active' ? 'Continue' : s.hasFeedback ? 'Feedback' : s.status}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
          <nav className="pager" aria-label="History pages">
            <Button variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
              Previous
            </Button>
            <span className="muted">
              Page {history.data.page} of {Math.max(1, history.data.totalPages)}
            </span>
            <Button
              variant="secondary"
              disabled={page >= history.data.totalPages}
              onClick={() => setPage((p) => p + 1)}
            >
              Next
            </Button>
          </nav>
        </section>
      )}
    </div>
  )
}
