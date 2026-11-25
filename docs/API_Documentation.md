# API documentation

Base URL: `https://<host>/api/v1` (local: `http://localhost:5080/api/v1`). Interactive Swagger UI is served at
`/swagger` when `Swagger:Enabled=true` (default in Development). The OpenAPI document is the source of truth; the
inventory below is generated from it.

## Conventions

- JSON, camelCase; enums as strings (e.g. `"difficulty": "Intermediate"`); timestamps ISO-8601 UTC.
- Authentication: `Authorization: Bearer <accessToken>`. Access tokens last 15 minutes; refresh with
  `POST /auth/refresh { "refreshToken": "…" }` (rotates the refresh token — always store the new one).
- Paging: `?page=1&pageSize=20` (max 100), optional `search` and `sort`. Paged results:
  `{ items, page, pageSize, totalCount, totalPages, hasNext }`.
- Idempotency: `POST /code/submit` accepts an `idempotencyKey`; flashcard reviews carry a `clientReviewId`.

### Response envelope

```json
{ "success": true, "data": { "...": "..." }, "error": null, "traceId": "0HN…" }
```

```json
{ "success": false, "data": null,
  "error": { "code": "validation_error", "message": "Email is invalid.", "details": { "email": ["Email is invalid."] } },
  "traceId": "0HN…" }
```

### Error codes

| HTTP | `error.code` | Meaning |
|------|--------------|---------|
| 400 | `validation_error` | Field errors in `details` |
| 401 | `unauthorized`, `invalid_credentials`, `invalid_token`, `account_locked` | Missing/expired token, wrong password, refresh token reuse, lockout after repeated failures |
| 402 | `premium_required` | Feature needs an active Premium entitlement |
| 403 | `forbidden`, `email_not_verified` | Role/ownership, unverified email |
| 404 | `not_found` | Resource missing or not visible |
| 409 | `conflict` | State conflicts (e.g. purchase owned by another account, duplicate slug) |
| 422 | `quiz_expired` and other business-rule codes | Request is valid but not allowed in the current state |
| 429 | `quota_exceeded`, `rate_limited` | Daily plan quota or request rate limit |
| 503 | `ai_unavailable`, `ai_invalid_response`, `execution_unavailable`, `billing_unavailable` | Dependency not configured or failing |
| 500 | `internal_error` | Unexpected; quote the `traceId` |

### Rate limits (requests per minute; configurable under `RateLimits`)

Global 300 · auth 10 · AI 12 · code execution 20.

### Example: sign in, then call an authenticated endpoint

```bash
curl -s -X POST http://localhost:5080/api/v1/auth/login -H 'Content-Type: application/json' \
  -d '{"email":"learner@example.com","password":"<password>","deviceName":"curl"}'
# → data.accessToken, data.refreshToken, data.user { id, email, roles, tier, … }

curl -s http://localhost:5080/api/v1/dashboard/summary -H "Authorization: Bearer $ACCESS_TOKEN"
```

### Example: run code against sample tests

```bash
curl -s -X POST http://localhost:5080/api/v1/code/run -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"challengeId":"<id>","languageCode":"python","sourceCode":"print(sum(map(int, input().split())))","customInput":null}'
# → data { status: "Accepted" | "WrongAnswer" | "CompilationError" | …, results[], passedCount, totalCount }
```

### Access levels

- Learner endpoints require a signed-in user (any role) unless marked anonymous in Swagger (registration, login,
  refresh, password reset, email verification, Google sign-in, Play RTDN with shared secret).
- `/admin/*` requires `SuperAdmin`, `Admin` or `ContentManager`; users, subscriptions, plans, AI, broadcast, audit
  and settings require `Admin`/`SuperAdmin`; changing settings and granting admin roles require `SuperAdmin`.

## Endpoint inventory

### Ai

| Method | Path | Description |
|---|---|---|
| POST | `/api/v1/ai/evaluate-answer` |  |
| GET | `/api/v1/ai/evaluations` |  |
| GET | `/api/v1/ai/evaluations/{id}` |  |
| POST | `/api/v1/ai/explain-code` |  |
| POST | `/api/v1/ai/generate-questions` | Admin: generates draft questions for review in the admin panel. |
| POST | `/api/v1/ai/review-code` |  |
| GET | `/api/v1/ai/status` |  |

