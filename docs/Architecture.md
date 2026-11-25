# DevInterview — Architecture

## 1. System overview

| Component | Technology | Responsibility |
|-----------|------------|----------------|
| Android app | Kotlin 2.2, Jetpack Compose (Material 3), MVVM + UDF, Hilt, Retrofit/OkHttp, Room, DataStore, WorkManager, Paging 3, Coil, FCM, Play Billing, Credential Manager | Learner experience, offline cache, background sync |
| API | ASP.NET Core 8, EF Core 8 (SQL Server), ASP.NET Identity, JWT, FluentValidation, Serilog, Swagger, Hangfire, rate limiting, API versioning | All business rules, persistence, AI orchestration, entitlements |
| Admin panel | React 18, TypeScript, MUI 6, TanStack Query, Vite | Content management and operations |
| Code execution | Docker sandbox (default) or Judge0 | Compile and run untrusted code with hard limits |
| AI | Anthropic (official .NET SDK) and any OpenAI-compatible endpoint | Mock interviews, answer evaluation, code review, admin question drafts |

All clients talk to `/api/v1/...` over HTTPS with JSON (camelCase, enums as strings). Every response is an
`ApiResponse<T>` envelope: `{ success, data, error: { code, message, details }, traceId }`.

## 2. Backend

### 2.1 Layers

```
API (controllers, middleware, policies)
  └─► Application (services, validators, abstractions)
        └─► Domain (entities, enums, pure rules)
Infrastructure implements Application abstractions (EF Core, Identity, AI, sandbox, email, FCM, billing, jobs)
Contracts holds DTOs shared by API and Application
```

- Controllers are thin: validate (FluentValidation filter), call a service, wrap the result. They never return EF entities.
- Errors are thrown as `AppException` subclasses (`NotFoundException`, `ValidationAppException`, `ConflictException`,
  `PremiumRequiredException`, `QuotaExceededException`, `ServiceUnavailableException`, …) and mapped by
  `ExceptionHandlingMiddleware` to status codes and stable `error.code` values (`validation_error`, `premium_required`,
  `quota_exceeded`, `ai_unavailable`, `execution_unavailable`, `email_not_verified`, …). Unknown exceptions become
  `internal_error` with the trace id; details are logged, never returned.
- Time comes from `IClock` (UTC). Day boundaries for streaks/goals use the user's IANA time zone.

### 2.2 Domain rules (pure, unit-tested)

| Rule | File | Notes |
|------|------|-------|
| Quiz scoring | `Rules/QuizScoring.cs` | Exact set match for multi-answer, optional negative marking, score clamped ≥ 0 |
| Spaced repetition | `Rules/SpacedRepetition.cs` | SM-2 with grades Again/Hard/Good/Easy |
| XP & levels | `Rules/Gamification.cs` | Level *n* starts at 50·n·(n−1) XP; streak calculator |
| Assessment | `Rules/Assessment.cs` | Answer length caps, adaptive difficulty, output comparison, preparation score (confidence-weighted) |

### 2.3 Concurrency and idempotency

- Usage quotas are incremented with a conditional `ExecuteUpdate` (no read-modify-write races).
- Quiz submission atomically claims the attempt (`InProgress → Submitted/Expired`) so double submits grade once;
  a repeated submit returns the stored result.
- XP awards are unique per `(UserId, SourceType, SourceKey)`; flashcard reviews per `(UserId, ClientReviewId)`;
  code submissions per `(UserId, IdempotencyKey)`.
- Refresh tokens are stored as SHA-256 hashes, rotated on every use; reuse of a rotated token revokes the whole family.

### 2.4 Authentication & authorization

- ASP.NET Identity (PBKDF2 password hashing), lockout after 5 failures for 15 minutes, 6-digit email codes for
  verification and password reset, Google sign-in by validating Google ID tokens against configured client ids.
- Access tokens (JWT, 15 min) + refresh tokens (30 days). Role claims: `SuperAdmin`, `Admin`, `ContentManager`, `User`.
- Policies: `AdminPanel` (content managers and admins), `AdminOnly`, `SuperAdmin`. Only a Super Admin can grant
  admin roles; role changes revoke the user's refresh tokens.
- Rate limiting: global, auth, AI and code-execution buckets (configurable under `RateLimits`).

### 2.5 AI orchestration

`AiOrchestrator` selects providers in `AI:ProviderOrder`, retries with backoff, fails over, and records every call in
`AiUsageLogs` (tokens, latency, estimated cost). Learner text is wrapped with `UntrustedContent.Wrap` (tag-escaped
and preceded by a guard instruction) to resist prompt injection. Model output must be JSON matching a schema; invalid
output gets one repair round-trip, then the call fails with `ai_invalid_response`. Parsed scores are clamped and
lists bounded before storing; nothing returned by a model is executed. Prompts have built-in defaults and versioned
overrides editable in the admin panel. AI features are Premium with daily fair-use quotas.

