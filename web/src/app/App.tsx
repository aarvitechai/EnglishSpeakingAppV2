import { QueryClientProvider } from '@tanstack/react-query'
import { useState } from 'react'
import { ApiStatusBadge } from '../shared/ui/ApiStatusBadge'
import { createQueryClient } from './queryClient'

export function App() {
  const [queryClient] = useState(createQueryClient)

  return (
    <QueryClientProvider client={queryClient}>
      <div className="app-shell">
        <header className="app-header">
          <span className="brand">SpeakMate AI</span>
          <ApiStatusBadge />
        </header>
        <main className="app-main">
          <h1>Practise speaking English, one conversation at a time.</h1>
          <p>Your patient AI coach is getting ready.</p>
        </main>
      </div>
    </QueryClientProvider>
  )
}
