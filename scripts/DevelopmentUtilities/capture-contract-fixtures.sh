#!/usr/bin/env bash
# Captures real API responses into android/data/src/test/resources/contract for ApiContractTest.
# Run against a local API with seeded data, signed in as any account (an admin works).
# Usage: API=http://localhost:5080 EMAIL=admin@example.com PASSWORD='...' ./scripts/DevelopmentUtilities/capture-contract-fixtures.sh
# Tokens are never written to the fixtures.
set -euo pipefail
API="${API:-http://localhost:5080}"; B="$API/api/v1"
: "${EMAIL:?Set EMAIL}"; : "${PASSWORD:?Set PASSWORD}"
OUT="$(cd "$(dirname "$0")/../.." && pwd)/android/data/src/test/resources/contract"
mkdir -p "$OUT"; cd "$OUT"
command -v jq >/dev/null || { echo "jq is required"; exit 1; }

TOKEN=$(curl -fsS -X POST "$B/auth/login" -H 'Content-Type: application/json' \
  -d "$(jq -n --arg e "$EMAIL" --arg p "$PASSWORD" '{email:$e,password:$p,deviceName:"contract-fixtures"}')" | jq -r .data.accessToken)
H="Authorization: Bearer $TOKEN"
get()  { curl -fsS "$B/$1" -H "$H" > "$2.json"; echo "captured $2"; }
post() { curl -fsS -X POST "$B/$1" -H "$H" -H 'Content-Type: application/json' -d "$3" > "$2.json"; echo "captured $2"; }
field() { jq -r "$2" "$1.json"; }

get users/me me
get technologies technologies
TECH=$(field technologies '.data[0].id')
get "technologies/$TECH/categories" categories
get "questions?page=1&pageSize=5&technologyId=$TECH" questions
Q=$(field questions '.data.items[0].id')
get "questions/$Q" question
get "questions/$Q/answer" answer
OPT=$(field question '.data.options[0].id')
post "questions/$Q/attempt" attempt "{\"selectedOptionIds\":[\"$OPT\"],\"timeSpentSeconds\":20,\"selfAssessedCorrect\":null}"
get questions/report-reasons report-reasons
get "questions/notes?page=1&pageSize=100" notes
get collections collections
get "search?q=index&page=1&pageSize=20" search
get companies companies
C=$(field companies '.data[0].id'); get "companies/$C" company
CC=$(field company '.data.collections[0].id'); get "companies/collections/$CC" company-collection
get "quizzes?page=1&pageSize=20" quizzes
get quizzes/daily daily
QZ=$(field daily '.data.id')
post "quizzes/$QZ/start" quiz-start '{}'
A=$(field quiz-start '.data.attemptId')
post "quizzes/$QZ/submit" quiz-submit "{\"attemptId\":\"$A\",\"answers\":[]}"
get "quizzes/attempts/$A" quiz-result
get "quizzes/attempts?page=1&pageSize=20" quiz-history
get code/languages languages
get "challenges?page=1&pageSize=30" challenges
CH=$(field challenges '.data.items[0].id')
get "challenges/$CH" challenge
get "challenges/$CH/hints/0" hint
get "challenges/$CH/solution" solution
get "code/submissions?page=1&pageSize=20" submissions
get flashcards/decks decks
get "flashcards?dueOnly=false&bookmarkedOnly=false&limit=5" flashcards
get ai/status ai-status
get "ai/evaluations?page=1" evaluations
get "mock-interviews?page=1" interviews
get dashboard/summary dashboard
get "dashboard/activity?days=14" activity
get dashboard/skill-progress skills
get analytics/overview analytics
get analytics/technology analytics-tech
get "analytics/weekly?weeks=8" weekly
get "analytics/monthly?months=6" monthly
get gamification/profile gamification
get "gamification/leaderboard?period=weekly" leaderboard
get learning/paths paths
P=$(field paths '.data[0].id'); get "learning/paths/$P" path
L=$(field path '.data.modules[0].lessons[0].id'); get "learning/lessons/$L" lesson
get "notifications?page=1&pageSize=30" notifications
get notifications/preferences notification-prefs
get notifications/reminders reminders
get subscriptions/plans plans
get subscriptions/status subscription-status
get subscriptions/history purchase-history
get sync/pull sync-pull
if grep -l 'eyJhbGci' ./*.json >/dev/null 2>&1; then echo "A token leaked into a fixture; aborting"; exit 1; fi
echo "Fixtures written to $OUT"
