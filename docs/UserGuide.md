# User guide

## Learners (Android app)

### Getting started
1. Install the app and walk through onboarding.
2. **Create an account** with email and password (8+ characters with upper- and lower-case letters and a digit), or
   **Continue with Google**. Enter the 6-digit code sent to your email to activate the account.
3. Choose your experience level, current/target role and preferred technologies — they shape recommendations.

### Navigation
The bottom bar has **Home**, **Learn**, **Practice**, **Interview** and **Profile**; the menu (☰) on those screens
opens every other area (question bank, quizzes, coding, flashcards, companies, analytics, bookmarks, notes,
collections, Premium, settings and help).

### Home
Your level and XP, interview-readiness score, streak, today's question goal and weekly study time, quick actions,
continue-learning cards, the daily coding challenge, a 14-day activity chart, skill accuracy by technology,
recommended paths, recently practised questions and upcoming interviews. Pull down to refresh.

### Question bank (Practice → Question bank)
Pick a technology, then a category (or "All"). Filter by difficulty, attempt status (not attempted / got wrong /
got right) or bookmarks, search, and sort. On a question:
- **Multiple choice**: select an option and tap **Check answer** — the server grades it and shows the explanation
  for every option.
- **Written/scenario questions**: tap **Show answer**, compare, then tap **I knew it** or **I missed parts** to record
  progress — or **Write your answer and get AI feedback** (Premium).
- Use the toolbar to bookmark, add a personal note, share, add to a collection or **report a problem**.
- **Previous/Next** moves through the category. **Random question** is available from Practice and category screens.

Bookmarks and notes work offline and sync automatically. Notes can be exported as Markdown (Notes → export).

### Quizzes
- **Daily quiz**: the same set for everyone each day.
- **Curated quizzes** and **Custom quiz** (choose technology, topics, difficulty, number of questions, time limit and
  optional negative marking).
- During a quiz: jump between questions with the numbered chips, **Mark for review**, and **Submit** (you'll see how many
  are unanswered). Timed quizzes use the server's clock and submit automatically when time runs out; you can leave and
  resume, but the timer keeps running.
- Results show score, pass/fail, accuracy, time, a per-topic breakdown, recommended revision topics, and a full review
  (filter wrong/skipped answers).

### Coding challenges
Browse by category, difficulty and status. Each challenge shows the statement, input/output format, constraints,
examples, hints (revealed one by one) and, where available, the solution approach. In the editor:
- choose a language; your code is saved as a draft automatically per language,
- **Run** checks the sample tests (optionally with your own stdin), **Submit** runs all tests including hidden ones,
- the Console shows status (Accepted, Wrong answer, Compilation error, Runtime error, Time/Memory limit exceeded),
  per-test results and compiler output. Code runs on our servers in an isolated sandbox.

### Mock interviews and AI tools (Premium)
- **Mock interview**: choose the type (technical, HR, coding, SQL, .NET, React, full-stack, system design,
  managerial), stack, target role, level, number of questions and duration. Answer by typing or with the
  microphone; tap the speaker to have questions read aloud. The interviewer asks follow-ups and adapts difficulty.
  **Finish** at any time to get a report with scores, strengths, weaknesses, missing concepts, stronger sample answers
  and a learning plan.
- **Answer evaluator**: paste a question and your answer for scored feedback and an improved answer.
- **Code reviewer**: paste code for quality, performance, security, readability and maintainability scores, issues
  with fixes, potential bugs and a suggested version.

AI feedback is a practice aid and can be wrong; an AI code review is not a professional security audit.

### Learn
Learning paths are organised into modules and lessons (reading, exercise, quiz). Mark lessons complete to earn XP
and progress. Tap the download icon to keep a path's lessons available offline. **Company preparation** describes
typical interview processes and focus topics; every item is labelled *Verified from public source*, *Community
contributed* or *Illustrative practice* — illustrative sets are original practice material, not real interview
questions.

### Flashcards
Choose a deck (or bookmarked cards). Tap the card to flip, then grade **Again / Hard / Good / Easy** (or swipe right =
Good, left = Again once flipped). The server schedules the next review with spaced repetition. Reviews made offline
are uploaded later.

### Progress
- **Analytics**: readiness score breakdown, accuracy, weekly/monthly activity, difficulty breakdown, quiz and coding
  stats, mock-interview trend, per-technology accuracy, focus areas and recommended next steps.
- **Achievements**: level, XP history, streaks and badges.
- **Leaderboard**: weekly, monthly and all-time XP rankings (you can hide yourself in Edit profile).

### Profile & settings
Edit your profile, photo, goals and time zone; manage **Premium** (subscribe through Google Play, restore purchases,
see usage and history); set theme, notification preferences and reminder hours, add upcoming real interviews for
reminders, change password, sync now, export your data, sign out (or sign out everywhere) and delete your account.

### Free vs Premium
Free: question bank (daily view allowance), quizzes, flashcards, non-premium learning paths, code runs/submissions
with daily limits. Premium adds AI mock interviews, answer evaluation and code review, premium content and company
preparation, and higher daily limits. Current limits are shown on the Subscription screen.

## Staff (admin panel)

Sign in at the admin URL with an account that has the **ContentManager**, **Admin** or **SuperAdmin** role.

| Area | Who | What you can do |
|------|-----|-----------------|
| Dashboard | all staff | Users, activity, content and AI usage figures; content performance tables |
| Technologies & categories | all staff | Create/edit/hide technologies, categories and topics |
| Questions | all staff | Filter, create and edit questions (options, answers, explanations, tags, premium flag), bulk publish/draft/archive |
| Import / export | all staff | Download CSV/Excel templates, validate with a dry run, import, export by technology |
| Question reports | all staff | Review learner reports and resolve or reject them with a note |
| Quizzes, challenges, learning paths, flashcards, companies | all staff | Full editors, including test cases and starter code for challenges and content sources for company material |
| Users | Admin+ | Search, change roles (admin roles: Super Admin only), deactivate/reactivate, grant complimentary Premium |
| Subscriptions & plans | Admin+ | View/revoke subscriptions, edit plans and their Google Play product/base plan ids |
| AI | Admin+ | Usage and cost, versioned prompt overrides, provider status (keys are server-side only) |
| Broadcast | Admin+ | Send an in-app notification (optionally push) to all, Premium or Free users |
| Audit logs | Admin+ | Every administrative change with masked details |
| Settings | Admin+ (edit: Super Admin) | Runtime settings stored in the database |

Content rules enforced on save: choice questions need exactly one correct option, multiple-select at least three
options, true/false exactly two; published questions need a short answer; anything marked *Verified* must cite a
source reference. Never present original practice content as a company's real interview questions.
