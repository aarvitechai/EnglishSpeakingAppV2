# Architecture

## Overview

```text
Browser (React SPA)
   │  same origin: /api/*, /health/*   (Vite proxy in dev, nginx in production)
   ▼
ASP.NET Core 10 API ───────► Google Gemini (generateContent, server-side key)
   │
   ▼
SQL Server  (app schema: learner data · auth schema: accounts and refresh tokens)
```

The browser only ever talks to our own origin. The API is the only component holding secrets (Gemini key, JWT signing key, database credentials).

## Backend layers (clean architecture)

| Project | Responsibility | May depend on |
|---|---|---|
| `SpeakMate.Domain` | Entities and invariants: `PracticeSession` (aggregate with turns and feedback), `LearnerProfile`, `UsageEvent`, `ConsentRecord`. No frameworks. | nothing |
| `SpeakMate.Application` | Use cases (`PracticeService`, `SessionFeedbackService`, `ProgressService`, `ProfileService`, `ConsentService`), ports (`IAiCoach`, repositories, `IUnitOfWork`), prompt builders, validators, DTOs. | Domain |
| `SpeakMate.Infrastructure` | EF Core `SpeakMateDbContext` + migrations, repositories, ASP.NET Core Identity, JWT issuing, Gemini adapter, fake coach, retention worker. | Application |
| `SpeakMate.Api` | Minimal-API endpoints, auth, rate limiting, error mapping, hardening, composition root. | Infrastructure |

`tests/SpeakMate.UnitTests/Architecture/LayerDependencyTests.cs` fails the build if a layer references something it shouldn't.

## Key design decisions

| Decision | Why |
|---|---|
| **Result/Error for expected failures, exceptions for bugs** | Endpoints map `ErrorType` → HTTP status in one place (`ResultHttpExtensions`). Domain invariant violations (`DomainException`) become 400s through `ApiExceptionHandler`. |
| **Aggregate root owns conversation rules** | Speakers alternate, coach opens, 30-message cap, ended sessions are read-only, at most 3 corrections. The rules hold no matter which use case or background job touches the data. |
| **Optimistic concurrency** (`rowversion` + unique `(SessionId, Sequence)`) | Two tabs replying to one session can't corrupt it; the loser gets 409. |
| **AI behind a port (`IAiCoach`)** | Swap providers or models without touching use cases. Tests use `FakeAiCoach`; local dev without a key uses it too (`Ai:Provider=Auto`). |
| **AI output is untrusted** | JSON-schema structured output, strict parsing, and `FeedbackSanitizer` enforces domain limits before anything is stored. |
| **No half-saved turns on AI failure** | The learner turn is added in memory and only saved together with the coach reply. Failed-call token usage is saved through an isolated scope (`IUsageRecorder`). |
| **Privacy by default** | Transcripts are cleared after feedback unless the learner consents; the retention worker enforces a 24-hour maximum. All learner tables cascade from `auth.Users`. |
| **Stateless API** | No server session state: JWT access tokens plus DB-backed refresh tokens. Any number of API instances can run behind a load balancer. |

## Request flow: sending a message

1. `POST /api/v1/sessions/{id}/messages` → per-user AI rate limit → `ValidationFilter<SendMessageRequest>`.
2. `PracticeService.SendMessageAsync` loads the session **scoped to the caller's user ID** (other users' sessions return 404).
3. Quota check (rolling 24-hour tokens) → `session.AddLearnerTurn(...)` (domain rules) → `IAiCoach.GenerateReplyAsync`.
4. Success: add the coach turn, record a `UsageEvent` with estimated cost, then commit once (concurrency-checked).
5. Failure: return 503 `ai.unavailable`; nothing is saved except failed-call usage.

## Scaling notes

- **API:** stateless; scale horizontally. Rate limits are per instance (in-memory). For strict global limits across many instances, move to a distributed limiter (e.g. Redis) or enforce at the gateway.
- **Retention worker:** safe to run on every instance (optimistic concurrency and batch sizes). At larger scale, run it as a single scheduled job instead.
- **Database:** indexes cover every hot query (`UserId+StartedAt DESC` for history, a filtered active-session index, `UserId+CreatedAt INCLUDE tokens` for quotas). `EnableRetryOnFailure` handles transient faults and failover.
- **AI:** bounded context (last 20 turns, 12k-character feedback transcript), per-attempt timeouts, retries and a circuit breaker.

## Web app structure

```text
web/src/
  app/         router, providers, query client
  features/    auth, profile (onboarding), home, practice (+ voice hooks), history, settings
  shared/api/  httpClient (token in memory, single-flight refresh), Zod schemas, endpoint functions
  shared/ui/   layout, form controls, labels, alerts
  test/        MSW server, fixtures, renderRoute helper
```
