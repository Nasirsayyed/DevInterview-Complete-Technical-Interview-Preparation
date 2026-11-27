# TASKS.md — DevInterview implementation tracker

Status legend: `[ ]` not started · `[~]` in progress / partially verified · `[x]` done (built + tested)

## Phase 1 — Foundation
- [x] Environment inspection (.NET 8, Android SDK 36, JDK 21, Node 22, Docker, SQL Server 2022 container)
- [x] Repository structure, CLAUDE.md, ARCHITECTURE.md, TASKS.md
- [x] Backend solution (API / Application / Domain / Infrastructure / Contracts + unit and integration tests)
- [x] EF Core + SQL Server, DI, Serilog, Swagger, versioning, `ApiResponse<T>` error envelope, rate limiting
- [x] Android multi-module project (build-logic conventions), design system, type-safe navigation shell
- [x] Environment configuration (appsettings + env vars/user-secrets, Android Gradle properties, admin `.env`)

## Phase 2 — Authentication
- [x] Register, email verification codes, login, lockout, refresh rotation with reuse detection, logout (all devices)
- [x] Google sign-in (ID token validation) — Android uses Credential Manager when a web client id is configured
- [x] Forgot/reset/change password, profile, photo upload, preferences, data export, account deletion
- [x] Android auth screens (splash, onboarding, login, register, verify, forgot password) + ViewModel/UI tests

## Phase 3 — Question bank
- [x] Technologies, categories, topics, questions (7 types), answers, explanations, tags
- [x] Filtering, search, paging (Paging 3 on Android), previous/next, random question
- [x] Bookmarks and notes (offline-first, LWW sync), collections, reports, note export
- [x] Seed content: 22 technologies, 236 original questions

## Phase 4 — Quiz engine
- [x] Daily, curated and custom quizzes; server deadline with clock-skew compensation; auto-submit; resume
- [x] Negative marking, pass mark, topic breakdown, review, XP; idempotent submit
- [x] Android quiz home/config/player/results + ViewModel tests

## Phase 5 — Coding challenges
- [x] Challenges, test cases (sample + hidden), starter code, hints, solutions, 23 seeded challenges with reference solutions
- [x] Docker sandbox provider (verified: Accepted / WA / CE / TLE / memory / no-network), Judge0 and Disabled providers
- [x] Run/Submit with idempotency keys and quotas; submissions history
- [x] Android challenge list/detail/editor (drafts, custom input, console) + tests
- [~] Kotlin sandbox image (`sandbox/images/kotlin`) — Dockerfile written; not built here (Docker Hub returns 429 rate limits in this environment)

## Phase 6 — AI features
- [x] Provider abstraction (Anthropic SDK, OpenAI-compatible), orchestrator with retries/failover/JSON repair/usage logging
- [x] Mock interviews (server-driven follow-ups, adaptive difficulty, report), answer evaluator, code reviewer, admin question drafts
- [x] Prompt-injection guarding, output validation and clamping, Premium entitlement + daily quotas
- [x] Android interview setup/chat (voice input + TTS with graceful fallback)/report, evaluator, reviewer
- [~] Live calls to AI providers not exercised here (no API key in this environment); covered by unit tests with fake providers

## Phase 7 — Learning paths & flashcards
- [x] Paths → modules → lessons (reading/exercise/quiz), progress, offline download; 10 seeded paths
- [x] Flashcards with SM-2 scheduling, offline review queue; 85 seeded cards
- [x] Company preparation with content-source labelling (10 illustrative profiles)

## Phase 8 — Analytics & gamification
- [x] Dashboard, analytics (overview, per technology, weekly/monthly), preparation score
- [x] XP, levels, streaks, achievements, leaderboard, weekly goals
- [x] Notifications (in-app + FCM), preferences, reminder jobs (Hangfire), interview reminders
- [x] Subscriptions: plans, Google Play verification (ownership-bound), RTDN, entitlements, admin grants

## Phase 9 — Admin panel
- [x] React + TypeScript + MUI app with RBAC (SuperAdmin / Admin / ContentManager)
- [x] Dashboard, taxonomy, questions + editor, import/export, reports, quizzes, challenges, learning paths, flashcards, companies
- [x] Users/roles, subscriptions/plans, AI prompts/usage/providers, broadcast, audit logs, settings
- [x] Vitest suite; verified against the live API in a browser (all pages, no API or console errors)

## Phase 10 — Finalization
- [x] Docs: Architecture, DatabaseDesign, API_Documentation (generated endpoint inventory), SetupGuide, DeploymentGuide, UserGuide
- [x] Scripts: DatabaseSetup (local SQL Server, migrations, idempotent script), SeedData generators + packs, smoke test
- [x] GitHub Actions CI (backend with SQL Server service, Android, admin)
- [x] Android ↔ API contract checks: all 98 client routes exist, 34 request bodies match field-for-field, 50 captured live responses decode (`ApiContractTest`)
- [x] Android release build with R8 minification (unsigned APK) builds
- [~] Container images: API build stage verified; API runtime layer (deb.debian.org blocked by network policy) and admin image (Docker Hub 429) not built here
- [~] Android instrumented/device testing — not run (no emulator here); JVM + Robolectric tests pass

## Requires external accounts / configuration before production
- Google Cloud OAuth web client id (Google sign-in) and `Authentication:Google:ClientIds`
- Firebase project (`google-services.json`, FCM service account)
- Play Console app, subscription `devinterview_premium` (base plans `monthly`, `yearly`), billing service account, RTDN
- AI provider API key(s); SMTP credentials; production JWT signing key
