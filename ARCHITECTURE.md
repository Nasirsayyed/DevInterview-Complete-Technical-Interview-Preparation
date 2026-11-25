# ARCHITECTURE.md

DevInterview is three clients of one API. The detailed write-up is in [`docs/Architecture.md`](docs/Architecture.md);
this page is the map to keep in your head while changing code.

```
 Android app (Kotlin/Compose)        Admin panel (React/MUI)
   │  REST /api/v1, JWT + refresh       │  REST /api/v1/admin, JWT (role-checked)
   └──────────────┬─────────────────────┘
                  ▼
        ASP.NET Core 8 API  ── Hangfire jobs (reminders, expiry, cleanup)
   Controllers → Application services → Domain rules
                  │            │              │
         EF Core 8 (SQL Server)  IAiProvider      ICodeExecutionProvider
                                 (Anthropic,      (Docker sandbox / Judge0 /
                                  OpenAI-compat)   Disabled) — never in-process
```

## Ownership rules

| Concern | Lives in | Never in |
|---------|----------|----------|
| Grading, XP, levels, streaks, SM-2 scheduling, quotas, entitlements | `backend/src/DevInterview.Domain/Rules` + Application services | Android, admin |
| AI prompts, provider keys, output validation | Backend (`Application/Features/Ai`, `Infrastructure/Ai`) | Clients |
| User code execution | Sandbox containers / Judge0 | API process |
| Purchase verification | Backend via Google Play Developer API | Android |
| Offline cache & pending changes | Android Room (`core:database`) | — |

## Backend layers (`backend/src`)

- **Domain** — entities, enums, pure rules (no EF, no I/O). Unit-tested directly.
- **Contracts** — request/response DTOs and `ApiResponse<T>`; the only types controllers return.
- **Application** — use-case services, FluentValidation validators, abstractions (`IAppDbContext`, `IAiProvider`,
  `ICodeExecutionProvider`, `IClock`, `IPurchaseVerifier`, …).
- **Infrastructure** — EF Core (`AppDbContext`, configurations, migrations, seeder), Identity/JWT, AI providers,
  sandbox providers, email, FCM, storage, Google Play billing, Hangfire jobs.
- **API** — controllers, middleware (error envelope, security headers), auth policies, rate limiting, Swagger.

## Android modules (`android/`)

`app` → `feature/*` → `core/{designsystem,navigation,common}` + `domain` ; `data` → `core/{network,database,datastore}` + `domain`.
Features depend on **domain interfaces** only (plus `data` for Play Billing in `feature:profile`); Hilt binds the
implementations from `data`. Navigation uses type-safe `@Serializable` routes in `core:navigation`.

## Admin (`admin/`)

Vite + React + TypeScript + MUI + TanStack Query. `src/api` (client, DTO types), `src/auth` (session + RBAC),
`src/components` (tables, declarative forms, pickers), `src/pages` (one module per admin area).

## Change checklist

1. Schema change → entity + `IEntityTypeConfiguration` + new migration (`dotnet ef migrations add`).
2. New endpoint → DTO in Contracts, validator, service method, controller returning `ApiResponse<T>`, test.
3. New screen → route in `core:navigation`, ViewModel with `StateFlow<UiState>`, repository method, test.
4. Update `TASKS.md` only after build + tests pass.
