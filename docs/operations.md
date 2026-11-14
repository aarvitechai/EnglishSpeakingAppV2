# Operations runbook

## Environments and branches

| Branch | Environment | Promoted by |
|---|---|---|
| `dev` | Development / CI | Every commit |
| `staging` | Pre-production | `scripts/promote.sh "<label>"` (merges `dev → staging → production` with `--no-ff`) |
| `production` | Live | Same script, after staging checks pass |

## Configuration (environment variables)

| Variable | Required | Example / default |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | yes | `Production` |
| `ConnectionStrings__SpeakMateDb` | yes | `Server=sql.internal;Database=EnglishSpeakingAppV2DB;User Id=speakmate_app;Password=…;Encrypt=True` |
| `Jwt__SigningKey` | yes | 48+ random characters |
| `Gemini__ApiKey` | yes (when `Ai__Provider=Gemini`) | paid-tier key |
| `Gemini__Model` | no | `gemini-3.5-flash-lite` (check Google's model list; models get deprecated) |
| `Cors__AllowedOrigins__0` | only if the web app is on another origin | `https://app.example.com` |
| `ReverseProxy__KnownProxies__0` | yes behind a proxy | IP address of nginx or the load balancer |
| `Usage__DailyTokenLimitPerUser` | no | `150000` |
| `RateLimiting__GlobalPermitsPerMinute` | no | `300` |
| `Retention__Enabled` | no | `true` |

The API **refuses to start** in Production with unsafe CORS, a missing connection string, a database connection that skips certificate validation, a JWT key that is too short, or Gemini selected without a key.

## Deploying

```bash
# 1. Database: generate a reviewed, idempotent script and apply it with a privileged identity
cd backend
dotnet tool run dotnet-ef migrations script --idempotent \
  --project src/SpeakMate.Infrastructure --startup-project src/SpeakMate.Api -o migrate.sql
sqlcmd -S <server> -d EnglishSpeakingAppV2DB -I -i migrate.sql

# 2. Images (from the repository root)
docker build -f backend/Dockerfile -t speakmate-api:<version> .
docker build -t speakmate-web:<version> web

# 3. Roll out the API (readiness: GET /health/ready), then the web container
```

- Migrations are **additive first**: add columns or tables, deploy code, then remove old columns in a later release. This lets old and new API versions run side by side during a rolling deploy.
- `sqlcmd` needs `-I` (QUOTED_IDENTIFIER ON) because the schema uses filtered indexes.

## Health and monitoring

| Probe | Meaning | Use for |
|---|---|---|
| `GET /health/live` | Process is up (no dependency checks) | Liveness: restart if failing |
| `GET /health/ready` | Database reachable | Readiness: remove from load balancer if failing |

Watch these signals:

- **5xx rate** and `Unhandled exception` log entries (each response includes a `traceId` matching the logs).
- **AI failures:** `AI coach failed` and `Gemini returned HTTP …` warnings. Sustained 401/403 means the key is invalid or revoked; 404 means the model was retired (update `Gemini__Model`); 429 means the quota is too low.
- **Cost:** daily totals from `app.UsageEvents`:

  ```sql
  SELECT CAST(CreatedAt AS date) AS Day, Model, SUM(InputTokens) AS InTok, SUM(OutputTokens) AS OutTok,
         SUM(EstimatedCostUsd) AS Usd, SUM(CASE WHEN Succeeded = 0 THEN 1 ELSE 0 END) AS Failures
  FROM app.UsageEvents GROUP BY CAST(CreatedAt AS date), Model ORDER BY Day DESC;
  ```

- **Security:** `Refresh token reuse detected` warnings (possible token theft) and spikes in `Failed login`.

## Incidents

| Symptom | First checks |
|---|---|
| Learners see "coach is having trouble responding" | Gemini status page; API logs for the HTTP status; key validity; model name still listed |
| Everyone is signed out | Was `Jwt__SigningKey` rotated? (expected) Clock skew between instances? |
| 429s for many users | One proxy IP shared by everyone: is `ReverseProxy__KnownProxies` set? |
| Readiness failing | SQL Server reachability, credentials, failover in progress |

## Backups

Use SQL Server automated backups with point-in-time restore. Deleted accounts must also disappear from backups within your stated retention window. Document that window in the privacy policy.
