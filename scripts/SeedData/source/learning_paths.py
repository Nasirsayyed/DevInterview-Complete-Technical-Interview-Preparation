from seedlib import write


def lesson(title, content, minutes=12, kind="Reading", questions=None, exercise=None, quiz=None):
    return {"title": title, "type": kind, "minutes": minutes, "content": content.strip(), "exercise": exercise,
            "questions": questions or [], "quiz": quiz}


def module(title, description, *lessons):
    return {"title": title, "description": description, "lessons": list(lessons)}


def path(key, title, description, level, hours, tech, modules, premium=False):
    return {"key": key, "title": title, "description": description, "level": level, "hours": hours, "premium": premium,
            "tech": tech, "modules": modules}


PATHS = [
    path("lp-csharp", "Complete C# Developer Roadmap", "From the type system to async and memory management — everything a C# interview covers.",
         "Beginner", 30, "csharp", [
        module("Language Foundations", "Types, memory model and object orientation.",
               lesson("Value types and reference types", """
## Value vs reference semantics
- **Value types** (`struct`, `enum`, numeric types, `DateTime`) store data directly and are **copied** on assignment.
- **Reference types** (`class`, `string`, arrays, delegates) store a reference to a heap object; assignment copies the reference.

## Strings are special
`string` is a reference type with **value equality** and **immutability**: every modification creates a new string.

## Interview tips
- Explain *boxing*: converting a value type to `object` allocates on the heap.
- Know when to choose a `struct`: small (≤ 16 bytes), immutable, value-like (e.g. `Point`, `Money`).
""", questions=["cs-001", "cs-002", "cs-005"]),
               lesson("Classes, inheritance and polymorphism", """
## Four pillars in C#
- **Encapsulation** — access modifiers and properties protect invariants.
- **Inheritance** — single class inheritance, many interfaces.
- **Polymorphism** — `virtual`/`override` dispatches at runtime; `new` only hides.
- **Abstraction** — abstract classes and interfaces.

## Interfaces vs abstract classes
Interfaces describe capabilities and can have default implementations (C# 8+) but no instance state. Abstract classes share state and code among related types.
""", questions=["cs-006", "cs-007", "oo-002"]),
               lesson("Checkpoint: C# fundamentals quiz", "Test yourself on the fundamentals before moving on.", 15, "Quiz", quiz="C# Fundamentals")),
        module("Collections, Generics and LINQ", "Choosing collections and writing efficient queries.",
               lesson("Choosing the right collection", """
| Need | Collection | Complexity |
|---|---|---|
| Ordered list, index access | `List<T>` | O(1) index, O(1)* append |
| Key → value lookup | `Dictionary<K,V>` | O(1) average |
| Uniqueness | `HashSet<T>` | O(1) average |
| Sorted keys | `SortedDictionary<K,V>` | O(log n) |
| FIFO / LIFO | `Queue<T>` / `Stack<T>` | O(1) |
| Priority | `PriorityQueue<T,P>` | O(log n) |

Generics avoid boxing and give compile-time type safety. Variance (`out T`) is only on interfaces and delegates.
""", questions=["cs-008", "cs-009", "cs-010"]),
               lesson("LINQ and deferred execution", """
LINQ operators like `Where` and `Select` are **deferred**: they run when enumerated. Operators like `Count()`, `ToList()` and `First()` execute immediately.

- Materialise once with `ToList()` if you enumerate multiple times.
- `IQueryable<T>` sends expression trees to a provider (EF Core → SQL); `IEnumerable<T>` runs in memory.
- `Single()` asserts exactly one element; `First()` takes the first.
""", questions=["cs-011", "cs-012", "cs-013"]),
               lesson("Exercise: refactor loops to LINQ", "Practise expressing loops declaratively.", 20, "Exercise",
                      exercise="Given a list of orders (Id, CustomerId, Total, CreatedAt), write LINQ queries that: (1) return the top 3 customers by total spend, "
                               "(2) group orders by month with totals, (3) find customers with no orders in the last 30 days. Then explain which queries would "
                               "be translated to SQL by EF Core and which would run in memory.")),
        module("Async, Errors and Memory", "Production-grade async code and resource management.",
               lesson("How async/await really works", """
`await` on an incomplete task **returns to the caller** and resumes later as a continuation. No thread is blocked while I/O is in flight.

**Rules**
1. Async all the way — never `.Result`/`.Wait()` (deadlocks, thread-pool starvation).
2. Run independent I/O concurrently with `Task.WhenAll`.
3. `async void` only for event handlers.
4. Pass `CancellationToken` through every async call.
5. Use `SemaphoreSlim` instead of `lock` around `await`.
""", 15, questions=["cs-016", "cs-017", "cs-018", "cs-019", "cs-020"]),
               lesson("Garbage collection and IDisposable", """
The GC is generational (Gen0/1/2) plus the **Large Object Heap** for objects ≥ 85,000 bytes. It reclaims managed memory only.

`IDisposable` releases **unmanaged** resources deterministically — use `using` / `await using`. The GC never calls `Dispose`; it may run finalizers.

Reduce allocations in hot paths with `Span<T>`, `ArrayPool<T>` and `StringBuilder`.
""", questions=["cs-021", "cs-022", "cs-023"]),
               lesson("Exceptions done right", """
- Rethrow with `throw;` to keep the stack trace.
- Catch specific exceptions; don't use exceptions for control flow.
- `finally` always runs when leaving `try` (except process termination).
- Wrap low-level exceptions with context using `InnerException`.
""", 10, questions=["cs-024", "cs-025", "cs-030"])),
    ]),

    path("lp-aspnet", "ASP.NET Core Developer Roadmap", "Build, secure and scale production Web APIs with ASP.NET Core.", "Intermediate", 28, "aspnet-core", [
        module("Hosting, Pipeline and DI", "How a request flows through an ASP.NET Core app.",
               lesson("The request pipeline", """
Middleware runs **in registration order** on the way in and in **reverse** on the way out. Typical order:

1. Exception handling
2. HTTPS redirection / HSTS
3. Static files
4. Routing
5. CORS
6. Authentication → Authorization
7. Endpoints

`app.Run` is terminal; `app.Use` can call `next`.
""", questions=["an-001", "an-002", "an-003"]),
               lesson("Dependency injection lifetimes", """
- **Transient** — new instance per resolution.
- **Scoped** — one per request (DbContext).
- **Singleton** — one per application; must be thread-safe.

Avoid **captive dependencies** (singleton → scoped). In background services create scopes with `IServiceScopeFactory`.
""", questions=["an-004", "an-005", "an-006"]),
               lesson("Configuration and options", """
Providers override in order: appsettings.json → appsettings.{Env}.json → user secrets → environment variables → command line.

Bind sections to typed classes with `services.Configure<T>(config.GetSection("X"))` and inject `IOptions<T>`. Keep secrets in environment variables or a vault — never in source control.
""", 10, questions=["an-019", "an-018"])),
        module("Building Web APIs", "Controllers, minimal APIs, filters and HTTP semantics.",
               lesson("HTTP semantics for APIs", """
- `201 Created` + Location for new resources; `204 No Content` for empty success.
- `400` validation, `401` unauthenticated, `403` forbidden, `404` not found, `409` conflict, `429` rate limited.
- PUT/DELETE are idempotent; make POST retries safe with idempotency keys.
- Version your API (`/api/v1`) and return a consistent error envelope.
""", questions=["an-008", "an-009", "ap-002", "ap-004"]),
               lesson("Controllers, minimal APIs and filters", """
`[ApiController]` enables automatic 400 responses and binding inference. Minimal APIs (`MapGet`, `MapGroup`) are lighter for simple endpoints.

Filters run inside MVC: authorization → resource → action → result, with exception filters for MVC errors. Prefer **middleware** for global exception handling.
""", questions=["an-007", "an-010", "an-011", "an-015"]),
               lesson("Exercise: design an orders API", "Design endpoints for an orders service.", 25, "Exercise",
                      exercise="Design REST endpoints for creating, listing (with filtering, sorting and cursor pagination), updating and cancelling orders. "
                               "Specify routes, methods, status codes, request/response DTOs, validation rules and how you would make 'create order' safe to retry.")),
        module("Security and Performance", "Authentication, authorization and scaling an API.",
               lesson("JWT authentication and refresh tokens", """
Access tokens should be **short-lived** (minutes) because self-contained JWTs can't be revoked easily. Pair them with **refresh tokens** stored hashed on the server, **rotated** on every use, with reuse detection that revokes the family.

Validate signature, issuer, audience and lifetime. Use policies (`RequireRole`, `RequireClaim`) for authorization.
""", 15, questions=["an-012", "an-013", "an-014", "se-005"]),
               lesson("Performance and resilience", """
- Use `IHttpClientFactory` with timeouts, retries (exponential backoff + jitter) and circuit breakers.
- Avoid sync-over-async to prevent thread-pool starvation.
- Cache judiciously (in-memory, distributed, output caching).
- Add rate limiting and health checks; observe with OpenTelemetry traces and metrics.
""", questions=["an-017", "sd-010", "sd-011"]),
               lesson("Checkpoint: ASP.NET Core quiz", "Verify your understanding of ASP.NET Core.", 15, "Quiz", quiz="ASP.NET Core Fundamentals")),
    ]),

    path("lp-fullstack-dotnet", "Full Stack .NET Developer Roadmap", "Connect C#, ASP.NET Core, EF Core, SQL Server and React into one coherent skill set.",
         "Intermediate", 40, None, [
        module("Data Access with EF Core and SQL", "From LINQ to efficient SQL.",
               lesson("EF Core essentials", """
- `DbContext` is scoped and not thread-safe; it implements **Unit of Work**.
- Use migrations for schema changes; generate idempotent scripts for production.
- Prefer projections (`Select` into DTOs) and `AsNoTracking()` for reads.
- Watch for **N+1** queries — use `Include` or projections.
""", questions=["ef-001", "ef-002", "ef-003", "ef-005", "oo-015"]),
               lesson("Indexes every developer should know", """
A **clustered index** orders the table; **nonclustered** indexes point back to it. Make predicates **SARGable**, add **INCLUDE** columns to cover queries, and read execution plans for Key Lookups and scans.
""", questions=["sq-012", "sq-013", "sq-014", "sq-015"])),
        module("Frontend with React", "Modern React for .NET developers.",
               lesson("Components, state and effects", """
Components are functions of props and state. Updates are batched; use functional updates when the next state depends on the previous. Effects synchronise with external systems and need honest dependency arrays.
""", questions=["re-002", "re-004", "re-005", "re-008"]),
               lesson("Calling your API from React", """
- Centralise API calls with typed clients.
- Handle loading, error and empty states explicitly.
- Store access tokens in memory; refresh with HttpOnly cookies or a refresh endpoint.
- Cancel requests on unmount with `AbortController`.
""", 10, questions=["js-008", "se-002", "se-003"])),
        module("Putting it together", "Architecture and delivery.",
               lesson("Clean architecture for full-stack apps", """
Keep business rules in Domain/Application layers, implement persistence and integrations in Infrastructure, and expose DTOs from the API. Dependencies point inward, which keeps logic testable.
""", questions=["oo-016", "oo-009", "ap-005"]),
               lesson("Exercise: end-to-end feature", "Implement a feature across all layers.", 45, "Exercise",
                      exercise="Implement 'bookmark a question': a SQL table with a unique (UserId, QuestionId) index, an EF Core configuration, an application "
                               "service with validation, POST/DELETE endpoints returning proper status codes, and a React button with optimistic UI that rolls back on failure."),
               lesson("Checkpoint: full-stack assessment", "A timed assessment across the stack.", 30, "Quiz", quiz="Full-Stack .NET Assessment")),
    ], premium=True),

    path("lp-react", "React Developer Roadmap", "Components, hooks, state management, routing and performance.", "Beginner", 25, "react", [
        module("React Fundamentals", "Thinking in components.",
               lesson("JSX, props and state", """
- Props flow **down** and are read-only.
- State is local; lift it to the nearest common ancestor when shared.
- Lists need stable `key`s for reconciliation.
- Controlled inputs keep form values in state.
""", questions=["re-001", "re-002", "re-015"]),
               lesson("How rendering works", """
State change → render (pure) → reconciliation (diff by type and key) → commit (DOM updates) → effects. Rendering may be interrupted in concurrent mode; commits are synchronous.
""", questions=["re-016"])),
        module("Hooks in Depth", "useState, useEffect, useRef, useMemo and custom hooks.",
               lesson("The rules and the classic pitfalls", """
1. Call hooks at the top level only.
2. Effects capture the render's props/state — beware **stale closures**.
3. `useRef` stores mutable values without re-rendering.
4. `useMemo` caches values; `useCallback` caches function identity.
""", 15, questions=["re-003", "re-006", "re-007", "re-008"]),
               lesson("Exercise: build a useDebounce hook", "Write a reusable custom hook.", 20, "Exercise",
                      exercise="Implement `useDebounce(value, delayMs)` that returns the value only after it stops changing for delayMs. Use it in a search box that "
                               "fetches results with AbortController so outdated requests are cancelled. Write a test with fake timers.")),
        module("State, Routing and Performance", "Scaling React apps.",
               lesson("State management options", """
Local state → lifted state → context (low-frequency global data) → stores like Redux Toolkit or Zustand for complex, high-frequency state. Reducers must be pure; RTK uses Immer for safe 'mutations'.
""", questions=["re-009", "re-010"]),
               lesson("Routing and performance", """
Use `<Link>` for client-side navigation and `useParams` for route data. For performance: measure with the Profiler, memoise selectively with `React.memo`, virtualise long lists and split code with `lazy`.
""", questions=["re-011", "re-012", "re-013", "re-014"]),
               lesson("Checkpoint: React quiz", "Review React fundamentals.", 15, "Quiz", quiz="React Fundamentals")),
    ]),

    path("lp-sql", "SQL Server Developer Roadmap", "Write correct, fast T-SQL and design solid schemas.", "Intermediate", 24, "sql-server", [
        module("Querying", "Joins, subqueries and CTEs.",
               lesson("Joins without surprises", """
- INNER returns matches only; LEFT keeps all left rows.
- Filtering the right table in WHERE turns a LEFT JOIN into an INNER JOIN — put such predicates in ON.
- CROSS JOIN = Cartesian product.
""", questions=["sq-001", "sq-002", "sq-003"]),
               lesson("Subqueries, NULLs and CTEs", """
`NOT IN` with a NULL in the list returns no rows — use `NOT EXISTS`. CTEs improve readability and enable recursion but are not materialised.
""", questions=["sq-004", "sq-005", "sq-006", "sq-022"])),
        module("Analytics with Window Functions", "Ranking, running totals and top-N per group.",
               lesson("Window functions", """
`ROW_NUMBER`, `RANK`, `DENSE_RANK`, `LAG/LEAD` and aggregates with `OVER (PARTITION BY … ORDER BY … ROWS …)`. They are evaluated after WHERE, so filter on them in an outer query or CTE.
""", 15, questions=["sq-007", "sq-008", "sq-009"]),
               lesson("Exercise: top earners per department", "Solve a classic interview problem with SQL.", 20, "Exercise",
                      exercise="Solve the 'Top Earner per Department' and 'Second Highest Salary' SQL coding challenges. Then rewrite each with a different "
                               "approach (window function vs. aggregate/subquery) and compare their execution plans.")),
        module("Performance and Transactions", "Indexes, plans, isolation and locking.",
               lesson("Indexes and execution plans", """
Read actual plans; look for scans on large tables, Key Lookups, spills and estimate errors. Make predicates SARGable and add covering indexes. Beware parameter sniffing.
""", questions=["sq-012", "sq-013", "sq-014", "sq-015", "sq-019"]),
               lesson("Transactions and isolation", """
READ COMMITTED (default) prevents dirty reads; REPEATABLE READ and SERIALIZABLE prevent more anomalies with more locking. RCSI/SNAPSHOT use row versions so readers don't block writers. Handle deadlock victims (error 1205) with retries.
""", questions=["sq-016", "sq-017", "sq-018"]),
               lesson("Checkpoint: SQL Server quiz", "Validate your SQL Server knowledge.", 15, "Quiz", quiz="SQL Server Fundamentals")),
    ]),

    path("lp-javascript", "JavaScript Developer Roadmap", "The language, the event loop and the browser.", "Beginner", 22, "javascript", [
        module("Core Language", "Types, scope and functions.",
               lesson("Types and equality", """
Primitive types vs objects, `typeof null === 'object'`, floating-point precision, and why to prefer `===`. `const` prevents reassignment, not mutation.
""", questions=["js-001", "js-002", "js-003", "js-012"]),
               lesson("Scope, hoisting and closures", """
`var` is function-scoped; `let`/`const` are block-scoped with a TDZ. Closures capture variables, not values — the classic `var` loop prints 3 3 3.
""", questions=["js-004", "js-005", "js-006"])),
        module("Asynchronous JavaScript", "Promises, async/await and the event loop.",
               lesson("The event loop", """
Synchronous code → microtasks (promises) → render → next macrotask (timers, events). Long tasks block the UI.
""", 15, questions=["js-007", "js-018"]),
               lesson("Promises and async/await", """
`async` functions return promises. Use `Promise.all` for fail-fast concurrency, `allSettled` when partial failure is fine, and `try/catch` with `await` for errors.
""", questions=["js-008", "js-009"])),
        module("Objects and the Browser", "Prototypes, this and DOM performance.",
               lesson("Prototypes and this", """
Property lookup walks the prototype chain. `this` depends on the call site, except in arrow functions, which capture it lexically.
""", questions=["js-010", "js-011"]),
               lesson("Browser internals", """
Event delegation, reflow vs repaint, and storage choices (cookies vs Web Storage). Animate `transform` and `opacity`, debounce input handlers.
""", questions=["js-014", "js-015", "js-016", "js-017"]),
               lesson("Checkpoint: JavaScript quiz", "Test your JavaScript knowledge.", 15, "Quiz", quiz="JavaScript Fundamentals")),
    ]),

    path("lp-dsa", "Data Structures and Algorithms", "The patterns behind most coding interview problems.", "Intermediate", 45, "dsa", [
        module("Complexity and Arrays", "Big-O and array techniques.",
               lesson("Big-O analysis", """
Count dominant operations, drop constants. Know common classes: O(1), O(log n), O(n), O(n log n), O(n²), O(2ⁿ). Amortised analysis explains O(1) appends.
""", questions=["ds-001", "ds-002", "ds-003"]),
               lesson("Two pointers and sliding windows", """
Sorted arrays → two pointers from both ends. Contiguous subarrays → sliding window or Kadane's algorithm. These turn O(n²) brute force into O(n).
""", questions=["ds-004", "ds-005"]),
               lesson("Practice: arrays and strings", "Solve challenges to apply the techniques.", 40, "Exercise",
                      exercise="Solve 'Two Sum', 'Reverse Words', 'Valid Palindrome' and 'Sliding Window Maximum' in the coding section. For each, state the brute-force "
                               "complexity and your optimised complexity.")),
        module("Linked Structures and Trees", "Lists, stacks, queues, trees and heaps.",
               lesson("Linked lists, stacks and queues", """
Pointer manipulation (reverse, merge, cycle detection), stacks for matching/undo, queues for BFS. Know their complexities and trade-offs versus arrays.
""", questions=["ds-006", "ds-007", "ds-008", "ds-009"]),
               lesson("Trees and heaps", """
BST in-order traversal is sorted; balance matters (O(log n) vs O(n)). Heaps give O(1) min and O(log n) updates — the basis of priority queues and top-K problems.
""", questions=["ds-010", "ds-011", "ds-019"])),
        module("Graphs, Sorting and DP", "Advanced patterns.",
               lesson("Graph algorithms", """
BFS (unweighted shortest path), DFS (connectivity, cycle detection), topological sort (DAGs), Dijkstra (non-negative weights).
""", 15, questions=["ds-012", "ds-013"]),
               lesson("Sorting and dynamic programming", """
Merge sort is stable O(n log n); quicksort is fast on average but O(n²) worst case. DP needs overlapping subproblems and optimal substructure; choose memoisation or tabulation.
""", 15, questions=["ds-014", "ds-015", "ds-016", "ds-017"]),
               lesson("Checkpoint: DSA quiz", "Check your algorithmic knowledge.", 20, "Quiz", quiz="Data Structures & Algorithms Fundamentals")),
    ]),

    path("lp-system-design", "System Design", "Scalability, caching, data and reliability for system design interviews.", "Advanced", 30, "system-design", [
        module("Foundations", "A framework for any design question.",
               lesson("How to approach a design interview", """
1. Clarify requirements (functional + non-functional).
2. Estimate scale (QPS, storage, bandwidth).
3. Define APIs and the data model.
4. Draw a high-level design.
5. Deep-dive bottlenecks: caching, sharding, queues.
6. Discuss trade-offs, failure modes and monitoring.
""", 15, questions=["sd-001", "sd-002"]),
               lesson("Caching strategies", """
Cache-aside, read-through, write-through, write-behind; TTLs and invalidation; stampede protection with request coalescing and jitter.
""", questions=["sd-003", "sd-004", "sd-005"])),
        module("Data at Scale", "Replication, partitioning and consistency.",
               lesson("Replication and consistency", """
Read replicas scale reads but introduce lag. CAP: choose consistency or availability during partitions. Provide read-your-writes where users expect it.
""", questions=["sd-006", "sd-008"]),
               lesson("Partitioning", """
Shard by a well-distributed key; avoid monotonic keys that create hotspots. Consistent hashing minimises data movement when nodes change.
""", questions=["sd-007", "sd-012"])),
        module("Reliability and Practice", "Async processing, resilience and a full design.",
               lesson("Queues and resilience", """
Queues decouple producers and consumers; at-least-once delivery requires idempotent consumers. Protect dependencies with timeouts, retries with jitter, circuit breakers and rate limiting.
""", questions=["sd-009", "sd-010", "sd-011"]),
               lesson("Practice: design a URL shortener", "Apply the framework end to end.", 45, "Exercise", questions=["sd-013"],
                      exercise="Design a URL shortener for 100M links/month. Produce estimates, API definitions, ID generation strategy, storage choice, caching, "
                               "analytics pipeline and failure handling. Then compare your design with the reference answer and run it as an AI mock interview.")),
    ], premium=True),

    path("lp-senior", "Senior Software Engineer Preparation", "Design principles, architecture and leadership topics for senior interviews.", "Expert", 35, "oop-design", [
        module("Design Principles", "SOLID and patterns that senior engineers apply daily.",
               lesson("SOLID with real examples", """
SRP: one reason to change. OCP: extend via new implementations. LSP: subtypes keep the base contract. ISP: small role interfaces. DIP: depend on abstractions owned by the policy layer.
""", 15, questions=["oo-005", "oo-006", "oo-007", "oo-008", "oo-009"]),
               lesson("Patterns that matter", """
Strategy, Decorator, Factory, Observer — know when *not* to use a pattern. Prefer composition; keep abstractions earning their keep.
""", questions=["oo-010", "oo-011", "oo-012", "oo-013", "oo-014"])),
        module("Architecture", "Monoliths, microservices and boundaries.",
               lesson("Architectural styles", """
Clean/hexagonal architecture, modular monoliths, microservices with database-per-service, sagas and the outbox pattern. Choose based on team size, domain stability and operational maturity.
""", 15, questions=["oo-016", "ap-005", "ap-006", "ap-007"]),
               lesson("Exercise: architecture decision record", "Practise communicating trade-offs.", 30, "Exercise",
                      exercise="Write an Architecture Decision Record (context, decision, alternatives, consequences) for splitting a notification feature out of a "
                               "monolith. Include data ownership, failure handling, deployment and how you would measure success.")),
        module("Leading Technically", "Quality, delivery and behavioural interviews.",
               lesson("Code review, testing and delivery", """
Review for correctness, readability and risk; test business rules at the unit level and integrations at the boundaries; deliver safely with feature flags, blue-green or canary releases and backward-compatible migrations.
""", questions=["do-008", "do-006"]),
               lesson("Behavioural stories with STAR", """
Prepare 6–8 stories using **Situation, Task, Action, Result**: conflict, failure, influence without authority, mentoring, a hard technical decision and a delivery under pressure. Quantify results and reflect on what you'd change.
""", 15)),
    ], premium=True),

    path("lp-android", "Android Developer Preparation", "Kotlin, coroutines and the Android architecture questions interviewers ask.", "Intermediate", 20, "kotlin", [
        module("Kotlin Essentials", "Language features used every day on Android.",
               lesson("Null safety and data classes", """
Kotlin distinguishes nullable (`String?`) and non-null types. Use `?.`, `?:` and avoid `!!`. Data classes generate equals/hashCode/copy — ideal for immutable UI state.
""", questions=["kt-001", "kt-002", "kt-003"]),
               lesson("Coroutines and Flow", """
`suspend` functions suspend without blocking threads. Use structured concurrency (`viewModelScope`), switch to `Dispatchers.IO` for blocking work, and expose state with `StateFlow`. Cancel work tied to lifecycles.
""", 15, questions=["kt-004"])),
        module("App Architecture", "MVVM, unidirectional data flow and offline-first.",
               lesson("MVVM with Jetpack Compose", """
ViewModels expose immutable `UiState` via `StateFlow`; composables render state and send events up. Repositories combine network and Room caches. Hilt provides dependencies.
""", 15, questions=["oo-009", "oo-016"]),
               lesson("Offline-first data", """
Treat the local database as the source of truth, sync in the background with WorkManager, resolve conflicts deterministically (e.g. last-writer-wins with timestamps), and show sync status to users.
""", questions=["sd-008", "sd-009"]),
               lesson("Exercise: design an offline notes feature", "Apply MVVM and offline sync.", 30, "Exercise",
                      exercise="Design a notes feature that works offline: Room entities with a pending-sync flag, a repository that writes locally then enqueues a "
                               "WorkManager sync, a conflict rule, and a Compose screen showing sync state. Describe the tests you would write.")),
    ]),
]

write("learning-paths.json", PATHS)
