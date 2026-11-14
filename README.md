# SpeakMate AI

An AI-powered English speaking-practice platform: learners talk with a patient AI coach, get a few high-value corrections, and track their progress.

> **SpeakMate AI** is a working name (placeholder). The full product plan is in [`AI_English_Practice_Web_Mobile_App_Plan.md`](AI_English_Practice_Web_Mobile_App_Plan.md).

## Tech stack

| Layer | Technology |
|---|---|
| API | ASP.NET Core 10 (minimal APIs), clean architecture |
| Database | SQL Server, EF Core 10 code-first migrations |
| AI | Google Gemini API, called **only** from the backend |
| Web | React 19 + TypeScript + Vite, TanStack Query, React Router |
| Tests | xUnit v3 (unit + integration against real SQL Server), Vitest + Testing Library + MSW |
| CI | GitHub Actions (`.github/workflows/ci.yml`) |

## Repository layout

```text
backend/
  src/
    SpeakMate.Domain/          Entities and business rules. No framework dependencies.
    SpeakMate.Application/     Use cases, interfaces (ports), DTOs, validation.
    SpeakMate.Infrastructure/  EF Core, SQL Server, Gemini client, identity: implements the ports.
    SpeakMate.Api/             HTTP endpoints, auth, middleware, composition root.
  tests/
    SpeakMate.UnitTests/         Fast tests: domain, application services, architecture rules.
    SpeakMate.IntegrationTests/  Full HTTP tests against the in-memory host and a real test database.
web/                           React web app.
```

Dependencies point inward only: `Api → Infrastructure → Application → Domain`. `tests/SpeakMate.UnitTests/Architecture` enforces this.

## Prerequisites

- .NET SDK 10 (pinned in `global.json`)
- Node.js 22 or later
- SQL Server (local instance `DESKTOP-2ERCDGB\MSSQLSERVER01`, Windows authentication)

## Run locally

```bash
# 1. API: https://localhost:7080 (API reference UI at /scalar in Development)
cd backend
dotnet run --project src/SpeakMate.Api --launch-profile https

# 2. Web: http://localhost:5173 (proxies /api and /health to the API)
cd web
npm install
npm run dev
```

## Test

```bash
cd backend && dotnet test          # unit + integration
cd web && npm test                 # component and unit tests
cd web && npm run test:coverage    # with coverage report
```

## Branching and release flow

| Branch | Purpose |
|---|---|
| `dev` | Integration branch. All work is committed here first. |
| `staging` | Pre-production. Updated by merging `dev`. |
| `production` | Released code. Updated by merging `staging`. |

Each phase is committed on `dev`, then promoted: `dev → staging → production`, using `--no-ff` merges so every promotion is visible in the history.

## Security principles

- The Gemini API key lives only in server-side secrets (user-secrets locally, a secret store in production). It is never in the web bundle, Git or logs.
- Users can only read and modify their own data. This is enforced on the server.
- No raw audio is stored. Transcripts are saved only with the learner's consent.