### Analytics

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/analytics/monthly` |  |
| GET | `/api/v1/analytics/overview` |  |
| GET | `/api/v1/analytics/technology` | Per-technology and per-category accuracy (Premium: advanced analytics). |
| GET | `/api/v1/analytics/weekly` |  |

### Auth

| Method | Path | Description |
|---|---|---|
| POST | `/api/v1/auth/change-password` |  |
| POST | `/api/v1/auth/forgot-password` |  |
| POST | `/api/v1/auth/google` | Signs in with a Google ID token obtained on the device via Credential Manager. |
| POST | `/api/v1/auth/login` |  |
| POST | `/api/v1/auth/logout` |  |
| POST | `/api/v1/auth/refresh` | Exchanges a refresh token for a new access/refresh token pair (rotation). |
| POST | `/api/v1/auth/register` | Creates an account. When email verification is required, a 6-digit code is emailed and no tokens are returned. |
| POST | `/api/v1/auth/resend-verification` |  |
| POST | `/api/v1/auth/reset-password` |  |
| POST | `/api/v1/auth/verify-email` |  |

### Challenges

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/challenges` |  |
| GET | `/api/v1/challenges/daily` |  |
| GET | `/api/v1/challenges/{id}` |  |
| GET | `/api/v1/challenges/{id}/hints/{index}` |  |
| GET | `/api/v1/challenges/{id}/solution` |  |

### Code

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/code/languages` |  |
| POST | `/api/v1/code/run` | Runs code against the sample test cases and an optional custom input. |
| GET | `/api/v1/code/submissions` |  |
| GET | `/api/v1/code/submissions/{id}` |  |
| POST | `/api/v1/code/submit` | Submits code against all (sample + hidden) test cases. Supply an IdempotencyKey to make retries safe. |

### Collections

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/collections` |  |
| POST | `/api/v1/collections` |  |
| DELETE | `/api/v1/collections/{id}` |  |
| GET | `/api/v1/collections/{id}` |  |
| PUT | `/api/v1/collections/{id}` |  |
| POST | `/api/v1/collections/{id}/items` |  |
| DELETE | `/api/v1/collections/{id}/items/{itemId}` |  |

### Companies

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/companies` |  |
| GET | `/api/v1/companies/collections/{id}` |  |
| GET | `/api/v1/companies/{id}` |  |

### Dashboard

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/dashboard/activity` |  |
| GET | `/api/v1/dashboard/skill-progress` |  |
| GET | `/api/v1/dashboard/summary` |  |

### Flashcards

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/flashcards` | Cards for a study session: due cards first (most overdue), then new cards. |
| GET | `/api/v1/flashcards/decks` |  |
| POST | `/api/v1/flashcards/review` |  |
| PUT | `/api/v1/flashcards/{id}/bookmark` |  |

### Gamification

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/gamification/leaderboard` | Leaderboard by XP. Period: weekly, monthly or all. |
| GET | `/api/v1/gamification/profile` |  |

### Learning

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/learning/lessons/{id}` |  |
| GET | `/api/v1/learning/paths` |  |
| GET | `/api/v1/learning/paths/{id}` |  |
| POST | `/api/v1/learning/progress` |  |
| GET | `/api/v1/learning/recommended` |  |

### Me

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/users/me` |  |
| PUT | `/api/v1/users/me` |  |
| POST | `/api/v1/users/me/delete` | Permanently deletes the account and personal data (requires password for password accounts). |
| DELETE | `/api/v1/users/me/devices` |  |
| POST | `/api/v1/users/me/devices` |  |
| GET | `/api/v1/users/me/export` | Exports the user's personal data as JSON (data portability). |
| DELETE | `/api/v1/users/me/photo` |  |
| POST | `/api/v1/users/me/photo` | Uploads a JPEG/PNG/WebP profile photo (max 2 MB). File type is verified by content. |
| PUT | `/api/v1/users/me/preferences` |  |

