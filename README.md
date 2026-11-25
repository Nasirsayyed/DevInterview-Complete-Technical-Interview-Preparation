# DevInterview

**Prepare Smart. Practice Hard. Get Hired.**

An AI-powered technical-interview preparation platform: an Android app for learners, an ASP.NET Core API that owns all
business rules, and a React admin panel for content and operations.

| Part | Path | Stack |
|------|------|-------|
| Android app | [`android/`](android) | Kotlin, Jetpack Compose, MVVM + Clean Architecture, Hilt, Room, Retrofit, WorkManager |
| API | [`backend/`](backend) | ASP.NET Core 8, EF Core 8, SQL Server, Identity + JWT, Hangfire, Serilog |
| Admin panel | [`admin/`](admin) | React 18, TypeScript, MUI, TanStack Query, Vite |
| Code sandbox | [`sandbox/`](sandbox) | Docker images and isolation settings for running untrusted code |
| Scripts | [`scripts/`](scripts) | Database setup, seed content and generators, smoke test |

Features: question bank with 7 question types, timed quizzes with negative marking, coding challenges executed in an
isolated sandbox, AI mock interviews with adaptive follow-ups, AI answer evaluation and code review, learning paths,
spaced-repetition flashcards, company preparation with labelled content sources, analytics, XP/streaks/achievements,
offline-first sync, push reminders and Google Play subscriptions verified server-side.

## Quick start

```bash
cp .env.example .env && $EDITOR .env      # secrets stay out of Git
./sandbox/build-images.sh
docker compose up --build                 # API :5080 · admin :5173
```

Or run each part locally — see [docs/SetupGuide.md](docs/SetupGuide.md).

## Documentation

- [Architecture](docs/Architecture.md) · [ARCHITECTURE.md](ARCHITECTURE.md) (one-page map)
- [Database design](docs/DatabaseDesign.md)
- [API documentation](docs/API_Documentation.md)
- [Setup guide](docs/SetupGuide.md) · [Deployment guide](docs/DeploymentGuide.md)
- [User guide](docs/UserGuide.md)
- [CLAUDE.md](CLAUDE.md) (project rules) · [TASKS.md](TASKS.md) (status)

## Tests

```bash
cd backend && dotnet test
cd android && ./gradlew testDebugUnitTest :domain:test
cd admin && npm ci && npm test
```

## Content

All seeded questions, challenges, flashcards and lessons are original. Company profiles are marked *Illustrative*
practice material and make no claim about questions asked in real interviews.
