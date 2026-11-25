# CLAUDE.md — DevInterview project rules

DevInterview is a technical-interview preparation product: an Android app (Kotlin/Compose),
an ASP.NET Core 8 API (SQL Server, EF Core 8), and a React/TypeScript admin panel.
Read `ARCHITECTURE.md` before changing structure and `TASKS.md` for current status.

## Repository layout

| Path | What lives there |
|------|------------------|
| `backend/` | `DevInterview.sln` — API, Application, Domain, Infrastructure, Contracts + tests |
| `android/` | Gradle multi-module app (`app`, `core/*`, `domain`, `data`, `feature/*`, `build-logic`) |
| `admin/` | Vite + React + TypeScript + MUI admin web app |
| `sandbox/` | Docker images + runner scripts for isolated code execution |
| `scripts/` | Database setup, seed data, developer utilities |
| `docs/` | Architecture, database, API, setup, deployment and user guides |

## Commands

```bash
# Backend
cd backend && dotnet build && dotnet test
dotnet ef migrations add <Name> -p src/DevInterview.Infrastructure -s src/DevInterview.API -o Persistence/Migrations
# Android (needs ANDROID_HOME)
cd android && ./gradlew assembleDebug testDebugUnitTest
# Admin
cd admin && npm ci && npm run build && npm test
```

## Non-negotiable rules

1. **Business logic lives in the backend.** Scoring, XP, streaks, spaced repetition, entitlements,
   quotas and AI orchestration are computed server-side. Android renders and caches; it never decides
   whether an answer is correct or a user is premium.
2. **No secrets in Git.** API keys, JWT signing keys, SMTP credentials, Google service accounts come
   from environment variables / user-secrets. `appsettings.json` holds only safe defaults.
3. **AI calls only from the backend** through `IAIProvider`. AI output is untrusted: parse, validate
   and clamp before storing. Never execute AI output.
4. **User code never runs in the API process.** Use `ICodeExecutionProvider` (Docker sandbox or Judge0).
5. **Never expose EF entities from controllers.** Use DTOs from `DevInterview.Contracts`.
6. **Every endpoint returns `ApiResponse<T>`**; errors are thrown as `AppException` subclasses and mapped
   by `ExceptionHandlingMiddleware`.
7. **UTC everywhere in the backend** (`DateTime` with `Kind=Utc`, via `IClock`). Convert to the user's time
   zone only for streak/day boundaries.
8. **Entity configuration** goes in `IEntityTypeConfiguration<T>` classes under
   `Infrastructure/Persistence/Configurations`. Add a migration for every schema change; never edit an
   applied migration.
9. **No placeholder UI.** Every button does something real or is not rendered. No hardcoded stats.
10. **Content must be original and technically correct.** Company content must carry a `ContentSource`
    (Verified / CommunityContributed / Illustrative) and never claims unverified real interview questions.
11. Log with Serilog structured templates; never log passwords, tokens, API keys or full user code/answers.
12. Update `TASKS.md` when a module is finished, and only mark it done when it builds and its tests pass.

## Conventions

- C#: file-scoped namespaces, nullable enabled, `async` all the way, `CancellationToken` on every async API.
- Kotlin: MVVM + UDF. ViewModels expose `StateFlow<UiState>`; one-off events via `Channel`.
  Repositories return `Flow`/`AppResult`. Dispatchers are injected.
- API routes: `/api/v1/<feature>`; JSON camelCase; enums serialized as strings.
- Tests: xUnit (backend), JUnit4 + Turbine + Robolectric (Android), Vitest (admin).
