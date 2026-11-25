# Setup guide (local development)

## Prerequisites

| Tool | Version | Used for |
|------|---------|----------|
| .NET SDK | 8.0.x | API, tests, EF Core tooling (`dotnet tool restore` installs `dotnet-ef` 8.0.34 from `backend/.config`) |
| Docker | 24+ | SQL Server container and the code-execution sandbox |
| JDK | 17 or 21 | Android build (Gradle 8.14 wrapper, AGP 8.13) |
| Android SDK | Platform 36, build-tools 36 | Android app (`ANDROID_HOME` or `android/local.properties`) |
| Node.js | 22.x (npm 10) | Admin panel |

## 1. Database

```bash
export MSSQL_SA_PASSWORD='<a strong local password>'
./scripts/DatabaseSetup/start-local-sqlserver.sh
```

## 2. API

Configuration is layered: `appsettings.json` (safe defaults) → `appsettings.Development.json` → user-secrets →
environment variables (`Section__Key`). Never put secrets in the JSON files.

```bash
cd backend
dotnet user-secrets --project src/DevInterview.API set "ConnectionStrings:Default" \
  "Server=localhost,1433;Database=DevInterview;User Id=sa;Password=<password>;TrustServerCertificate=True"
dotnet user-secrets --project src/DevInterview.API set "Seed:AdminEmail" "admin@example.com"
dotnet user-secrets --project src/DevInterview.API set "Seed:AdminPassword" "<Strong!Passw0rd>"
# Optional — enable AI features
dotnet user-secrets --project src/DevInterview.API set "AI:Providers:Anthropic:ApiKey" "<key>"
dotnet run --project src/DevInterview.API          # http://localhost:5080, Swagger at /swagger
```

On first start the API applies migrations and loads the seed packs from `scripts/SeedData` (22 technologies,
236 questions, 23 coding challenges, 85 flashcards, 10 learning paths, 10 company profiles, plans, roles and
achievements). Seeding is idempotent.

In Development, verification and password-reset codes are written to the API log (`Email:Provider=Log`).
For real email set `Email:Provider=Smtp`, `Email:Host`, `Email:Port`, `Email:Username`, `Email:Password`.

### Code execution

```bash
./sandbox/build-images.sh                                  # builds/pulls the language images
dotnet user-secrets --project src/DevInterview.API set "CodeExecution:Provider" "Docker"
```

`appsettings.Development.json` already selects `Docker`; use `Disabled` if Docker is unavailable.

### Tests

```bash
cd backend
dotnet test                                                     # unit + integration (SQLite)
DEVINTERVIEW_TEST_SQLSERVER="Server=localhost,1433;User Id=sa;Password=<password>;TrustServerCertificate=True" dotnet test
                                                                # also runs migrations against real SQL Server
```

`scripts/DevelopmentUtilities/smoke-test.sh` exercises a running API end to end — registration and email
verification, questions, bookmarks/notes, a custom quiz (including idempotent re-submit), dashboard, flashcards,
learning paths, search, refresh-token rotation with reuse detection and AI status. Set `API_LOG` to the API log file
so it can read the verification code.

## 3. Android app

```bash
cd android
echo "sdk.dir=$ANDROID_HOME" > local.properties   # if ANDROID_HOME is not exported
./gradlew assembleDebug testDebugUnitTest
```

Build properties (in `~/.gradle/gradle.properties` or `-P` flags, never committed when they are private):

| Property | Default | Purpose |
|----------|---------|---------|
| `devinterview.apiBaseUrl` | `http://10.0.2.2:5080/` | API base URL (emulator → host machine) |
| `devinterview.googleWebClientId` | empty | OAuth *web* client id for Google sign-in; the button is hidden when empty |
| `devinterview.versionCode` / `devinterview.versionName` | `1` / `1.0.0` | Release versioning |

Firebase (push + Crashlytics) is enabled only when `android/app/google-services.json` exists (git-ignored).
Google Play Billing works with an app uploaded to a Play Console testing track; on other builds the subscription
screen shows that Play plans are unavailable.

Debug builds allow cleartext HTTP so they can reach a local API; release builds do not.

## 4. Admin panel

```bash
cd admin
cp .env.example .env.local        # VITE_API_BASE_URL=http://localhost:5080
npm ci
npm run dev                       # http://localhost:5173
npm test && npm run build
```

Sign in with the seeded Super Admin. The API's `Cors:AllowedOrigins` must include the admin origin.

## 5. Everything in Docker

```bash
cp .env.example .env              # fill in MSSQL_SA_PASSWORD, JWT_SIGNING_KEY, SEED_ADMIN_* (and AI keys if wanted)
./sandbox/build-images.sh
docker compose up --build         # API :5080, admin :5173, SQL Server :1433
```

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| API exits with "development JWT signing key must not be used" | Set `Jwt__SigningKey` (32+ random bytes) outside Development |
| Run/Submit returns `execution_unavailable` | `CodeExecution:Provider` is `Disabled`, Docker is not reachable, or images are missing |
| AI screens say "temporarily unavailable" | No provider API key configured, or the provider is failing (see API log / admin AI usage) |
| Android cannot reach the API | Use `10.0.2.2` on the emulator; on a device use the host's LAN IP and run the API with `--urls http://0.0.0.0:5080` |
