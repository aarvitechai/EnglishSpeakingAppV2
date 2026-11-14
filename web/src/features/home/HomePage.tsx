import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { progressApi, queryKeys, sessionsApi } from '../../shared/api/endpoints'
import { errorMessage } from '../../shared/api/httpClient'
import type { Progress } from '../../shared/api/schemas'
import { Alert } from '../../shared/ui/Alert'
import { Spinner } from '../../shared/ui/Spinner'
import { categoryLabels, formatDate, formatDuration, modeLabels } from '../../shared/ui/labels'
import { useAuth } from '../auth/useAuth'

function WeekChart({ days }: { days: Progress['last7Days'] }) {
  const max = Math.max(10, ...days.map((d) => d.minutes))
  return (
    <figure className="week-chart" aria-label="Practice minutes over the last 7 days">
      <ol className="week-chart__bars">
        {days.map((d) => {
          const label = new Date(`${d.date}T12:00:00`).toLocaleDateString(undefined, {
            weekday: 'short',
          })
          return (
            <li key={d.date} className="week-chart__day">
              <span className="week-chart__value">{d.minutes > 0 ? d.minutes : ''}</span>
              <span
                className="week-chart__bar"
                style={{ height: `${Math.max(4, (d.minutes / max) * 100)}%` }}
                data-empty={d.minutes === 0 || undefined}
                title={`${label}: ${d.minutes} min`}
              />
              <span className="week-chart__label">{label}</span>
            </li>
          )
        })}
      </ol>
      <figcaption className="visually-hidden">
        {days.map((d) => `${d.date}: ${d.minutes} minutes`).join(', ')}
      </figcaption>
    </figure>
  )
}

export function HomePage() {
  const { user } = useAuth()
  const progress = useQuery({
    queryKey: queryKeys.progress,
    queryFn: ({ signal }) => progressApi.get(signal),
  })
  const recent = useQuery({
    queryKey: queryKeys.history(1),
    queryFn: ({ signal }) => sessionsApi.history(1, 5, signal),
  })

  const p = progress.data
  const targetPct = p
    ? Math.min(100, Math.round((p.todayMinutes / Math.max(1, p.dailyTargetMinutes)) * 100))
    : 0

  return (
    <div className="stack-lg">
      <section className="hero">
        <h1>Hello, {user?.displayName} 👋</h1>
        <p className="muted">A few minutes of speaking every day makes a real difference.</p>
        <Link to="/practice/new" className="btn btn--primary btn--lg">
          Start a practice session
        </Link>
      </section>

      {progress.isPending && <Spinner label="Loading progress…" />}
      {progress.error && <Alert tone="error">{errorMessage(progress.error)}</Alert>}

      {p && (
        <section className="grid-3" aria-label="Your progress">
          <article className="card stat">
            <h2 className="stat__label">Today</h2>
            <p className="stat__value">
              {p.todayMinutes} <span className="stat__unit">/ {p.dailyTargetMinutes} min</span>
            </p>
            <div
              className="progress-bar"
              role="progressbar"
              aria-label="Daily goal"
              aria-valuenow={targetPct}
              aria-valuemin={0}
              aria-valuemax={100}
            >
              <span style={{ width: `${targetPct}%` }} />
            </div>
          </article>
          <article className="card stat">
            <h2 className="stat__label">Streak</h2>
            <p className="stat__value">
              {p.currentStreakDays}{' '}
              <span className="stat__unit">{p.currentStreakDays === 1 ? 'day' : 'days'}</span>
            </p>
            <p className="muted small">Days in a row with practice</p>
          </article>
          <article className="card stat">
            <h2 className="stat__label">Completed sessions</h2>
            <p className="stat__value">{p.completedSessions}</p>
            <p className="muted small">{p.totalPracticeMinutes} minutes in the last 90 days</p>
          </article>
        </section>
      )}

      {p && (
        <div className="grid-2">
          <section className="card">
            <h2>Last 7 days</h2>
            <WeekChart days={p.last7Days} />
          </section>
          <section className="card">
            <h2>Focus areas</h2>
            {p.focusAreas.length === 0 ? (
              <p className="muted">
                Finish a session to see which areas your feedback mentions most.
              </p>
            ) : (
              <ul className="focus-list">
                {p.focusAreas.map((f) => (
                  <li key={f.category}>
                    <span>{categoryLabels[f.category]}</span>
                    <span className="badge">{f.count}</span>
                  </li>
                ))}
              </ul>
            )}
            <p className="muted small">
              Based on the corrections in your recent feedback, not a test score.
            </p>
          </section>
        </div>
      )}

      <section className="card">
        <div className="row-between">
          <h2>Recent sessions</h2>
          <Link to="/history">See all</Link>
        </div>
        {recent.isPending && <Spinner />}
        {recent.data?.items.length === 0 && (
          <p className="muted">No sessions yet. Your first one is a click away.</p>
        )}
        <ul className="session-list">
          {recent.data?.items.map((s) => (
            <li key={s.id}>
              <Link
                to={s.status === 'Active' ? `/practice/${s.id}` : `/practice/${s.id}/summary`}
                className="session-list__item"
              >
                <span aria-hidden="true">{modeLabels[s.mode].icon}</span>
                <span className="session-list__main">
                  <strong>{s.topic}</strong>
                  <span className="muted small">
                    {formatDate(s.startedAt)} · {formatDuration(s.durationSeconds)} ·{' '}
                    {s.learnerTurnCount} messages
                  </span>
                </span>
                <span className={`badge badge--${s.status.toLowerCase()}`}>
                  {s.status === 'Active' ? 'Continue' : s.status}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}
