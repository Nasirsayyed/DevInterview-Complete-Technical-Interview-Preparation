from seedlib import mc, q, write

T = "aspnet-core"
H, MW, DI, API, FI, AU, MIN, SR, PERF = ("Fundamentals & Hosting", "Middleware", "Dependency Injection", "Web API", "Filters",
                                         "Authentication & Authorization", "Minimal APIs", "SignalR", "Performance Optimization")
ASP = [
    mc("an-001", T, MW, "Beginner", "Middleware execution order", "How do middleware components process a request in ASP.NET Core?",
       [("In alphabetical order of their class names", False),
        ("In the order they are added to the pipeline; the response then flows back through them in reverse order", True),
        ("All middleware runs in parallel", False), ("Only the last registered middleware runs", False)],
       "In registration order on the way in, reverse order on the way out.",
       "Each middleware receives `HttpContext` and a `next` delegate; code before `await next()` runs on the request path and code after "
       "it runs on the response path. Order is therefore significant: exception handling first, then HTTPS redirection, static files, "
       "routing, CORS, authentication, authorization and endpoints.", tags=["middleware", "pipeline"], seconds=45),

    mc("an-002", T, MW, "Intermediate", "app.Use vs app.Run", "What is the difference between `app.Use(...)` and `app.Run(...)`?",
       [("There is none", False), ("`Run` adds terminal middleware that does not call the next delegate; `Use` can call next", True),
        ("`Use` is only for static files", False), ("`Run` starts the web server", False)],
       "`Run` is terminal; `Use` can pass control to the next middleware.",
       "Middleware registered after an `app.Run` delegate is never reached. `app.Map`/`MapWhen` branch the pipeline. Short-circuiting in "
       "`Use` (not calling `next`) is valid, e.g. to return 429 early.", tags=["middleware"], seconds=45),

    mc("an-003", T, MW, "Intermediate", "Authentication before authorization",
       "Why must `UseAuthentication()` be placed before `UseAuthorization()`?",
       [("It is just a convention with no effect", False),
        ("Authorization evaluates HttpContext.User, which authentication middleware populates from the request credentials", True),
        ("Authorization middleware starts the authentication handlers itself", False),
        ("Because UseAuthorization registers the JWT handler", False)],
       "Authorization needs the authenticated principal that authentication creates.",
       "Authentication answers *who are you?* and sets `HttpContext.User`. Authorization answers *are you allowed?* by evaluating "
       "policies against that principal. Placing authorization first means every user is anonymous at evaluation time.",
       tags=["middleware", "security"], seconds=45),

    mc("an-004", T, DI, "Beginner", "Service lifetimes", "Which DI lifetime creates **one instance per HTTP request**?",
       [("Singleton", False), ("Scoped", True), ("Transient", False), ("Static", False)],
       "Scoped.",
       "Transient: a new instance every resolution. Scoped: one per scope — in ASP.NET Core, per request (DbContext is registered scoped). "
       "Singleton: one for the application lifetime; it must be thread-safe.", tags=["dependency-injection", "lifetimes"], seconds=40),

    mc("an-005", T, DI, "Advanced", "Captive dependencies", "What problem occurs when a **singleton** service depends on a **scoped** service?",
       [("Nothing; DI handles it", False),
        ("The scoped instance is captured and lives as long as the singleton, sharing per-request state across requests (scope validation throws in Development)", True),
        ("The singleton becomes scoped", False), ("The app fails to compile", False)],
       "A captive dependency: the scoped object effectively becomes a singleton.",
       "For example a singleton holding a `DbContext` would share one non-thread-safe context across concurrent requests. ASP.NET Core "
       "validates scopes in Development and throws at resolution time. Inject `IServiceScopeFactory` into the singleton and create a "
       "scope per unit of work instead.", tags=["dependency-injection"], seconds=75),

    mc("an-006", T, DI, "Intermediate", "Scoped services in a BackgroundService",
       "How should a hosted `BackgroundService` use a scoped service such as a DbContext?",
       [("Inject the DbContext directly into the constructor", False),
        ("Inject IServiceScopeFactory and create a scope for each unit of work", True),
        ("Register the DbContext as singleton", False), ("Use a static DbContext", False)],
       "Create a scope with IServiceScopeFactory per iteration.",
       "Hosted services are singletons. `using var scope = _scopeFactory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();` "
       "gives each iteration fresh scoped dependencies that are disposed afterwards.", tags=["background-service", "dependency-injection"]),

    mc("an-007", T, API, "Beginner", "[ApiController] behaviour", "Which behaviour does the `[ApiController]` attribute enable?",
       [("Automatic HTTP 400 responses when model validation fails", True), ("Automatic database migrations", False),
        ("Conventional routing by default", False), ("Server-side rendering of Razor views", False)],
       "Automatic 400 ProblemDetails responses for invalid models (plus binding-source inference and attribute routing requirement).",
       "`[ApiController]` also infers `[FromBody]` for complex parameters, requires attribute routing and returns ProblemDetails for error "
       "status codes. The automatic 400 can be customised via `ConfigureApiBehaviorOptions`.", tags=["web-api"], seconds=45),

    mc("an-008", T, API, "Intermediate", "Status code for created resources",
       "A POST successfully creates a new resource. Which response is most appropriate?",
       [("200 OK with no body", False), ("201 Created with a Location header pointing to the new resource", True),
        ("204 No Content", False), ("202 Accepted", False)],
       "201 Created with a Location header.",
       "`CreatedAtAction`/`CreatedAtRoute` return 201 with Location. Use 202 Accepted when processing continues asynchronously and 204 "
       "for successful operations with no body (e.g. DELETE).", tags=["http", "rest"], seconds=40),

    mc("an-009", T, API, "Intermediate", "Idempotent HTTP methods", "Which HTTP method is **not** idempotent by definition?",
       [("GET", False), ("PUT", False), ("DELETE", False), ("POST", True)],
       "POST.",
       "An idempotent request has the same effect when repeated: GET, HEAD, PUT and DELETE are idempotent; POST is not (two POSTs may "
       "create two orders). Make retries safe for POST with idempotency keys stored server-side.", tags=["http", "idempotency"], seconds=40),

    mc("an-010", T, FI, "Intermediate", "Filter pipeline order", "Which MVC filter type runs **first** for a request?",
       [("Action filters", False), ("Result filters", False), ("Authorization filters", True), ("Exception filters", False)],
       "Authorization filters.",
       "Order: authorization → resource → (model binding) → action → action execution → result filters. Exception filters handle "
       "unhandled exceptions from earlier stages. Filters run inside the endpoint, after middleware.", tags=["filters", "mvc"], seconds=45),

    mc("an-011", T, FI, "Advanced", "Exception filters vs exception middleware",
       "Why is global exception-handling **middleware** usually preferred over an MVC exception filter?",
       [("Filters are slower", False),
        ("Exception filters only catch exceptions thrown inside MVC (actions/filters), while middleware also covers routing, other middleware and minimal APIs", True),
        ("Middleware can change the status code but filters cannot", False), ("Exception filters are obsolete", False)],
       "Middleware sees the whole pipeline; filters see only MVC execution.",
       "Use `UseExceptionHandler`, `IExceptionHandler` (.NET 8) or custom middleware to map exceptions to consistent responses for every "
       "endpoint type, and log once with a correlation id.", tags=["filters", "error-handling"], seconds=60),

    mc("an-012", T, AU, "Beginner", "Authentication vs authorization", "Which statement is correct?",
       [("They are synonyms", False), ("Authentication verifies identity; authorization decides what that identity may do", True),
        ("Authorization happens before authentication", False), ("Authentication is only needed for admin users", False)],
       "AuthN = who you are; AuthZ = what you may do.",
       "In ASP.NET Core, authentication handlers (JWT bearer, cookies) build a `ClaimsPrincipal`; authorization policies, roles and "
       "requirements evaluate it. A failed authentication yields 401; an authenticated but forbidden request yields 403.",
       tags=["security"], seconds=35),

    mc("an-013", T, AU, "Advanced", "Short-lived JWTs", "Why should JWT access tokens usually be short-lived (minutes, not days)?",
       [("JWTs become invalid JSON after a few minutes", False),
        ("Self-contained tokens are validated without a server lookup, so they cannot easily be revoked before they expire", True),
        ("Long tokens slow down HTTPS", False), ("Browsers delete JWTs after 15 minutes", False)],
       "They cannot be revoked easily; short lifetimes limit the damage of a leak.",
       "Pair short-lived access tokens with refresh tokens that are stored server-side (hashed), rotated on each use and revocable. "
       "Detect refresh-token reuse and revoke the token family.", tags=["jwt", "refresh-tokens"], seconds=60),

    mc("an-014", T, AU, "Intermediate", "Policy-based authorization",
       "How do you require that a user has **both** the `Admin` role **and** an `email_verified` claim on an endpoint?",
       [("Add [Authorize(Roles = \"Admin,email_verified\")]", False, "A comma-separated role list means any one of the roles."),
        ("Register a policy with RequireRole(\"Admin\") and RequireClaim(\"email_verified\") and apply [Authorize(Policy = \"...\")]", True),
        ("Check the header manually in every action", False), ("Use [AllowAnonymous]", False)],
       "Define a policy combining requirements and reference it by name.",
       "Policies compose requirements (roles, claims, custom `IAuthorizationRequirement` handlers). Stacking two `[Authorize]` attributes "
       "also requires both, but named policies keep rules central and testable.", tags=["authorization", "policies"]),

    mc("an-015", T, MIN, "Beginner", "Minimal API endpoints", "Which call maps an HTTP GET endpoint in a minimal API?",
       [("app.UseGet(\"/todos\", ...)", False), ("app.MapGet(\"/todos\", ...)", True),
        ("app.Get(\"/todos\", ...)", False), ("services.AddGet(\"/todos\", ...)", False)],
       "`app.MapGet`.",
       "Minimal APIs use `MapGet/MapPost/MapPut/MapDelete` with lambda handlers whose parameters are bound from route, query, body and DI. "
       "`MapGroup` shares prefixes and filters; `TypedResults` improves OpenAPI metadata.", tags=["minimal-apis"], seconds=35),

    mc("an-016", T, SR, "Intermediate", "SignalR transports", "Which transport does SignalR prefer when both client and server support it?",
       [("Long polling", False), ("Server-Sent Events", False), ("WebSockets", True), ("gRPC", False)],
       "WebSockets, falling back to Server-Sent Events, then long polling.",
       "SignalR negotiates the best available transport. Scaling out across servers requires a backplane (Redis or Azure SignalR "
       "Service) or sticky sessions so that messages reach clients connected to other instances.", tags=["signalr", "realtime"], seconds=40),

    mc("an-017", T, PERF, "Intermediate", "IHttpClientFactory", "Why prefer `IHttpClientFactory` over creating `new HttpClient()` per request?",
       [("It makes requests run synchronously", False),
        ("It pools message handlers to avoid socket exhaustion while still honouring DNS changes through handler rotation", True),
        ("HttpClient cannot send JSON", False), ("It disables TLS", False)],
       "Handler pooling avoids socket exhaustion and stale DNS.",
       "Disposing HttpClient per request leaves sockets in TIME_WAIT; a single static client never sees DNS changes. The factory "
       "(or a singleton with `SocketsHttpHandler.PooledConnectionLifetime`) solves both, and integrates resilience pipelines.",
       tags=["httpclient", "performance"]),

    mc("an-018", T, H, "Beginner", "Application startup", "In .NET 6+ project templates, where are services and the request pipeline configured?",
       [("Global.asax", False), ("Program.cs using WebApplication.CreateBuilder", True), ("web.config", False), ("Startup.xml", False)],
       "Program.cs with the minimal hosting model.",
       "`WebApplication.CreateBuilder(args)` sets up configuration, logging and DI (`builder.Services`); `builder.Build()` returns the app "
       "where middleware and endpoints are mapped before `app.Run()`.", tags=["hosting"], seconds=30),

    mc("an-019", T, H, "Intermediate", "Configuration precedence",
       "With the default `WebApplication.CreateBuilder` configuration, a key is set in appsettings.json, appsettings.Production.json and as an environment variable. Which value wins in Production?",
       [("appsettings.json", False), ("appsettings.Production.json", False), ("The environment variable", True), ("The first one read", False)],
       "The environment variable (only command-line arguments rank higher).",
       "Later providers override earlier ones: appsettings.json → appsettings.{Environment}.json → user secrets (Development) → "
       "environment variables → command-line args. Use `__` for nested keys in environment variables, e.g. `Jwt__SigningKey`.",
       tags=["configuration"], seconds=50),

    q("an-020", T, PERF, "ScenarioBased", "Advanced", "Investigating rising latency",
      "After a traffic increase, an ASP.NET Core API's p95 latency triples while CPU stays at 35%. How would you investigate and what are likely causes?",
      answer="Low CPU with high latency suggests waiting: thread-pool starvation from blocking calls, slow DB queries/connection-pool "
             "exhaustion, or downstream latency. Use traces, metrics and dotnet-counters to find it.",
      explanation="1. Check `dotnet-counters` for thread-pool queue length and thread count (starvation from `.Result`/`.Wait()`).\n"
                  "2. Inspect DB: slow-query logs, missing indexes, connection-pool exhaustion (pool timeouts), lock waits.\n"
                  "3. Distributed tracing (OpenTelemetry) to see which dependency dominates the span time.\n"
                  "4. Look for synchronous I/O, large allocations/GC pauses (`gc-heap-size`, % time in GC).\n"
                  "5. Fix: async all the way, caching, query tuning, bulkheads/timeouts on dependencies, then load test to confirm.",
      reference="High latency with low CPU indicates threads waiting rather than computing. Measure thread-pool queue length "
                "(starvation from sync-over-async), database wait times and connection pool usage, downstream dependency latency via "
                "distributed tracing, and GC pauses. Remediate with async I/O, indexing/caching, timeouts and bulkheads, and verify with load tests.",
      tags=["performance", "diagnostics"], seconds=300, premium=True),
]

