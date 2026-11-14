# Security and privacy

## Threats and controls

| Threat | Control | Where | Tested by |
|---|---|---|---|
| AI key leaks to clients | Key only in server config; sent in the `x-goog-api-key` header (never the URL); web build calls only `/api` | `GeminiCoach`, Vite proxy and nginx | `GeminiCoachTests.SendsKeyInHeaderNotUrl…` |
| Password guessing | Lockout after 5 failures (15 min); per-IP auth rate limit (10 per minute) with `Retry-After`; generic error messages; dummy hash for unknown emails | `AuthService`, `RateLimitingExtensions` | `AuthEndpointTests`, `AuthRateLimitTests` |
| Stolen access token | 15-minute lifetime; HS256 with a ≥32-byte key; issuer, audience and algorithm checked | `TokenFactory`, JWT bearer options | `ProtectedEndpoint_WithTamperedToken…` |
| Stolen refresh token | HttpOnly, Secure, SameSite=Strict cookie scoped to `/api/v1/auth`; stored as a SHA-256 hash; single-use rotation; **reuse revokes the whole token family** | `AuthEndpoints`, `AuthService` | `Refresh_ReusingSpentToken_RevokesWholeFamily` |
| XSS stealing tokens | Access token kept only in JS memory (not storage); strict CSP with `script-src 'self'`; React escapes output | `httpClient.ts`, `nginx.conf` | `httpClient.test.ts` |
| CSRF | Refresh cookie is SameSite=Strict and path-scoped; all other endpoints require a bearer header | `AuthEndpoints` | — |
| Accessing other users' data (IDOR) | User ID always taken from the token; every query is filtered by owner; foreign IDs return 404 | Repositories, `MeEndpoints` | `OtherUser_CannotReadOrWriteSession`, `EachUser_SeesOnlyTheirOwnProfile` |
| Forgotten authorization on new endpoints | Fallback policy requires authentication unless an endpoint is explicitly `AllowAnonymous` | `ServiceCollectionExtensions` | `UnknownRoute_Anonymous_ReturnsUnauthorized` |
| Prompt injection | Learner text only in the `user` role; system rules tell the model to ignore embedded instructions; model output can't trigger actions; schema-validated output | `CoachPromptBuilder`, `FeedbackPromptBuilder` | `CoachPromptBuilderTests` |
| Malformed or harmful AI output | JSON schema; reject blocked, incomplete, empty or overlong replies; sanitise feedback | `GeminiCoach`, `FeedbackSanitizer` | `GeminiCoachTests`, `FeedbackSanitizerTests` |
| Cost abuse | Per-user AI rate limit; rolling daily token quota; message and session caps; bounded context | `UsageGuard`, `PracticeSession` | `SessionQuotaTests`, `PracticeServiceTests` |
| Denial of service | Global per-IP rate limit; 64 KB request body limit; header size and timeout limits | `HardeningExtensions` | `GlobalRateLimitTests` |
| IP spoofing via `X-Forwarded-For` | Forwarded headers trusted only from configured `ReverseProxy:KnownProxies` | `HardeningExtensions` | — |
| Information leakage | Problem details without stack traces; no `Server` header; `Cache-Control: no-store`; provider error bodies never forwarded | `ApiExceptionHandler`, middleware | `HttpErrors_BecomeProviderExceptionsWithoutLeakingBody`, `ApiResponses_AreNotCached` |
| Unsafe production config | Startup fails on wildcard or http CORS origins, missing connection string, or `TrustServerCertificate=True` | `HardeningExtensions` | `UnsafeProductionSettings_PreventStartup` |
| Accidental data destruction in tests | Integration tests refuse to run unless the database name ends in `_Test` | `TestDatabase` | — |

## Privacy

| Data | Stored? | Retention |
|---|---|---|
| Audio | **Never**. Speech is turned into text by the browser. | — |
| Conversation text | Only during the session, then **cleared after feedback** unless the learner turns on "Keep my transcripts" | Unconsented transcripts last at most 24 hours (retention worker) |
| Feedback, session metadata, practice minutes | Yes | Until account deletion |
| AI usage (tokens, cost, model) | Yes, **without any text** | Until account deletion |
| Consent decisions | Append-only history with policy version | Until account deletion |
| Logs | Method, path, status and timing only. No bodies, prompts or replies. | Per your log platform |

- **Account deletion** (`DELETE /api/v1/me`, password required) cascades through every table. This is verified by `Delete_WithPassword_RemovesEverything_AndBlocksLogin`.
- **Data export** (`GET /api/v1/me/export`) returns everything stored as JSON.
- **AI provider:** Google's free Gemini tier may use submitted content to improve its products. Use a **paid key** for real learners, and review Google's current terms before launch.

## Secrets checklist

- [ ] `Jwt__SigningKey`: random, at least 32 bytes, unique per environment, rotated if exposed (rotation signs everyone out).
- [ ] `Gemini__ApiKey`: paid tier, restricted to the Generative Language API, rotated if exposed.
- [ ] `ConnectionStrings__SpeakMateDb`: least-privilege login (data reader/writer; migrations run with a separate identity).
- [ ] Nothing secret in `appsettings*.json`, Git, logs, screenshots or the web bundle.
