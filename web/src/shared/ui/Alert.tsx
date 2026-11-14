import type { ReactNode } from 'react'

type Tone = 'error' | 'info' | 'success' | 'warning'

export function Alert({
  tone = 'info',
  children,
  action,
}: {
  tone?: Tone
  children: ReactNode
  action?: ReactNode
}) {
  return (
    <div className={`alert alert--${tone}`} role={tone === 'error' ? 'alert' : 'status'}>
      <div className="alert__body">{children}</div>
      {action}
    </div>
  )
}
