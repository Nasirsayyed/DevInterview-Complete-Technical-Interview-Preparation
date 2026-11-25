#!/usr/bin/env bash
# End-to-end smoke test against a running API (default http://localhost:5080).
# Requires: curl, jq. In Development the email verification code is read from the API log (Email:Provider=Log).
set -euo pipefail
API="${API:-http://localhost:5080}/api/v1"
LOG="${API_LOG:-}"
EMAIL="smoke$(date +%s)@example.com"
PASS='Smoke#Test123'
j() { curl -s -H "Content-Type: application/json" ${TOKEN:+-H "Authorization: Bearer $TOKEN"} "$@"; }
step() { printf '\n== %s\n' "$1"; }

step "Register $EMAIL"
j -X POST "$API/auth/register" -d "{\"firstName\":\"Smoke\",\"lastName\":\"Test\",\"email\":\"$EMAIL\",\"password\":\"$PASS\",\"confirmPassword\":\"$PASS\",\"experienceLevel\":\"Intermediate\",\"currentJobRole\":\"Developer\",\"targetJobRole\":\"Senior Developer\",\"preferredTechnologyIds\":[]}" | jq -c '{success, verify: .data.emailVerificationRequired}'

step "Login before verification (expect email_not_verified)"
j -X POST "$API/auth/login" -d "{\"email\":\"$EMAIL\",\"password\":\"$PASS\"}" | jq -c '.error.code'

if [ -n "$LOG" ]; then
  sleep 1
  CODE=$(grep "To: $EMAIL" "$LOG" | grep -o 'Code: [0-9]\{6\}' | tail -1 | cut -d' ' -f2)
  step "Verify email with code from log"
  TOKEN=$(j -X POST "$API/auth/verify-email" -d "{\"email\":\"$EMAIL\",\"code\":\"$CODE\"}" | jq -r '.data.accessToken')
else
  echo "Set API_LOG to the API log file to complete verification automatically."; exit 1
fi
REFRESH=$(TOKEN="" j -X POST "$API/auth/login" -d "{\"email\":\"$EMAIL\",\"password\":\"$PASS\"}" | jq -r '.data.refreshToken')

step "Technologies"
j "$API/technologies" | jq -c '[.data[] | {name, questionCount}] | .[0:5]'
TECH=$(j "$API/technologies" | jq -r '.data[] | select(.slug=="csharp") | .id')

step "Questions (C#, page 1)"
QID=$(j "$API/questions?technologyId=$TECH&pageSize=5" | tee /tmp/q.json | jq -r '.data.items[0].id')
jq -c '{total: .data.totalCount, first: .data.items[0].title}' /tmp/q.json

step "Question detail + attempt"
OPT=$(j "$API/questions/$QID" | jq -r '.data.options[0].id')
j -X POST "$API/questions/$QID/attempt" -d "{\"selectedOptionIds\":[\"$OPT\"],\"timeSpentSeconds\":30}" | jq -c '{isCorrect: .data.isCorrect, xp: .data.xpAwarded}'

step "Bookmark + note"
j -X POST "$API/questions/$QID/bookmark" | jq -c '.success'
j -X POST "$API/questions/$QID/notes" -d '{"content":"Remember: DateTime is a struct."}' | jq -c '.data.content'

step "Custom quiz: create, start, submit"
QUIZ=$(j -X POST "$API/quizzes" -d "{\"mode\":\"Technology\",\"technologyIds\":[\"$TECH\"],\"questionCount\":5,\"timeLimitSeconds\":300,\"negativeMarkingFraction\":0,\"passingPercentage\":60}" | jq -r '.data.id')
ATTEMPT=$(j -X POST "$API/quizzes/$QUIZ/start" | tee /tmp/a.json | jq -r '.data.attemptId')
ANSWERS=$(jq -c '[.data.questions[] | {questionId, selectedOptionIds: [.options[0].id], isMarkedForReview: false, timeSpentSeconds: 10}]' /tmp/a.json)
j -X POST "$API/quizzes/$QUIZ/submit" -d "{\"attemptId\":\"$ATTEMPT\",\"answers\":$ANSWERS}" | jq -c '{status: .data.status, correct: .data.correct, total: .data.totalQuestions, percentage: .data.percentage, xp: .data.xpAwarded}'
step "Duplicate submit is idempotent"
j -X POST "$API/quizzes/$QUIZ/submit" -d "{\"attemptId\":\"$ATTEMPT\",\"answers\":[]}" | jq -c '{status: .data.status, xp: .data.xpAwarded}'

step "Dashboard summary"
j "$API/dashboard/summary" | jq -c '.data | {firstName, tier, level: .level.level, xp: .level.totalXp, streak: .currentStreak, attempted: .questionsAttempted, prep: .preparationScore, dailyChallenge: .dailyChallenge.title}'

step "Flashcards"
CARD=$(j "$API/flashcards?limit=3" | jq -r '.data[0].id')
j -X POST "$API/flashcards/review" -d "{\"flashcardId\":\"$CARD\",\"grade\":\"Good\",\"clientReviewId\":\"$(cat /proc/sys/kernel/random/uuid)\"}" | jq -c '.data | {intervalDays, nextDueAt}'

step "Learning paths"
j "$API/learning/paths" | jq -c '[.data[] | {title, lessonCount}] | .[0:3]'

step "Search 'index'"
j "$API/search?q=index" | jq -c '.data.countsByType'

step "Refresh token rotation + reuse detection"
NEW=$(TOKEN="" j -X POST "$API/auth/refresh" -d "{\"refreshToken\":\"$REFRESH\"}" | jq -r '.data.refreshToken')
TOKEN="" j -X POST "$API/auth/refresh" -d "{\"refreshToken\":\"$REFRESH\"}" | jq -c '.error.code'
TOKEN="" j -X POST "$API/auth/refresh" -d "{\"refreshToken\":\"$NEW\"}" | jq -c '.error.code'

step "AI status (premium/provider)"
j "$API/ai/status" | jq -c '.data'
echo; echo "Smoke test finished."
