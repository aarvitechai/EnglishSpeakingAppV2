# SpeakMate AI

An AI-powered English speaking-practice platform: learners talk with a patient AI coach, get a few high-value corrections, and track their progress.

> **SpeakMate AI** is a working name (placeholder). The full product plan is in [`AI_English_Practice_Web_Mobile_App_Plan.md`](AI_English_Practice_Web_Mobile_App_Plan.md).

## Try it locally

| What | URL |
|---|---|
| **Web app** | **http://localhost:5173** |
| API reference (Scalar, dev only) | https://localhost:7080/scalar |
| Health | http://localhost:5173/health/ready |

Start both servers (see [Run locally](#run-locally)), open the web app, create an account, finish onboarding and start a session. Without a Gemini key the API uses an offline demo coach, labelled "offline demo mode". Set a key to talk to the real AI.

## Documentation

- [Architecture](docs/architecture.md): layers, design decisions, request flow, scaling
- [Security and privacy](docs/security.md): threat model with controls and the tests that prove them
- [Operations runbook](docs/operations.md): configuration, deploy, monitoring, incidents

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

## One-time secret setup

Secrets are never stored in the repository. Locally they live in .NET user-secrets (in your Windows profile); in production, use environment variables or a vault.

```bash
cd backend
dotnet tool restore
# JWT signing key (at least 32 bytes, random)
dotnet user-secrets set "Jwt:SigningKey" "$(python -c "import secrets;print(secrets.token_urlsafe(48))")" --project src/SpeakMate.Api
# Gemini API key (Phase 4)
dotnet user-secrets set "Gemini:ApiKey" "<your-key>" --project src/SpeakMate.Api
```

In production, set the same values as `Jwt__SigningKey`, `Gemini__ApiKey` and `ConnectionStrings__SpeakMateDb`.

## Authentication design

| Concern | Approach |
|---|---|
| Passwords | ASP.NET Core Identity (PBKDF2 hashing). At least 10 characters, no forced character classes (NIST 800-63B). |
| Brute force | Account lockout after 5 failures (15 min) and a per-IP rate limit on `/api/v1/auth/*` (10 per minute). |
| Access token | JWT (HS256), valid 15 minutes, returned in the body and kept **in memory** by the web app. |
| Refresh token | 256-bit random value in an `HttpOnly; Secure; SameSite=Strict; Path=/api/v1/auth` cookie. Only its SHA-256 hash is stored. Single-use rotation; replaying a spent token revokes the whole token family. |
| Authorization | A fallback policy requires authentication on every endpoint unless it is explicitly `AllowAnonymous`. User IDs always come from the token, never from the URL. |
| Account deletion | All learner tables cascade from `auth.Users`. |

## AI coach (Gemini)

- **Server-side only:** the backend calls `POST v1beta/models/{model}:generateContent` with the key in the `x-goog-api-key` header. Clients never see the key.
- **Model:** `gemini-3.5-flash-lite` by default (the cheapest stable model as of November 2026), set in `Gemini:Model`. Check [Google's model list](https://aistudio.google.com/docs/models) before changing it.
- **Structured output:** replies must match a JSON schema (`{"reply": "..."}`). Empty, incomplete, blocked, overlong or malformed replies are rejected and the learner gets a friendly 503 to retry. Nothing is half-saved.
- **Resilience:** retries with jittered back-off on 429/5xx, per-attempt and total timeouts, and a circuit breaker.
- **Cost control:** every call is recorded in `app.UsageEvents` (tokens and estimated USD, never text). There's a rolling 24-hour token quota per learner, a cap of 3 active sessions, a cap of 30 messages per session, only the last 20 turns are sent as context, and AI endpoints are rate-limited to 20 requests per minute per user.
- **Providers:** `Ai:Provider` is `Gemini` (production; startup fails without a key), `Auto` (development: Gemini if a key is set, otherwise the offline fake coach) or `Fake` (tests).
- **Privacy:** Google's free tier may use submitted content to improve its products. Use a **paid** API key before real learners use the app.

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

## Database

- **Development database:** `EnglishSpeakingAppV2DB` on `DESKTOP-2ERCDGB\MSSQLSERVER01`, Windows authentication.
- **Test database:** `EnglishSpeakingAppV2DB_Test`. Integration tests drop and rebuild it from the migrations on every run, and refuse to run against any database whose name doesn't end in `_Test`.
- All tables live in the `app` schema. Enums are stored as readable strings.

```bash
# from backend/ (the dotnet-ef version is pinned in dotnet-tools.json: run `dotnet tool restore` once)
dotnet tool run dotnet-ef migrations add <Name> --project src/SpeakMate.Infrastructure --startup-project src/SpeakMate.Api --output-dir Persistence/Migrations
dotnet tool run dotnet-ef database update --project src/SpeakMate.Infrastructure --startup-project src/SpeakMate.Api

# production: generate a reviewed, idempotent script instead of migrating at app startup
dotnet tool run dotnet-ef migrations script --idempotent --project src/SpeakMate.Infrastructure --startup-project src/SpeakMate.Api -o migrate.sql
```

The API never migrates the database automatically on startup. Schema changes are deployed as a reviewed step.

## Test

```bash
cd backend && dotnet test          # 197 tests: unit + integration (real SQL Server test database)
cd web && npm test                 # 30 tests: unit + integration (real routes, mocked network)
cd web && npm run test:coverage    # with coverage report
cd web && npm run lint && npm run typecheck && npm run format:check
```

| Suite | What it covers |
|---|---|
| Backend unit | Domain invariants, application services (with fakes), prompt rules, Gemini wire contract, feedback sanitising, progress maths, architecture rules |
| Backend integration | Real HTTP pipeline + SQL Server: auth flows, lockout, token rotation and reuse detection, rate limits, data isolation between users, sessions, feedback, retention, export, account deletion cascade, production config guards |
| Web | HTTP client refresh logic, auth and routing guards, onboarding, conversation (incl. simulated speech recognition), feedback retry, consent toggles, account deletion |

## Production deployment

Dockerfiles: `backend/Dockerfile` (non-root ASP.NET runtime) and `web/Dockerfile` (unprivileged nginx with a strict CSP; proxies `/api` to the API). Configuration, migrations and monitoring are covered in [docs/operations.md](docs/operations.md).

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
