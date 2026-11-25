from seedlib import mc, q, write

T = "dsa"
CX, AR, LL, SQ, TG, SS, DP, HS = ("Complexity Analysis", "Arrays & Strings", "Linked Lists", "Stacks & Queues", "Trees & Graphs",
                                  "Sorting & Searching", "Dynamic Programming", "Hashing")
DSA = [
    mc("ds-001", T, CX, "Beginner", "Binary search complexity", "What is the time complexity of binary search on a sorted array of n elements?",
       [("O(n)", False), ("O(log n)", True), ("O(n log n)", False), ("O(1)", False)], "O(log n).",
       "Each comparison halves the search space, so at most ⌊log₂ n⌋ + 1 comparisons are needed. It requires random access and sorted data.",
       tags=["binary-search", "big-o"], seconds=30),
    mc("ds-002", T, CX, "Intermediate", "Nested loop complexity", "What is the time complexity of this code?",
       [("O(n)", False), ("O(n log n)", False), ("O(n²)", True), ("O(2ⁿ)", False)], "O(n²).",
       "The inner loop runs 0 + 1 + … + (n−1) = n(n−1)/2 times, which is Θ(n²) — constant factors and lower-order terms are dropped.",
       code="for (int i = 0; i < n; i++)\n    for (int j = 0; j < i; j++)\n        count++;", lang="csharp", tags=["big-o"], seconds=40),
    mc("ds-003", T, CX, "Intermediate", "Amortised dynamic array append", "Why is appending to a dynamic array (List<T>, ArrayList) O(1) amortised?",
       [("Arrays never resize", False), ("Capacity grows geometrically (e.g. doubles), so the occasional O(n) copy is spread over many cheap appends", True),
        ("Each append allocates a new array", False), ("Because of hashing", False)],
       "Geometric growth makes total copying cost O(n) for n appends.",
       "Doubling means each element is copied a constant number of times on average. Pre-size the list when the final size is known to avoid reallocations.",
       tags=["amortized-analysis"], seconds=45),
    mc("ds-004", T, AR, "Intermediate", "Pair sum in a sorted array", "What is the most efficient way to find whether two numbers in a **sorted** array sum to a target?",
       [("Check every pair: O(n²)", False), ("Two pointers from both ends moving inward: O(n) time, O(1) space", True),
        ("Sort again then binary search each: O(n² log n)", False), ("Recursion over all subsets", False)],
       "Two pointers.", "If the sum is too small move the left pointer right, if too large move the right pointer left. For unsorted input "
       "use a hash set of complements in O(n) time and space.", tags=["two-pointers"], seconds=45),
    mc("ds-005", T, AR, "Intermediate", "Maximum subarray", "Which algorithm finds the maximum-sum contiguous subarray in O(n)?",
       [("Dijkstra's algorithm", False), ("Kadane's algorithm", True), ("Merge sort", False), ("Floyd–Warshall", False)],
       "Kadane's algorithm.", "Track `best = max(best, current = max(x, current + x))` in one pass. Handle all-negative arrays by initialising "
       "with the first element.", tags=["kadane", "arrays"], seconds=40),
    mc("ds-006", T, LL, "Intermediate", "Cycle detection in a linked list", "How can you detect a cycle in a singly linked list using O(1) extra space?",
       [("Store visited nodes in a hash set", False, "Works, but uses O(n) space."), ("Floyd's tortoise and hare: move one pointer by 1 and another by 2; they meet if there is a cycle", True),
        ("Reverse the list twice", False), ("Count nodes until null", False)],
       "Fast and slow pointers.", "If fast reaches null there is no cycle. After they meet, resetting one pointer to head and advancing both "
       "by one finds the cycle start.", tags=["linked-list", "two-pointers"], seconds=50),
    mc("ds-007", T, LL, "Beginner", "Indexed access in a linked list", "What is the time complexity of accessing the k-th element of a singly linked list?",
       [("O(1)", False), ("O(k), i.e. O(n) in the worst case", True), ("O(log n)", False), ("O(n²)", False)],
       "O(n) — you must walk from the head.", "Linked lists trade indexed access for O(1) insertion/removal at a known node. Arrays offer O(1) "
       "indexing and better cache locality, which often makes them faster in practice.", tags=["linked-list"], seconds=30),
    mc("ds-008", T, SQ, "Beginner", "Balanced brackets", "Which data structure is the natural choice to check whether brackets like `({[]})` are balanced?",
       [("Queue", False), ("Stack", True), ("Heap", False), ("Hash map only", False)],
       "A stack.", "Push opening brackets; on a closing bracket pop and verify it matches. The string is balanced if the stack is empty at the "
       "end. O(n) time.", tags=["stack"], seconds=30),
    mc("ds-009", T, SQ, "Beginner", "BFS data structure", "Breadth-first search uses which data structure to decide the next node to visit?",
       [("Stack", False, "A stack gives depth-first order."), ("Queue", True), ("Priority queue", False, "Used by Dijkstra."), ("Set", False)],
       "A FIFO queue.", "BFS visits nodes level by level, so on unweighted graphs it finds shortest paths in number of edges. Mark nodes visited "
       "when enqueued to avoid duplicates.", tags=["bfs", "queue"], seconds=30),
    mc("ds-010", T, TG, "Intermediate", "In-order traversal of a BST", "What order does an in-order traversal of a binary search tree produce?",
       [("Level order", False), ("Ascending (sorted) order of keys", True), ("Reverse insertion order", False), ("Random order", False)],
       "Sorted order.", "In-order visits left subtree, node, right subtree. This property is used to validate BSTs and to find the k-th smallest element.",
       tags=["bst", "traversal"], seconds=30),
    mc("ds-011", T, TG, "Intermediate", "Unbalanced BST worst case", "What is the worst-case search time in an **unbalanced** binary search tree with n nodes?",
       [("O(1)", False), ("O(log n)", False), ("O(n)", True), ("O(n log n)", False)],
       "O(n) — inserting sorted keys produces a linked-list-shaped tree.", "Self-balancing trees (AVL, red-black) guarantee O(log n) height; "
       "SortedDictionary/TreeMap use red-black trees.", tags=["bst", "balancing"], seconds=35),
    mc("ds-012", T, TG, "Advanced", "Dijkstra's limitation", "When does Dijkstra's algorithm fail to find correct shortest paths?",
       [("On directed graphs", False), ("When some edges have negative weights", True), ("On graphs with cycles", False), ("On sparse graphs", False)],
       "Negative edge weights.", "Dijkstra finalises a node when it is popped from the priority queue, assuming distances never decrease later. "
       "Use Bellman–Ford for negative weights (it also detects negative cycles).", tags=["graphs", "shortest-path"], seconds=50),
    mc("ds-013", T, TG, "Intermediate", "Topological sort", "Topological sorting is only possible for which kind of graph?",
       [("Undirected graphs", False), ("Directed acyclic graphs (DAGs)", True), ("Complete graphs", False), ("Weighted graphs", False)],
       "DAGs.", "Kahn's algorithm repeatedly removes nodes with in-degree 0; if nodes remain, there is a cycle. Applications: build systems, "
       "course prerequisites, task scheduling.", tags=["graphs", "topological-sort"], seconds=35),
    mc("ds-014", T, SS, "Intermediate", "Stable O(n log n) sorting", "Which algorithm is stable **and** guarantees O(n log n) in the worst case?",
       [("Quicksort", False), ("Heap sort", False, "Heap sort is O(n log n) but not stable."), ("Merge sort", True), ("Selection sort", False)],
       "Merge sort.", "Stability keeps equal keys in their original order — important when sorting by multiple criteria. Merge sort needs O(n) "
       "extra space for arrays.", tags=["sorting", "stability"], seconds=40),
    mc("ds-015", T, SS, "Intermediate", "Quicksort worst case", "What is quicksort's worst-case time complexity and when does it occur?",
       [("O(n log n), never worse", False), ("O(n²), when pivots repeatedly split the array very unevenly (e.g. first-element pivot on sorted input)", True),
        ("O(n), on sorted input", False), ("O(log n)", False)],
       "O(n²) with consistently bad pivots.", "Randomised or median-of-three pivots make this unlikely. Introsort (used by .NET's Array.Sort "
       "and C++ std::sort) switches to heap sort when recursion gets too deep.", tags=["quicksort"], seconds=45),
    mc("ds-016", T, DP, "Intermediate", "When dynamic programming applies", "Dynamic programming is applicable when a problem has…",
       [("only independent subproblems", False), ("overlapping subproblems and optimal substructure", True), ("no recursive structure", False), ("random input", False)],
       "Overlapping subproblems + optimal substructure.", "Divide and conquer (e.g. merge sort) has independent subproblems, so caching gives "
       "nothing. Classic DP problems: Fibonacci, knapsack, edit distance, longest common subsequence.", tags=["dynamic-programming"], seconds=40),
    mc("ds-017", T, DP, "Advanced", "Memoisation vs tabulation", "What distinguishes tabulation from memoisation?",
       [("Tabulation is top-down recursion with a cache", False), ("Tabulation fills a table bottom-up iteratively; memoisation caches results of top-down recursive calls", True),
        ("Only memoisation is DP", False), ("Tabulation always uses less memory", False)],
       "Bottom-up table vs top-down cached recursion.", "Tabulation avoids recursion depth limits and often allows rolling-array space "
       "optimisation; memoisation computes only the states actually reached.", tags=["dynamic-programming"], seconds=45),
    mc("ds-018", T, HS, "Intermediate", "Hash table degradation", "What makes hash table operations degrade towards O(n)?",
       [("Using string keys", False), ("Many keys hashing to the same bucket (poor hash function or adversarial input)", True),
        ("Removing items", False), ("Iterating the table", False)],
       "Collisions concentrating keys in few buckets.", "Good hash distribution and resizing keep the load factor low. Mutable keys whose hash "
       "changes after insertion become unreachable — make dictionary keys immutable.", tags=["hashing"], seconds=40),
    mc("ds-019", T, TG, "Intermediate", "Fast minimum access", "Which structure offers O(1) access to the minimum element and O(log n) insertion and removal?",
       [("Sorted array", False, "Insertion is O(n)."), ("Binary min-heap", True), ("Linked list", False), ("Hash set", False)],
       "A binary min-heap (priority queue).", ".NET 6+ provides `PriorityQueue<TElement, TPriority>`. Heaps power Dijkstra, scheduling and "
       "'top K' problems.", tags=["heap", "priority-queue"], seconds=35),
]

