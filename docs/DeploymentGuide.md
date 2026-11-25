# Deployment guide

## 1. Topology

```
            HTTPS (TLS terminated at a reverse proxy / load balancer)
Android ─┐
Admin  ──┼──► API (ASP.NET Core, 1..n instances) ──► SQL Server 2022 (managed: Azure SQL / RDS / self-hosted)
         │        │  Hangfire (SQL Server storage; workers in every API instance)
         │        ├──► AI providers (Anthropic / OpenAI-compatible)
         │        ├──► Code execution: dedicated Docker host or Judge0
         │        ├──► FCM HTTP v1, Google Play Developer API, SMTP
         │        └──► Upload storage (volume / shared disk for profile photos)
```

## 2. Required configuration (environment variables)

| Variable | Notes |
|----------|-------|
| `ASPNETCORE_ENVIRONMENT=Production` | Enables production guards (no dev JWT key, real email required) |
| `ConnectionStrings__Default` | SQL Server connection string (use a least-privilege login after the first migration) |
| `Jwt__SigningKey` | ≥ 32 random bytes; rotate by deploying a new key (all sessions must sign in again) |
| `Cors__AllowedOrigins__0` | Admin panel origin, e.g. `https://admin.example.com` |
| `App__PublicBaseUrl` | Public API URL (used for links and uploaded file URLs) |
| `Email__Provider=Smtp`, `Email__Host`, `Email__Port`, `Email__Username`, `Email__Password`, `Email__FromAddress` | Verification and password-reset codes |
| `Seed__AdminEmail`, `Seed__AdminPassword` | First deployment only; remove afterwards |
| `AI__Providers__Anthropic__ApiKey` / `AI__Providers__OpenAI__ApiKey` (+ `__Model`, `__BaseUrl`) | Optional; AI features report unavailable without them |
| `AI__ProviderOrder__0`, `__1` | Failover order |
| `CodeExecution__Provider` | `Docker`, `Judge0` or `Disabled` |
| `CodeExecution__Judge0__BaseUrl`, `CodeExecution__Judge0__AuthToken` | When using Judge0 |
| `Authentication__Google__ClientIds__0` | OAuth client id(s) whose ID tokens are accepted (the Android app's web client id) |
| `Firebase__ProjectId`, `Firebase__CredentialsPath` | FCM service-account JSON mounted as a secret file |
| `Billing__GooglePlay__PackageName`, `Billing__GooglePlay__ServiceAccountPath`, `Billing__GooglePlay__NotificationSecret` | Purchase verification and RTDN |
| `Swagger__Enabled=false` | Recommended in production |
| `BackgroundJobs__Enabled=true` | Hangfire workers; the read-only dashboard at `/jobs` is restricted to Super Admins |

Store secrets in the platform's secret manager (Azure Key Vault, AWS Secrets Manager, Kubernetes secrets) and inject
them as environment variables or files. Nothing secret is read from `appsettings.json`.

## 3. Database

1. Create the database and a login for the API.
2. Either let the API migrate on start (`Database__ApplyMigrationsOnStartup=true`, default) or set it to `false`
   and apply `scripts/DatabaseSetup/generate-migration-script.sh` output in your release pipeline (recommended for
   multi-instance deployments).
3. Back up with point-in-time restore enabled; test restores.

## 4. API

```bash
docker build -f backend/Dockerfile -t registry.example.com/devinterview/api:<version> .
docker push registry.example.com/devinterview/api:<version>
```

Run behind a reverse proxy that terminates TLS and forwards `X-Forwarded-*`. Health endpoint: `GET /health`.
Logs are structured (Serilog) to stdout — ship them to your log platform. Persist `/app/uploads` on a volume
shared by all instances (or switch `IFileStorage` to object storage).

## 5. Code execution in production

User code is untrusted. Prefer one of:

- **Dedicated execution host**: run the API (or a separate API instance that only serves `/code/*`) on a VM used
  for nothing else, with the Docker daemon socket mounted, `CodeExecution__WorkRoot` on a local disk with the same
  path in and out of the container, and the images from `sandbox/build-images.sh` pinned by digest.
- **Judge0**: managed or self-hosted on isolated infrastructure.

Never mount the Docker socket on a host that also runs the database or other services.

## 6. Admin panel

```bash
docker build --build-arg VITE_API_BASE_URL=https://api.example.com -t registry.example.com/devinterview/admin:<version> admin
```

Serve it on its own origin over HTTPS and add that origin to `Cors__AllowedOrigins`. The nginx config sets
`X-Frame-Options: DENY`, `nosniff` and `noindex`.

## 7. Android release

1. Create the upload keystore (keep it out of the repository) and set in CI:
   `DEVINTERVIEW_KEYSTORE_PATH`, `DEVINTERVIEW_KEYSTORE_PASSWORD`, `DEVINTERVIEW_KEY_ALIAS`, `DEVINTERVIEW_KEY_PASSWORD`.
2. Provide `android/app/google-services.json` from the Firebase console as a CI secret file.
3. Build: `./gradlew bundleRelease -Pdevinterview.apiBaseUrl=https://api.example.com/ -Pdevinterview.googleWebClientId=<id> -Pdevinterview.versionCode=<n> -Pdevinterview.versionName=<x.y.z>`
   (R8 minification and resource shrinking are on).
4. Play Console: create the subscription product `devinterview_premium` with base plans `monthly` and `yearly`
   (matching the seeded plans), set up Real-Time Developer Notifications to
   `https://api.example.com/api/v1/subscriptions/google-play/rtdn?token=<NotificationSecret>`, and grant the
   billing service account access to the app in the Play Console.
5. Upload the AAB to an internal testing track first and run the purchase flow with license testers.

## 8. CI

`.github/workflows/ci.yml` builds and tests the backend (with a SQL Server service for migration tests and a check
for pending EF model changes), the Android app (assemble + unit/Robolectric tests) and the admin panel
(typecheck, Vitest, production build).

## 9. Operations checklist

- [ ] Secrets in a secret manager; `.env` / service-account files never committed
- [ ] `Swagger__Enabled=false`, HTTPS only, HSTS on
- [ ] Database backups and restore drill
- [ ] Alerting on 5xx rate, AI failure rate (admin → AI usage), Hangfire failed jobs (`/jobs`)
- [ ] Execution host isolated; sandbox images pinned by digest
- [ ] Bootstrap admin password rotated and `Seed__Admin*` removed after first start
