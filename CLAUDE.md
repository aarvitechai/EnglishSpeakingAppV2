# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

**SpeakMate AI** (working name, placeholder): an AI-powered English speaking-practice platform for adult learners worldwide. Responsive web app + Android/iOS apps, with Google Gemini as the AI provider, called only from a secure backend.

The core value is a **learning loop**, not a chatbot: set a goal → practise speaking → see transcript and corrections → repeat better versions → track progress → return for a next activity.

The full product plan is in `AI_English_Practice_Web_Mobile_App_Plan.md`. It is the source of truth. The `.docx` is a condensed formatted copy of the same plan. Read the relevant plan section before making product or architecture decisions.

## Current status

In active development, built phase by phase. Each phase is committed on `dev`, then merged `dev → staging → production` with `--no-ff`.

## Stack (decided, replaces the plan's Node.js/PostgreSQL suggestion)

| Layer | Choice |
|---|---|
| Backend | ASP.NET Core 10 minimal APIs, clean architecture |
| Database | SQL Server `DESKTOP-2ERCDGB\MSSQLSERVER01`, database `EnglishSpeakingAppV2DB`, **Windows authentication only** (mixed mode is off) |
| ORM | EF Core 10 code-first migrations |
| AI | Gemini REST API via a typed `HttpClient`, server-side only, model ID in configuration |
| Web | React 19 + TypeScript (strict) + Vite, TanStack Query, React Router, Zod |
| Tests | xUnit v3 on Microsoft.Testing.Platform, NSubstitute; Vitest + Testing Library + MSW |

Package versions: NuGet versions are pinned centrally in `backend/Directory.Packages.props`. Use 10.0.x Microsoft packages; 11.x needs .NET 11, which isn't installed.

## Layout and dependency rule

```text
backend/src/SpeakMate.Domain          entities and rules: no EF, no ASP.NET
backend/src/SpeakMate.Application     use cases, ports (interfaces), DTOs, validators
backend/src/SpeakMate.Infrastructure  EF Core DbContext, migrations, Gemini client, identity
backend/src/SpeakMate.Api             endpoints, auth, middleware, DI composition root
backend/tests/SpeakMate.UnitTests     domain, services, architecture rules
backend/tests/SpeakMate.IntegrationTests  WebApplicationFactory + real SQL Server test DB
web/                                  React app (calls only our API via /api, proxied in dev)
```

Dependencies point inward only. `LayerDependencyTests` fails the build if a layer references something it must not.

## Commands

```bash
cd backend && dotnet build                 # warnings are errors (Directory.Build.props)
cd backend && dotnet test                  # all backend tests (MTP runner, set in global.json)
cd backend && dotnet test --project tests/SpeakMate.UnitTests
cd backend && dotnet run --project src/SpeakMate.Api --launch-profile https   # https://localhost:7080, /scalar in dev
cd web && npm run dev                      # http://localhost:5173
cd web && npm test | npm run lint | npm run typecheck | npm run format:check | npm run build
```

Gemini key, set locally (never commit it): `dotnet user-secrets set "Gemini:ApiKey" "<key>" --project backend/src/SpeakMate.Api`

## Non-negotiable rules

### Secrets
- The Gemini API key exists **only on the server** (server-side env secrets).
- Never put it in React or React Native source, browser or mobile bundles, source maps, public-prefixed env vars (`VITE_*`, `EXPO_PUBLIC_*`), Git commits, logs or client error messages.
- Clients call our backend. The backend calls Gemini. Keep `.env*` files out of Git.

### AI handling
- Treat prompts and model output as untrusted. Validate structured responses on the server against a schema and reject malformed output.
- Model text must never trigger privileged application actions.
- Design for prompt-injection resistance: user speech or text must not be able to reveal secrets or system prompts.
- **Don't hardcode model names from memory or tutorials.** Check current Gemini docs (https://ai.google.dev/gemini-api/docs) and pricing before choosing a model, and keep the model ID in configuration.
- Apply per-user rate limits and quotas. Log usage (`usage_events`: model, tokens/audio, estimated cost) so cost per session can be measured.