T = "system-design"
SC, CA, DS, MQ, RL = ("Scalability", "Caching", "Data & Storage", "Messaging & Async", "Reliability")
SD = [
    mc("sd-001", T, SC, "Beginner", "Horizontal vs vertical scaling", "What is horizontal scaling?",
       [("Adding more CPU and RAM to one server", False, "That is vertical scaling."), ("Adding more server instances and distributing load across them", True),
        ("Making the UI wider", False), ("Compressing data", False)],
       "Scale out by adding instances.", "Horizontal scaling improves availability and has no single-machine ceiling, but needs load balancing and "
       "stateless or shared-state services.", tags=["scalability"], seconds=30),
    mc("sd-002", T, SC, "Intermediate", "Stateless services", "Why do stateless application servers make horizontal scaling easier?",
       [("They need no database", False), ("Any instance can handle any request, so load balancers can route freely and instances can be added or removed safely", True),
        ("They use less CPU", False), ("They never fail", False)],
       "No session affinity required.", "Move session/state to a shared store (Redis, database) or into signed tokens. Sticky sessions are a "
       "workaround that hurts balancing and failover.", tags=["stateless"], seconds=40),
    mc("sd-003", T, CA, "Intermediate", "Cache-aside", "In the cache-aside pattern, what happens on a cache miss?",
       [("The cache loads data from the database by itself", False, "That is read-through."), ("The application reads from the database, then writes the result into the cache", True),
        ("The request fails", False), ("The database pushes data to the cache", False)],
       "The application loads from the source and populates the cache.", "On writes, update the database then invalidate (delete) the cache "
       "entry. Combine with TTLs to bound staleness.", tags=["caching", "patterns"], seconds=40),
    mc("sd-004", T, CA, "Intermediate", "Write-through caching", "What characterises a write-through cache?",
       [("Writes go only to the cache and are flushed later", False, "That is write-behind/write-back."), ("Each write updates the cache and the backing store synchronously", True),
        ("The cache is never written", False), ("Writes bypass the cache entirely", False, "That is write-around.")],
       "Synchronous writes to both cache and store.", "Write-through keeps the cache fresh at the cost of write latency; write-behind is faster "
       "but risks data loss if the cache fails before flushing.", tags=["caching"], seconds=40),
    mc("sd-005", T, CA, "Advanced", "Cache stampede", "A hot cache key expires and thousands of concurrent requests hit the database at once. Which mitigation helps most directly?",
       [("Disable caching", False), ("Request coalescing / single-flight locking so one request rebuilds the value while others wait or serve stale data", True),
        ("Add more web servers", False), ("Use shorter TTLs", False)],
       "Coalesce rebuilds (and/or serve stale while revalidating, add TTL jitter).", "Other tools: probabilistic early expiration, background "
       "refresh of hot keys, and jittered TTLs to avoid synchronised expirations.", tags=["caching", "thundering-herd"], seconds=60),
    mc("sd-006", T, DS, "Intermediate", "CAP theorem", "According to the CAP theorem, what must a distributed data store choose during a network partition?",
       [("Between latency and throughput", False), ("Between consistency and availability", True), ("Between SQL and NoSQL", False), ("Between reads and writes", False)],
       "Consistency vs availability under partition.", "Partitions are unavoidable, so the real choice is CP (refuse some requests to stay "
       "consistent) or AP (answer with possibly stale data). PACELC adds the latency/consistency trade-off when there is no partition.",
       tags=["cap-theorem", "consistency"], seconds=45),
    mc("sd-007", T, DS, "Intermediate", "Sharding hotspots", "What is a common risk of sharding data by a monotonically increasing key such as a timestamp?",
       [("Data becomes unsortable", False), ("All new writes land on the same shard, creating a hotspot", True), ("Joins become faster", False), ("No risk", False)],
       "Write hotspots on the newest shard.", "Prefer hashed keys or composite keys (tenant + time) for even distribution; use range sharding "
       "only when range scans dominate and load is known.", tags=["sharding"], seconds=45),
    mc("sd-008", T, DS, "Intermediate", "Read replicas", "A user updates their profile and immediately reloads it from a read replica, seeing old data. Why?",
       [("Replicas never update", False), ("Asynchronous replication lag: the replica has not applied the write yet", True), ("Browser caching only", False), ("Index corruption", False)],
       "Replication lag (eventual consistency).", "Provide read-your-writes consistency by routing a user's reads to the primary for a short "
       "window after writes, or by tracking replication position.", tags=["replication", "consistency"], seconds=45),
    mc("sd-009", T, MQ, "Intermediate", "At-least-once delivery", "A message queue guarantees at-least-once delivery. What must consumers do?",
       [("Nothing", False), ("Be idempotent, because the same message may be delivered more than once", True), ("Process messages in parallel only", False), ("Delete the queue", False)],
       "Make consumers idempotent.", "Deduplicate with message ids stored alongside the effect (or idempotent upserts). Use dead-letter queues "
       "for poison messages and the outbox pattern to publish reliably with database changes.", tags=["queues", "idempotency"], seconds=45),
    mc("sd-010", T, RL, "Intermediate", "Circuit breaker", "What does the circuit breaker pattern do?",
       [("Retries forever", False), ("Stops calling a failing dependency for a period after repeated failures, failing fast and allowing it to recover", True),
        ("Encrypts traffic", False), ("Balances load", False)],
       "Fail fast after repeated failures, then probe (half-open).", "States: closed → open (fail fast) → half-open (trial calls). Combine with "
       "timeouts, retries with exponential backoff + jitter, and fallbacks.", tags=["resilience"], seconds=40),
    mc("sd-011", T, RL, "Intermediate", "Token bucket rate limiting", "How does a token bucket rate limiter work?",
       [("It rejects every request after the first", False), ("Tokens are added at a fixed rate up to a capacity; each request consumes a token, so short bursts are allowed while the average rate is capped", True),
        ("It queues requests forever", False), ("It counts requests per day only", False)],
       "Refill at rate r, burst up to capacity b.", "Return HTTP 429 with Retry-After when empty. In a distributed system keep counters in a "
       "shared store (e.g. Redis) or enforce limits at the API gateway.", tags=["rate-limiting"], seconds=45),
    mc("sd-012", T, DS, "Advanced", "Consistent hashing", "Why is consistent hashing used for distributing keys across cache nodes?",
       [("It encrypts keys", False), ("When a node is added or removed only about 1/n of keys move, instead of almost all keys with hash mod n", True),
        ("It guarantees strong consistency", False), ("It sorts keys", False)],
       "Minimal key remapping on membership changes.", "Virtual nodes smooth the distribution across physical servers. Used by Dynamo-style "
       "stores, distributed caches and load balancers.", tags=["consistent-hashing"], seconds=50),
    q("sd-013", T, SC, "ScenarioBased", "Advanced", "Design a URL shortener",
      "Design a URL shortener handling 100M new links per month and 10:1 read/write ratio. Cover the API, ID generation, storage, caching and scaling.",
      answer="POST /links returns a short code; GET /{code} redirects (301/302). Generate base62 codes from a distributed ID or random 7-char codes with collision checks, store in a key-value store, cache hot codes, and scale reads with CDN/cache.",
      explanation="**Scale:** ~40 writes/s, ~400 reads/s average (plan for 10× peaks); 100M × 12 × ~500 bytes ≈ 600 GB/year.\n"
                  "**API:** `POST /api/links {url, customAlias?, expiresAt?}` → `{code}`; `GET /{code}` → 301 (cacheable) or 302 (to count clicks).\n"
                  "**IDs:** base62 of a 64-bit unique id (Snowflake-style or pre-allocated ranges per server) gives 7–8 chars without "
                  "collisions; random codes need a uniqueness check.\n**Storage:** key-value or document store keyed by code (partition by "
                  "code hash); replicate for availability.\n**Caching:** Redis/CDN for hot codes (reads dominate).\n**Analytics:** emit click "
                  "events to a queue and aggregate asynchronously so redirects stay fast.\n**Abuse:** rate limiting, malicious URL scanning, expiration.",
      reference="Estimate load and storage; define create/redirect APIs; generate collision-free short codes (base62 of unique ids or "
                "random with checks); store mappings in a partitioned, replicated key-value store; cache hot mappings and use 301/302 "
                "deliberately; process analytics asynchronously; add rate limiting, abuse detection and expiry.",
      tags=["design-problem"], seconds=900, premium=True),
]