Mock interviews are server-driven: the server chooses the next question, at most two follow-ups per topic, adapts
difficulty from answer scores, and writes the final report (overall score bounded by the per-answer average).

### 2.6 Code execution

`ICodeExecutionProvider` implementations:

- **DockerSandboxExecutionProvider** — `docker run --rm --network none --read-only --memory/--memory-swap --cpus
  --pids-limit --cap-drop ALL --security-opt no-new-privileges --user 65534:65534`, tmpfs work dirs, source mounted
  read-only, compile once then run every test case with `timeout -s KILL`, output framed and truncated. A hard
  deadline kills the container. See `sandbox/README.md`.
- **Judge0ExecutionProvider** — for managed execution.
- **DisabledExecutionProvider** — returns `execution_unavailable`.

Output comparison normalises line endings and trailing whitespace. Hidden test inputs/outputs never leave the server.

### 2.7 Subscriptions

Plans are data (`SubscriptionPlans`). Android starts Google Play Billing with the user's id as the obfuscated account
id and sends the purchase token to `POST /subscriptions/google-play/verify`. The API verifies it with the Google Play
Developer API (subscriptionsv2), checks product/base plan and ownership, looks tokens up by hash (the token is kept server-side for re-verification), acknowledges
the purchase, and grants access. Real-time developer notifications (`/subscriptions/google-play/rtdn`) and an hourly
job re-verifies renewals and expiry. Entitlements are always computed server-side.

### 2.8 Background jobs (Hangfire)

Quiz attempt expiry, subscription expiry/re-verification, hourly reminders (daily practice, streak at risk, daily
challenge, weekly summary, learning goal, weekly mock-interview nudge — each at the user's preferred local hour),
interview reminders within 24 hours, and data cleanup.

## 3. Android

### 3.1 Modules

```
app ──► feature:{auth,dashboard,questions,quiz,coding,mockinterview,learning,flashcards,analytics,profile,settings}
feature:* ──► core:designsystem, core:navigation, core:common, domain
data ──► core:network, core:database, core:datastore, domain      (bound to domain interfaces with Hilt)
build-logic: convention plugins (application, library, compose, feature, hilt, jvm)
```

### 3.2 Patterns

- ViewModels expose `StateFlow<UiState>` and one-off events through a `Channel`; screens are stateless composables
  taking the ViewModel as a parameter (fakes in tests).
- Repositories return `AppResult<T>` with typed `AppError` (`PremiumRequired`, `QuotaExceeded`, `Validation(fields)`,
  `Unavailable(code)`, `Network`, …) so the UI can show a paywall, inline field errors or a retry.
- Networking: `AuthInterceptor` adds the bearer token; `TokenAuthenticator` refreshes once (mutex) on 401 and emits a
  session-expired event when refresh fails. Tokens are encrypted with an Android Keystore AES-GCM key.
- Quiz timer uses the server deadline and server time to compensate for device clock skew; the server also enforces it.

### 3.3 Offline-first

| Data | Local store | Sync |
|------|-------------|------|
| Opened/bookmarked questions | Room cache | Network first, cache fallback |
| Bookmarks, notes | Room, `pendingSync` flag | Pushed in batch; server applies last-writer-wins by client timestamp |
| Flashcard reviews | Queue (`pending_reviews`) | Append-only, idempotent by `clientReviewId`; cards stay hidden until the server reschedules them |
| Learning paths | Downloaded lessons | Explicit download; network first when online |
| Code drafts | Room per challenge/language | Local only |

`SyncWorker` (WorkManager, network-constrained, exponential backoff) runs on changes, when connectivity returns and
every 6 hours. Rows are marked synced only if unchanged since they were sent. Local data is wiped on sign-out and when a
different account signs in.

## 4. Admin panel

Token handling: access token in memory, refresh token in `sessionStorage` (per tab), single-flight refresh on 401.
Navigation and routes are filtered by role; the API enforces the same policies. Content editors mirror the server's
validation rules (question option rules, Verified sources need references) so problems are shown before saving.

## 5. Security summary

- No secrets in Git: keys, signing keys, SMTP, service accounts come from environment variables / user-secrets.
  The API refuses to start outside Development with the development JWT key or the log-only email sender in Production.
- Security headers, CORS allow-list for the admin origin, HSTS outside Development, request size limits on uploads,
  image magic-byte validation for profile photos, audit log with sensitive fields masked.
- AI security reviews are labelled as non-authoritative and not a substitute for professional audits.