### Privacy
- Treat voice, transcripts and anything users say aloud as sensitive.
- **Don't store raw audio in the MVP.** Store transcripts only with consent.
- Don't log raw conversation content by default.
- Every user can access only their own data. Enforce this with server-side authorization.
- Microphone, listening, thinking and speaking states must always be visible in the UI. The learner must never have to guess whether recording or transmission is active.
- Provide account deletion and consent withdrawal flows (`consent_records`).
- Avoid sending unnecessary personal data to the model.

### Product claims
- Never claim official CEFR certification, guaranteed scores, objective accent scores, or medical or therapeutic benefit.
- Only show pronunciation or fluency feedback when the pipeline can measure it reliably. When transcription is uncertain, ask for clarification instead of correcting.

## AI coach behaviour

- Patient and encouraging. Adapt to the learner's level (beginner, intermediate, advanced) and goal.
- Ask **one question at a time** and let the learner finish. Don't interrupt for minor mistakes.
- At intervals or at session end, give **no more than 3** high-priority corrections, with natural alternatives. Explain briefly in the learner's support language when they ask. Invite them to repeat or rephrase.
- Never shame the learner or request sensitive personal information.
- Keep feedback categories separate: grammar and structure, vocabulary and phrasing, fluency (only when measurable), pronunciation (only when validated).

A reference system instruction is in section 5 of the plan. Prompt templates live server-side.

## MVP scope

**In scope:** email auth; onboarding (level, goal, support language, daily target); voice practice with a **text fallback**; modes (daily conversation, role-play, interview, topic challenge, guided beginner, text); transcript; session feedback (summary, top 3 corrections, useful phrases, a repeat task); history; basic progress; settings and privacy controls.

**Out of scope for the first release (don't build unless asked):** social features or chat rooms, human tutor marketplace, leaderboards, exams or certification, many personas or languages, real-time voice before costs are tested, user-generated content, user-to-user messaging.

## Core data model (start small)

`users`, `learner_profiles`, `practice_sessions`, `conversation_turns`, `session_feedback`, `learning_goals`, `usage_events`, `consent_records`. Use migrations and minimise personal data. Details are in section 7 of the plan.

## Voice approach

Order: text conversation first → turn-based voice (speech-to-text → Gemini → text-to-speech, Path A) → then evaluate real-time audio (Path B). Real-time audio needs short-lived client credentials issued by the backend. Handle permissions, silence, network loss, backgrounding, stop and retry, and fall back to text.

## Build order (first 14 sessions)

1. One-page product requirements and MVP exclusions
2. Wireframes: onboarding, home, practice, feedback
3. Monorepo and TypeScript setup (lint, format, env files)
4. Web and mobile navigation
5. Backend health endpoint and env configuration
6. Authentication and protected routes
7. Database schema and migrations
8. Onboarding and profile persistence
9. Backend Gemini text endpoint (server-only key)
10. Text chat UI and conversation history
11. Practice modes and level-aware prompt templates
12. Structured feedback and session summary
13. Usage limits, error handling, logs
14. Test with 3–5 learners and fix blockers **before** starting voice

The broader roadmap (Phases 0–9, from discovery to public launch, each with exit checks) is in section 9 of the plan.

## Testing expectations

- **Functional:** auth, onboarding, session start/pause/end, text fallback, transcript and history, deletion and consent withdrawal.
- **AI quality:** beginner prompts stay simple, one question per turn, at most 3 corrections, no invented certificates, prompt injection fails, schema validation rejects bad output.
- **Security:** no API key in client bundles or source maps, no cross-user data access, rate limits work, logs contain no secrets or raw voice.
- **Devices and networks:** Android and iOS permissions, desktop browser microphone, slow or interrupted networks, app backgrounding.

## Conventions

- Use TypeScript everywhere.
- Make numeric success targets only after a pilot baseline. Don't invent metrics or success claims.
- Verify current official docs (Gemini, React, React Native, Expo, Node, PostgreSQL) before writing integration code. APIs, models, quotas and pricing change.
- Open decisions (final name, launch markets, adult-only, voice architecture, pricing, hosting region) are listed in section 16 of the plan. Use sensible prototype defaults and flag these to the user instead of deciding them silently.