### MockInterviews

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/mock-interviews` |  |
| POST | `/api/v1/mock-interviews/start` |  |
| GET | `/api/v1/mock-interviews/{id}` |  |
| POST | `/api/v1/mock-interviews/{id}/answer` |  |
| POST | `/api/v1/mock-interviews/{id}/complete` |  |
| POST | `/api/v1/mock-interviews/{id}/next-question` | Skips the current question and asks the next one. |
| GET | `/api/v1/mock-interviews/{id}/report` |  |

### Notifications

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/notifications` |  |
| GET | `/api/v1/notifications/preferences` |  |
| PUT | `/api/v1/notifications/preferences` |  |
| POST | `/api/v1/notifications/read-all` |  |
| GET | `/api/v1/notifications/reminders` |  |
| POST | `/api/v1/notifications/reminders` |  |
| DELETE | `/api/v1/notifications/reminders/{id}` |  |
| POST | `/api/v1/notifications/{id}/read` |  |

### Questions

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/questions` | Paged, filterable question list. Sort: difficulty (default), difficulty_desc, title, newest. |
| GET | `/api/v1/questions/bookmarks` |  |
| GET | `/api/v1/questions/notes` |  |
| GET | `/api/v1/questions/notes/export` | Exports all notes as Markdown (default) or CSV. |
| GET | `/api/v1/questions/random` |  |
| GET | `/api/v1/questions/report-reasons` |  |
| GET | `/api/v1/questions/{id}` |  |
| GET | `/api/v1/questions/{id}/answer` | Reveals the answer and detailed explanation (counts toward the free daily limit). |
| POST | `/api/v1/questions/{id}/attempt` | Submits a practice attempt; objective questions are graded server-side. |
| DELETE | `/api/v1/questions/{id}/bookmark` |  |
| POST | `/api/v1/questions/{id}/bookmark` |  |
| DELETE | `/api/v1/questions/{id}/notes` |  |
| POST | `/api/v1/questions/{id}/notes` |  |
| POST | `/api/v1/questions/{id}/report` |  |

### Quizzes

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/quizzes` |  |
| POST | `/api/v1/quizzes` | Generates a custom quiz from the question bank using the given filters. |
| GET | `/api/v1/quizzes/attempts` |  |
| GET | `/api/v1/quizzes/attempts/{attemptId}` |  |
| PUT | `/api/v1/quizzes/attempts/{attemptId}/answers` | Saves one answer (and mark-for-review state) during an attempt. |
| GET | `/api/v1/quizzes/attempts/{attemptId}/state` |  |
| GET | `/api/v1/quizzes/daily` |  |
| GET | `/api/v1/quizzes/{id}` |  |
| POST | `/api/v1/quizzes/{id}/start` | Starts (or resumes) an attempt. Returns questions without correct answers plus the server deadline. |
| POST | `/api/v1/quizzes/{id}/submit` | Submits the attempt. Idempotent: repeated submits return the original result. |

### Search

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/search` | Global search across questions, technologies, categories, challenges, learning paths, flashcards and the user's notes. |

### Subscriptions

| Method | Path | Description |
|---|---|---|
| POST | `/api/v1/subscriptions/google-play/rtdn` | Google Play Real-Time Developer Notifications (Pub/Sub push). Protected by a shared secret query parameter. |
| POST | `/api/v1/subscriptions/google-play/verify` | Verifies a Google Play purchase token with Google and activates the entitlement. |
| GET | `/api/v1/subscriptions/history` |  |
| GET | `/api/v1/subscriptions/plans` |  |
| GET | `/api/v1/subscriptions/status` | Current tier, expiry and today's usage against plan limits (always computed server-side). |

### Sync

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/sync/pull` |  |
| POST | `/api/v1/sync/push` |  |