T = "oop-design"
OC, SO, DPN, ARCH = ("OOP Concepts", "SOLID Principles", "Design Patterns", "Software Architecture")
OOP = [
    mc("oo-001", T, OC, "Beginner", "Encapsulation", "Which best describes encapsulation?",
       [("Inheriting behaviour from a base class", False), ("Bundling data with the methods that operate on it and restricting direct access to internal state", True),
        ("Having many forms of a method", False), ("Splitting code into files", False)],
       "Hide internal state behind a controlled interface.", "Encapsulation lets an object protect its invariants (e.g. a BankAccount balance can "
       "only change through Deposit/Withdraw that validate amounts).", tags=["encapsulation"], seconds=30),
    mc("oo-002", T, OC, "Beginner", "Polymorphism", "What is runtime (subtype) polymorphism?",
       [("Defining two methods with the same name and different parameters", False, "That is overloading (compile-time)."),
        ("Calling a method through a base type reference and having the derived type's implementation execute", True),
        ("Copying objects", False), ("Using generics", False)],
       "Dynamic dispatch through a common base type or interface.", "It lets code depend on abstractions (`IShape.Area()`) while behaviour "
       "varies by concrete type, which is the foundation of the Open/Closed principle.", tags=["polymorphism"], seconds=35),
    mc("oo-003", T, OC, "Intermediate", "Composition over inheritance", "Why is composition often preferred over inheritance?",
       [("Inheritance is not supported in modern languages", False),
        ("It assembles behaviour from small, replaceable collaborators, avoiding fragile deep hierarchies and tight coupling to base-class internals", True),
        ("Composition is always faster", False), ("Inheritance prevents polymorphism", False)],
       "Looser coupling and more flexibility.", "Inheritance models a stable 'is-a' relationship; composition ('has-a') lets behaviour change at "
       "runtime and supports dependency injection and testing with fakes.", tags=["composition"], seconds=45),
    mc("oo-004", T, OC, "Intermediate", "Overloading vs overriding", "Which statement is correct?",
       [("Overriding is resolved at compile time", False),
        ("Overloading chooses among same-named methods by parameter list at compile time; overriding replaces a virtual method's implementation, resolved at runtime", True),
        ("Both require the virtual keyword", False), ("Overloading changes the return type only", False)],
       "Overloading = compile time; overriding = runtime.", "Overload resolution uses the static types of arguments; overriding uses the "
       "runtime type of the receiver.", tags=["overloading", "overriding"], seconds=40),
    mc("oo-005", T, SO, "Beginner", "Single Responsibility Principle", "What does the Single Responsibility Principle state?",
       [("A class should have only one method", False), ("A module should have one reason to change — it should serve one actor/responsibility", True),
        ("Each project should have one class", False), ("Functions must be under ten lines", False)],
       "One reason to change.", "An `InvoiceService` that calculates totals, renders PDFs and sends emails changes for three different reasons; "
       "split it into collaborators.", tags=["solid", "srp"], seconds=35),
    mc("oo-006", T, SO, "Intermediate", "Open/Closed Principle", "Which design follows the Open/Closed Principle for adding new payment methods?",
       [("A switch statement in PaymentService that grows with every new method", False),
        ("An IPaymentProcessor interface with one implementation per method, selected at runtime, so new methods are added as new classes", True),
        ("Copying PaymentService for each method", False), ("Storing payment logic in the database", False)],
       "Extend with new implementations instead of editing existing code.", "Open for extension, closed for modification: polymorphism "
       "(strategy pattern) and DI registration let you add behaviour without touching tested code.", tags=["solid", "ocp"], seconds=50),
    mc("oo-007", T, SO, "Intermediate", "Liskov Substitution Principle", "Which is a classic LSP violation?",
       [("A Dog class inheriting from Animal", False),
        ("A Square inheriting from Rectangle where setting Width also changes Height, breaking code that expects independent sides", True),
        ("An interface with two implementations", False), ("A sealed class", False)],
       "Square/Rectangle — the subtype breaks base-type expectations.", "Subtypes must honour the base contract: no stronger preconditions, "
       "no weaker postconditions, no surprising exceptions (e.g. throwing NotSupportedException from an inherited method).",
       tags=["solid", "lsp"], seconds=50),
    mc("oo-008", T, SO, "Intermediate", "Interface Segregation Principle", "What does the Interface Segregation Principle recommend?",
       [("One large interface per application", False), ("Clients should not be forced to depend on methods they do not use; prefer small, role-specific interfaces", True),
        ("Avoid interfaces", False), ("Every class implements every interface", False)],
       "Small, focused interfaces.", "A `IMultiFunctionDevice` with Print/Scan/Fax forces a simple printer to stub methods; split into "
       "IPrinter, IScanner, IFax.", tags=["solid", "isp"], seconds=40),
    mc("oo-009", T, SO, "Intermediate", "Dependency Inversion Principle", "What does the Dependency Inversion Principle state?",
       [("Low-level modules should depend on high-level modules", False),
        ("High-level policy should depend on abstractions, and low-level details should implement those abstractions", True),
        ("Always use a DI container", False), ("Never use interfaces", False)],
       "Depend on abstractions, not concretions.", "DIP is the principle; dependency injection is a technique to supply implementations. "
       "In clean architecture the application layer defines interfaces (e.g. IEmailSender) that infrastructure implements.",
       tags=["solid", "dip"], seconds=40),
    mc("oo-010", T, DPN, "Beginner", "Singleton pattern", "Which pattern ensures a class has a single instance with a global access point?",
       [("Factory", False), ("Singleton", True), ("Observer", False), ("Adapter", False)],
       "Singleton.", "In .NET prefer registering a service with a singleton lifetime in DI over a static Instance property: it stays "
       "testable and the lifetime is explicit. Singletons must be thread-safe.", tags=["singleton"], seconds=30),
    mc("oo-011", T, DPN, "Intermediate", "Strategy pattern", "Which pattern encapsulates interchangeable algorithms behind a common interface?",
       [("Strategy", True), ("Decorator", False), ("Singleton", False), ("Facade", False)],
       "Strategy.", "Example: IShippingCostStrategy with Standard/Express/Free implementations chosen at runtime. It removes conditional "
       "logic and supports the Open/Closed principle.", tags=["strategy"], seconds=30),
    mc("oo-012", T, DPN, "Intermediate", "Observer pattern", "In C#, which language feature directly implements the Observer pattern?",
       [("Events and delegates", True), ("Attributes", False), ("Generics", False), ("Partial classes", False)],
       "Events (and IObservable<T>/Rx).", "Subjects notify subscribers without knowing their concrete types. Unsubscribe to avoid memory "
       "leaks with long-lived publishers.", tags=["observer", "events"], seconds=30),
    mc("oo-013", T, DPN, "Intermediate", "Decorator pattern", "Which pattern adds behaviour to an object by wrapping it with another object implementing the same interface?",
       [("Decorator", True), ("Builder", False), ("Prototype", False), ("Bridge", False)],
       "Decorator.", "Examples: `BufferedStream` wrapping `FileStream`, a `CachingRepository` wrapping a repository, or HttpClient "
       "DelegatingHandlers for logging/retries. Decorators compose without subclass explosion.", tags=["decorator"], seconds=35),
    mc("oo-014", T, DPN, "Intermediate", "Factory Method", "What problem does the Factory Method pattern address?",
       [("Making objects immutable", False), ("Letting subclasses or a dedicated method decide which concrete class to instantiate, so callers depend only on an abstraction", True),
        ("Sharing one instance", False), ("Converting interfaces", False)],
       "Decouples creation from use.", "Callers ask for an `INotificationSender` and the factory returns Email/SMS/Push implementations based "
       "on configuration. Abstract Factory creates families of related objects.", tags=["factory"], seconds=40),
    mc("oo-015", T, DPN, "Advanced", "Unit of Work in EF Core", "In Entity Framework Core, which type already implements the Unit of Work pattern?",
       [("DbSet<T>", False, "DbSet is closer to a repository."), ("DbContext", True), ("IQueryable<T>", False), ("ModelBuilder", False)],
       "DbContext.", "DbContext tracks changes across entities and commits them in one SaveChanges call (one transaction). Wrapping EF in "
       "generic repositories often adds little; use specific query services or the DbContext directly behind an application interface.",
       tags=["unit-of-work", "repository", "ef-core"], seconds=45),
    mc("oo-016", T, ARCH, "Intermediate", "Clean Architecture dependency rule", "What is the dependency rule in Clean Architecture?",
       [("Outer layers must not depend on inner layers", False), ("Source-code dependencies point inward: the domain and application layers know nothing about frameworks, UI or databases", True),
        ("Every layer depends on every other layer", False), ("The database is the centre", False)],
       "Dependencies point toward the domain.", "Infrastructure implements interfaces defined by the application layer, keeping business "
       "rules testable and independent of EF Core, ASP.NET Core or external services.", tags=["clean-architecture"], seconds=40),
]