T2 = "ef-core"
EF = [
    mc("ef-001", T2, "EF Core Fundamentals", "Beginner", "Purpose of migrations", "What do EF Core migrations provide?",
       [("Automatic backups", False), ("Versioned, incremental schema changes generated from the model and applied to the database", True),
        ("Data replication", False), ("Query caching", False)],
       "A versioned history of schema changes derived from the model.",
       "`dotnet ef migrations add Name` compares the model with the last snapshot and generates Up/Down code. Apply with "
       "`dotnet ef database update`, `Database.MigrateAsync()`, or generate idempotent SQL scripts for production pipelines.",
       tags=["migrations"], seconds=40),

    mc("ef-002", T2, "Querying & Performance", "Intermediate", "The N+1 query problem",
       "A page lists 50 orders and, for each one, accesses `order.Customer.Name` with lazy loading enabled. What happens?",
       [("One query loads everything", False), ("1 query for the orders plus up to 50 additional queries for customers (N+1)", True),
        ("The code throws", False), ("EF caches customers automatically, so no extra queries", False)],
       "N+1 queries; fix with Include or a projection.",
       "Use eager loading (`Include(o => o.Customer)`) or, better, project only the needed columns: "
       "`.Select(o => new { o.Id, CustomerName = o.Customer.Name })`, which becomes a single JOIN.", tags=["n-plus-one", "performance"]),

    mc("ef-003", T2, "Querying & Performance", "Intermediate", "AsNoTracking", "When should you use `AsNoTracking()`?",
       [("Always, for every query", False), ("For read-only queries whose results will not be updated via SaveChanges", True),
        ("Only for inserts", False), ("To enable lazy loading", False)],
       "For read-only queries.",
       "Tracking stores snapshots for change detection, which costs memory and CPU. Read-only endpoints benefit from `AsNoTracking()` "
       "or projections to DTOs (which are never tracked).", tags=["change-tracking"], seconds=40),

    mc("ef-004", T2, "Querying & Performance", "Advanced", "Untranslatable predicates",
       "What does EF Core 3.0+ do when a `Where` predicate calls a C# method it cannot translate to SQL?",
       [("Silently loads the whole table and filters in memory", False, "That was EF Core 2.x client evaluation behaviour."),
        ("Throws an InvalidOperationException explaining the expression could not be translated", True),
        ("Ignores the predicate", False), ("Translates it to a stored procedure", False)],
       "It throws a translation exception.",
       "Since 3.0, client evaluation is only allowed in the final projection. Rewrite the predicate with translatable expressions, or "
       "explicitly switch to client evaluation with `AsEnumerable()` after reducing the data set.", tags=["linq", "translation"], seconds=60),

    mc("ef-005", T2, "EF Core Fundamentals", "Intermediate", "DbContext lifetime",
       "Which statement about `DbContext` in ASP.NET Core is correct?",
       [("It is thread-safe and should be a singleton", False),
        ("It is not thread-safe; register it scoped (per request) and do not run parallel operations on one instance", True),
        ("It must be created with new in every method", False), ("It can only be used synchronously", False)],
       "Scoped and not thread-safe.",
       "`AddDbContext` registers it as scoped. Starting two queries concurrently on the same context throws. Use `IDbContextFactory` "
       "for background jobs or Blazor Server, and `AddDbContextPool` to reduce allocation overhead.", tags=["dbcontext"], seconds=45),

    mc("ef-006", T2, "ADO.NET", "Intermediate", "Preventing SQL injection in ADO.NET",
       "How should user input be included in an ADO.NET `SqlCommand`?",
       [("Concatenate it after escaping single quotes", False), ("Use SqlParameter objects (e.g. @email) instead of string concatenation", True),
        ("Base64-encode it", False), ("Wrap it in square brackets", False)],
       "Use parameters.",
       "Parameters are sent separately from the SQL text, so input is never interpreted as SQL, and plans can be reused. Dapper and EF Core "
       "parameterise automatically (EF's `FromSqlInterpolated`/`SqlQuery` convert interpolated values into parameters).",
       tags=["ado-net", "sql-injection"], seconds=40),
]

write("questions/aspnetcore.json", ASP)
write("questions/efcore.json", EF)
