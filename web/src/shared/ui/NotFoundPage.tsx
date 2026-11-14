import { Link } from 'react-router'

export function NotFoundPage() {
  return (
    <div className="card center">
      <h1>Page not found</h1>
      <Link to="/home">Go home</Link>
    </div>
  )
}