### Technologies

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/technologies` |  |
| GET | `/api/v1/technologies/{id}/categories` |  |

### AdminCatalog

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/admin/challenges` |  |
| POST | `/api/v1/admin/challenges` |  |
| DELETE | `/api/v1/admin/challenges/{id}` |  |
| GET | `/api/v1/admin/challenges/{id}` |  |
| PUT | `/api/v1/admin/challenges/{id}` |  |
| GET | `/api/v1/admin/companies` |  |
| POST | `/api/v1/admin/companies` |  |
| DELETE | `/api/v1/admin/companies/{id}` |  |
| GET | `/api/v1/admin/companies/{id}` |  |
| PUT | `/api/v1/admin/companies/{id}` |  |
| GET | `/api/v1/admin/flashcards` |  |
| POST | `/api/v1/admin/flashcards` |  |
| DELETE | `/api/v1/admin/flashcards/{id}` |  |
| PUT | `/api/v1/admin/flashcards/{id}` |  |
| GET | `/api/v1/admin/learning-paths` |  |
| POST | `/api/v1/admin/learning-paths` |  |
| DELETE | `/api/v1/admin/learning-paths/{id}` |  |
| GET | `/api/v1/admin/learning-paths/{id}` |  |
| PUT | `/api/v1/admin/learning-paths/{id}` |  |
| GET | `/api/v1/admin/quizzes` |  |
| POST | `/api/v1/admin/quizzes` |  |
| DELETE | `/api/v1/admin/quizzes/{id}` |  |
| GET | `/api/v1/admin/quizzes/{id}` |  |
| PUT | `/api/v1/admin/quizzes/{id}` |  |

### AdminContent

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/admin/categories` |  |
| POST | `/api/v1/admin/categories` |  |
| DELETE | `/api/v1/admin/categories/{id}` |  |
| PUT | `/api/v1/admin/categories/{id}` |  |
| GET | `/api/v1/admin/question-reports` |  |
| PUT | `/api/v1/admin/question-reports/{id}` |  |
| GET | `/api/v1/admin/questions` |  |
| POST | `/api/v1/admin/questions` |  |
| POST | `/api/v1/admin/questions/bulk-status` |  |
| GET | `/api/v1/admin/questions/export` |  |
| POST | `/api/v1/admin/questions/import` | Imports questions from a .csv or .xlsx file using the template columns. Use dryRun=true to validate first. |
| GET | `/api/v1/admin/questions/import-template` |  |
| DELETE | `/api/v1/admin/questions/{id}` |  |
| GET | `/api/v1/admin/questions/{id}` | Full question including answers; used for editing and preview before publishing. |
| PUT | `/api/v1/admin/questions/{id}` |  |
| GET | `/api/v1/admin/technologies` |  |
| POST | `/api/v1/admin/technologies` |  |
| DELETE | `/api/v1/admin/technologies/{id}` |  |
| PUT | `/api/v1/admin/technologies/{id}` |  |
| POST | `/api/v1/admin/topics` |  |
| DELETE | `/api/v1/admin/topics/{id}` |  |
| PUT | `/api/v1/admin/topics/{id}` |  |

### AdminOperations

| Method | Path | Description |
|---|---|---|
| GET | `/api/v1/admin/ai/prompts` |  |
| POST | `/api/v1/admin/ai/prompts` |  |
| GET | `/api/v1/admin/ai/providers` |  |
| GET | `/api/v1/admin/ai/usage` |  |
| GET | `/api/v1/admin/audit-logs` |  |
| GET | `/api/v1/admin/dashboard` |  |
| POST | `/api/v1/admin/notifications/broadcast` |  |
| GET | `/api/v1/admin/plans` |  |
| POST | `/api/v1/admin/plans` |  |
| PUT | `/api/v1/admin/plans/{id}` |  |
| GET | `/api/v1/admin/reports/content` |  |
| GET | `/api/v1/admin/settings` |  |
| PUT | `/api/v1/admin/settings` |  |
| GET | `/api/v1/admin/subscriptions` |  |
| POST | `/api/v1/admin/subscriptions/{id}/revoke` |  |
| GET | `/api/v1/admin/users` |  |
| GET | `/api/v1/admin/users/{id}` |  |
| PUT | `/api/v1/admin/users/{id}/active` |  |
| PUT | `/api/v1/admin/users/{id}/roles` |  |
| POST | `/api/v1/admin/users/{id}/subscriptions` |  |
