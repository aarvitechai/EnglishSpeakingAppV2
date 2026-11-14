export function Spinner({ label = 'Loading…' }: { label?: string }) {
  return (
    <span className="spinner" role="status" aria-live="polite">
      <span className="spinner__dot" aria-hidden="true" />
      <span className="visually-hidden">{label}</span>
    </span>
  )
}

export function FullPageSpinner({ label }: { label: string }) {
  return (
    <div className="full-page-center">
      <Spinner label={label} />
      <p className="muted">{label}</p>
    </div>
  )
}