T = "web-apis"
API = [
    mc("ap-001", T, "REST", "Beginner", "RESTful resource URLs", "Which URL best follows REST resource-naming conventions for retrieving order 42?",
       [("GET /getOrder?id=42", False), ("GET /orders/42", True), ("POST /orders/get/42", False), ("GET /order_42", False)],
       "GET /orders/42.", "Use plural nouns for collections, ids for members, and HTTP methods for actions (GET, POST, PUT, PATCH, DELETE). "
       "Nest only for true containment: /customers/7/orders.", tags=["rest", "naming"], seconds=30),
    mc("ap-002", T, "REST", "Intermediate", "401 vs 403", "When should an API return 403 Forbidden instead of 401 Unauthorized?",
       [("When the token is missing or invalid", False, "That is 401."), ("When the caller is authenticated but not permitted to perform the action", True),
        ("When the resource does not exist", False), ("When the server is down", False)],
       "403 = authenticated but not allowed.", "401 means authentication is missing or failed (include WWW-Authenticate). Some APIs return 404 "
       "instead of 403 to avoid revealing that a resource exists.", tags=["http-status"], seconds=35),
    mc("ap-003", T, "REST", "Intermediate", "Cursor pagination", "Why is cursor (keyset) pagination often preferred over offset pagination for large, frequently changing datasets?",
       [("It allows jumping to any page number", False), ("It stays fast for deep pages and avoids skipped or duplicated items when rows are inserted between requests", True),
        ("It needs no index", False), ("It returns all rows", False)],
       "Stable and efficient deep paging.", "Offset pagination must scan and discard `offset` rows and shifts when data changes. A cursor encodes "
       "the last seen sort key: `WHERE (CreatedAt, Id) < (@c, @id) ORDER BY CreatedAt DESC, Id DESC`.", tags=["pagination"], seconds=50),
    mc("ap-004", T, "REST", "Intermediate", "Idempotency keys", "How can a payment POST endpoint safely handle client retries after a timeout?",
       [("Ask users not to retry", False), ("Require an Idempotency-Key header; store the key with the result and return the stored result for repeated keys", True),
        ("Use GET instead of POST", False), ("Retry on the server automatically", False)],
       "Idempotency keys.", "The key must be unique per logical operation and stored atomically with the operation's outcome (unique index), so "
       "concurrent duplicates cannot double-charge.", tags=["idempotency"], seconds=45),
    mc("ap-005", T, "Microservices", "Intermediate", "Database per service", "Why do microservices commonly use a database per service?",
       [("Databases are cheap", False), ("To keep services loosely coupled and independently deployable, with each service owning its data and schema", True),
        ("To enable cross-service joins", False), ("It is required by Kubernetes", False)],
       "Data ownership and independent evolution.", "Other services access the data through APIs or events, not shared tables. The trade-off "
       "is eventual consistency and the need for patterns like sagas and the outbox.", tags=["microservices", "data"], seconds=40),
    mc("ap-006", T, "Microservices", "Advanced", "Saga pattern", "How does a saga maintain consistency across services without distributed transactions?",
       [("By locking all databases", False), ("By executing a sequence of local transactions and running compensating actions if a later step fails", True),
        ("By using two-phase commit across HTTP", False), ("By sharing one database", False)],
       "Local transactions + compensations.", "Sagas are orchestrated (a coordinator issues commands) or choreographed (services react to events). "
       "Compensations must be idempotent and semantically undo steps (refund, release inventory).", tags=["saga", "distributed-transactions"], seconds=50),
    q("ap-007", T, "Microservices", "WrittenAnswer", "Advanced", "Microservices vs modular monolith",
      "Compare a modular monolith with microservices. When would you choose each for a new product?",
      answer="Start with a modular monolith for small teams and evolving domains; adopt microservices when independent scaling/deployment and team autonomy outweigh distributed-systems costs.",
      explanation="**Modular monolith:** one deployable unit with strict module boundaries (separate namespaces/projects, internal APIs, "
                  "possibly separate schemas). Pros: simple deployment, local transactions, easy refactoring, low ops cost. Cons: one "
                  "scaling unit and release train.\n**Microservices:** independently deployable services owning their data. Pros: team "
                  "autonomy, independent scaling, fault isolation, technology freedom. Cons: network latency/failures, eventual "
                  "consistency, observability and DevOps overhead.\n**Choose:** modular monolith for new products and small teams until "
                  "boundaries are proven; extract services where scaling, deployment cadence or team ownership demand it.",
      reference="A modular monolith keeps strong module boundaries in a single deployment: simpler operations and transactions, but shared "
                "scaling and releases. Microservices give independent deployment, scaling and team ownership at the cost of distributed "
                "systems complexity (latency, partial failure, eventual consistency, observability). Prefer a modular monolith early and "
                "extract services along proven boundaries when organisational or scaling needs justify it.",
      tags=["architecture", "microservices"], seconds=300, premium=True),
]

write("questions/dsa.json", DSA)
write("questions/systemdesign.json", SD)
write("questions/oop.json", OOP)
write("questions/webapis.json", API)
