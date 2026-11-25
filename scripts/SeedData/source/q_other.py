from seedlib import mc, q, write

QUESTIONS = [
    # ---------------- Java ----------------
    mc("ja-001", "java", "Core Java", "Beginner", "Comparing strings in Java", "How should two `String` values be compared for equal content in Java?",
       [("a == b", False, "== compares references."), ("a.equals(b)", True), ("a.compareTo(b) == 1", False), ("a.hashCode() == b.hashCode()", False)],
       "Use equals().", "`==` may appear to work for interned literals but fails for strings created at runtime. Use `Objects.equals(a, b)` "
       "to handle null safely.", tags=["strings", "equality"], seconds=30),
    mc("ja-002", "java", "Core Java", "Intermediate", "equals and hashCode", "What happens if a class overrides `equals` but not `hashCode` and is used as a HashMap key?",
       [("Nothing; it works", False), ("Equal objects may land in different buckets, so lookups fail to find keys that are 'equal'", True),
        ("Compilation fails", False), ("HashMap sorts the keys", False)],
       "The equals/hashCode contract is broken: equal objects must have equal hash codes.", "Records generate both consistently; IDEs or "
       "`Objects.hash(...)` help implement them for classes.", tags=["collections", "hashmap"], seconds=45),
    mc("ja-003", "java", "Core Java", "Intermediate", "Checked exceptions", "What distinguishes checked from unchecked exceptions in Java?",
       [("Checked exceptions extend RuntimeException", False, "Unchecked ones do."),
        ("Checked exceptions must be declared with throws or caught; unchecked exceptions (RuntimeException and Error subclasses) need not be", True),
        ("Unchecked exceptions cannot be caught", False), ("No difference", False)],
       "The compiler enforces handling of checked exceptions.", "Checked: IOException, SQLException. Unchecked: NullPointerException, "
       "IllegalArgumentException. Many modern APIs favour unchecked exceptions for programming errors.", tags=["exceptions"], seconds=40),
    mc("ja-004", "java", "Core Java", "Intermediate", "Why String is immutable", "Which is a benefit of `String` being immutable in Java?",
       [("Strings can be modified in place faster", False), ("Strings can be safely shared, cached (string pool) and used as HashMap keys and across threads", True),
        ("It uses less memory when concatenating in loops", False, "Use StringBuilder for that."), ("It disables garbage collection", False)],
       "Safe sharing, pooling, thread-safety and stable hash codes.", "Repeated concatenation creates many objects; StringBuilder avoids that.",
       tags=["strings", "immutability"], seconds=40),
    mc("ja-005", "java", "Core Java", "Advanced", "volatile", "What does declaring a field `volatile` guarantee in Java?",
       [("That count++ on it is atomic", False), ("Visibility: writes by one thread are visible to subsequent reads by other threads, with ordering guarantees", True),
        ("Mutual exclusion", False), ("That the field is never cached by the JIT", False)],
       "Visibility and ordering, not atomicity.", "`count++` is read-modify-write; use AtomicInteger or synchronized for atomic updates.",
       tags=["concurrency"], seconds=50),

    # ---------------- Python ----------------
    mc("py-001", "python", "Core Python", "Intermediate", "Mutable default arguments", "What does this print?",
       [("[2]", False), ("[1, 2]", True), ("[]", False), ("It raises TypeError", False)],
       "[1, 2] — the default list is created once and shared between calls.",
       "Default values are evaluated once at function definition. Use `items=None` and create a new list inside the function.",
       code="def add(item, items=[]):\n    items.append(item)\n    return items\n\nadd(1)\nprint(add(2))", lang="python",
       tags=["functions", "gotchas"], seconds=45),
    mc("py-002", "python", "Core Python", "Beginner", "list vs tuple", "What is a key difference between a list and a tuple?",
       [("Tuples can only hold numbers", False), ("Tuples are immutable (and hashable if their items are), lists are mutable", True),
        ("Lists are faster to create", False), ("Tuples cannot be indexed", False)],
       "Tuples are immutable.", "Hashable tuples can be dictionary keys or set members; lists cannot.", tags=["data-types"], seconds=30),
    mc("py-003", "python", "Core Python", "Intermediate", "The GIL", "In standard CPython builds, what does the Global Interpreter Lock imply for CPU-bound code?",
       [("Threads run Python bytecode truly in parallel on all cores", False),
        ("Only one thread executes Python bytecode at a time, so CPU-bound work should use multiprocessing (or native extensions)", True),
        ("Async code is disabled", False), ("I/O-bound threads cannot overlap", False, "Threads release the GIL during blocking I/O.")],
       "One thread runs bytecode at a time.", "Threads still help I/O-bound work. CPython 3.13 introduced an experimental free-threaded build "
       "without the GIL, but the default build keeps it.", tags=["concurrency", "gil"], seconds=50),
    mc("py-004", "python", "Core Python", "Intermediate", "Generators", "What is the main advantage of a generator (a function using `yield`)?",
       [("It runs faster than any loop", False), ("It produces values lazily, one at a time, without building the whole sequence in memory", True),
        ("It can be indexed like a list", False), ("It runs in parallel", False)],
       "Lazy evaluation with constant memory.", "Generators are ideal for streaming large files or infinite sequences; they can be iterated only once.",
       tags=["generators"], seconds=35),
    mc("py-005", "python", "Core Python", "Beginner", "List comprehension output", "What does this print?",
       [("[0, 1, 4, 9]", False), ("[0, 4]", True), ("[1, 9]", False), ("[4]", False)], "[0, 4]",
       "range(4) yields 0..3; the filter keeps 0 and 2; their squares are 0 and 4.",
       code="print([i * i for i in range(4) if i % 2 == 0])", lang="python", tags=["comprehensions"], seconds=30),

    # ---------------- Kotlin ----------------
    mc("kt-001", "kotlin", "Core Kotlin", "Beginner", "val vs var", "What is the difference between `val` and `var` in Kotlin?",
       [("val is for numbers only", False), ("val declares a read-only reference that cannot be reassigned; var can be reassigned", True),
        ("var is immutable", False), ("They are identical", False)],
       "val = read-only reference; var = mutable reference.", "`val` does not make the object immutable: a `val list = mutableListOf()` can still "
       "be modified. Prefer val by default.", tags=["basics"], seconds=30),
    mc("kt-002", "kotlin", "Core Kotlin", "Beginner", "Safe call and Elvis", "What does `name?.length ?: 0` evaluate to when `name` is null?",
       [("null", False), ("0", True), ("It throws NullPointerException", False), ("-1", False)],
       "0.", "`?.` returns null instead of throwing; the Elvis operator `?:` supplies a default. `!!` asserts non-null and throws if it is null.",
       tags=["null-safety"], seconds=30),
    mc("kt-003", "kotlin", "Core Kotlin", "Intermediate", "data classes", "What does the compiler generate for a Kotlin `data class`?",
       [("Only a constructor", False), ("equals/hashCode, toString, copy and componentN functions based on the primary-constructor properties", True),
        ("A database table", False), ("Getters only for private fields", False)],
       "Value-semantics members.", "Only properties in the primary constructor participate. `copy(...)` makes immutable updates easy, which is "
       "why data classes are common for UI state in Compose.", tags=["data-classes"], seconds=40),
    mc("kt-004", "kotlin", "Core Kotlin", "Advanced", "delay vs Thread.sleep", "Inside a coroutine, how does `delay(1000)` differ from `Thread.sleep(1000)`?",
       [("No difference", False), ("delay suspends the coroutine without blocking the thread, freeing it for other coroutines; sleep blocks the thread", True),
        ("delay blocks the main thread", False), ("sleep is cancellable", False)],
       "Suspension vs blocking.", "`delay` is also cancellable. Never block the main thread on Android; use suspend functions and switch "
       "blocking work to `Dispatchers.IO`.", tags=["coroutines"], seconds=45),

    # ---------------- C++ ----------------
    mc("cp-001", "cpp", "Core C++", "Beginner", "References vs pointers", "Which statement about C++ references is true?",
       [("A reference can be null", False), ("A reference must be initialised, cannot be null in valid code and cannot be reseated to refer to another object", True),
        ("References require manual delete", False), ("References are slower than pointers", False)],
       "Always bound, never reseated.", "Use references for required parameters and pointers (or std::optional) when 'no object' is a valid state.",
       tags=["references", "pointers"], seconds=35),
    mc("cp-002", "cpp", "Core C++", "Intermediate", "RAII", "What is RAII?",
       [("A threading library", False), ("Tying resource lifetime to object lifetime: acquire in the constructor, release in the destructor", True),
        ("Runtime type identification", False), ("A garbage collector", False)],
       "Resource Acquisition Is Initialization.", "Destructors run deterministically at scope exit, even during exceptions, so smart pointers, "
       "lock_guard and file wrappers never leak.", tags=["raii"], seconds=35),
    mc("cp-003", "cpp", "Core C++", "Intermediate", "unique_ptr vs shared_ptr", "Which statement about smart pointers is correct?",
       [("unique_ptr can be copied", False), ("unique_ptr has sole ownership and can only be moved; shared_ptr uses reference counting for shared ownership", True),
        ("shared_ptr never frees memory", False), ("Both are garbage collected", False)],
       "unique_ptr = exclusive, movable; shared_ptr = shared, ref-counted.", "Prefer unique_ptr by default (zero overhead). Break shared_ptr "
       "cycles with weak_ptr. Create them with make_unique / make_shared.", tags=["smart-pointers"], seconds=40),
    mc("cp-004", "cpp", "Core C++", "Advanced", "Virtual destructors", "Why should a polymorphic base class declare a virtual destructor?",
       [("To make the class abstract", False), ("Deleting a derived object through a base pointer otherwise has undefined behaviour and the derived destructor does not run", True),
        ("Virtual destructors are faster", False), ("To allow copying", False)],
       "So delete via a base pointer calls the derived destructor.", "Rule of thumb: if a class has any virtual functions, give it a public "
       "virtual destructor (or a protected non-virtual one).", tags=["destructors", "polymorphism"], seconds=45),

    # ---------------- DevOps ----------------
    mc("do-001", "devops", "Docker", "Beginner", "Image vs container", "What is the difference between a Docker image and a container?",
       [("They are the same", False), ("An image is a read-only template of layers; a container is a running (or stopped) instance of an image with its own writable layer", True),
        ("Containers are stored in registries", False), ("Images run processes", False)],
       "Image = template; container = instance.", "Many containers can run from one image. Container filesystem changes are lost when it is "
       "removed unless stored in volumes.", tags=["docker"], seconds=30),
    mc("do-002", "devops", "Docker", "Intermediate", "Layer caching in Dockerfiles", "How should a .NET Dockerfile be ordered to maximise build cache reuse?",
       [("COPY . . first, then dotnet restore", False), ("Copy the project files and run dotnet restore first, then copy the remaining source and build", True),
        ("Disable caching with --no-cache", False), ("Put RUN apt-get update in the last line", False)],
       "Restore dependencies in a layer that only changes when project files change.", "Docker reuses a layer when its instruction and inputs "
       "are unchanged, so dependency restores are skipped on code-only changes.", tags=["docker", "caching"], seconds=45),
    mc("do-003", "devops", "Docker", "Intermediate", "Multi-stage builds", "What is the main benefit of a multi-stage Docker build?",
       [("Builds run on multiple machines", False), ("The final image contains only runtime artefacts, not the SDK and build tools, making it smaller and safer", True),
        ("It enables hot reload", False), ("It encrypts layers", False)],
       "Small, minimal runtime images.", "Build with the SDK image, then `COPY --from=build` into the aspnet runtime image (or a chiseled/distroless image).",
       tags=["docker"], seconds=35),
    mc("do-004", "devops", "Kubernetes", "Intermediate", "Kubernetes Services", "Which Kubernetes object provides a stable virtual IP and DNS name for a set of pods?",
       [("Deployment", False, "Deployments manage ReplicaSets/pods."), ("Service", True), ("ConfigMap", False), ("Namespace", False)],
       "A Service.", "Pods are ephemeral with changing IPs; a Service selects pods by labels and load-balances to ready endpoints. Ingress or "
       "Gateway API exposes HTTP routes externally.", tags=["kubernetes"], seconds=35),
    mc("do-005", "devops", "Kubernetes", "Intermediate", "Readiness vs liveness probes", "What happens when a pod's **readiness** probe fails?",
       [("The container is restarted", False, "That happens on liveness failure."), ("The pod is removed from Service endpoints so it receives no traffic until it is ready again", True),
        ("The node is drained", False), ("Nothing", False)],
       "It stops receiving traffic.", "Liveness failures restart the container; readiness failures only take it out of rotation. Startup probes "
       "protect slow-starting apps from premature liveness kills.", tags=["kubernetes", "probes"], seconds=45),
    mc("do-006", "devops", "Git", "Intermediate", "Undoing a pushed commit", "A bad commit is already on the shared main branch. What is the safest way to undo it?",
       [("git reset --hard HEAD~1 and force-push", False, "Rewrites shared history."), ("git revert <commit>, which creates a new commit that reverses the change", True),
        ("Delete the repository", False), ("git commit --amend", False)],
       "git revert.", "Revert preserves history for everyone. Reset/rebase/amend rewrite history and are fine only on unpublished local branches.",
       tags=["git"], seconds=35),
    mc("do-007", "devops", "Git", "Intermediate", "Rebase vs merge", "What does `git rebase main` do on a feature branch?",
       [("Deletes main", False), ("Re-applies the feature branch's commits on top of main, rewriting them with new hashes for a linear history", True),
        ("Creates a merge commit", False, "That is git merge."), ("Pushes to the remote", False)],
       "Replays commits onto the new base.", "Rebase gives linear history but rewrites commits — avoid rebasing branches others have pulled.",
       tags=["git"], seconds=40),
    mc("do-008", "devops", "CI/CD", "Intermediate", "Blue-green deployments", "How does a blue-green deployment reduce release risk?",
       [("By deploying only on Fridays", False), ("A new version runs alongside the current one; traffic switches once it is verified and can switch back instantly", True),
        ("By skipping tests", False), ("By updating servers one file at a time", False)],
       "Two environments with an instant traffic switch.", "Canary releases shift a small percentage of traffic first. Database migrations must "
       "be backward compatible (expand/contract) for either strategy.", tags=["deployment"], seconds=40),

    # ---------------- OS & networking ----------------
    mc("os-001", "cs-fundamentals", "Operating Systems", "Beginner", "Processes vs threads", "Which statement about processes and threads is correct?",
       [("Threads have separate address spaces", False), ("Threads of the same process share its address space and resources; processes are isolated from each other", True),
        ("Processes are lighter than threads", False), ("A process can have only one thread", False)],
       "Threads share memory within a process.", "Shared memory makes inter-thread communication cheap but requires synchronisation; processes "
       "communicate via IPC and offer stronger isolation.", tags=["processes", "threads"], seconds=35),
    mc("os-002", "cs-fundamentals", "Operating Systems", "Intermediate", "Deadlock conditions", "Which is **not** one of the four necessary (Coffman) conditions for deadlock?",
       [("Mutual exclusion", False), ("Hold and wait", False), ("Circular wait", False), ("Starvation", True)],
       "Starvation.", "The four conditions are mutual exclusion, hold and wait, no preemption and circular wait. Preventing any one (e.g. "
       "acquiring locks in a global order to avoid circular wait) prevents deadlock.", tags=["deadlock"], seconds=40),
    mc("os-003", "cs-fundamentals", "Operating Systems", "Advanced", "Page faults", "What triggers a page fault in a virtual memory system?",
       [("Writing to disk", False), ("Accessing a virtual page that is not currently mapped to a physical frame (e.g. not yet loaded or swapped out)", True),
        ("A CPU cache miss", False), ("Deleting a file", False)],
       "Accessing an unmapped page.", "The OS handles it by loading the page (minor faults need no disk I/O). Excessive major faults cause "
       "thrashing.", tags=["virtual-memory"], seconds=45),
    mc("nw-001", "cs-fundamentals", "Computer Networks", "Beginner", "TCP vs UDP", "Which statement about TCP and UDP is correct?",
       [("UDP guarantees ordered delivery", False), ("TCP is connection-oriented with reliable, ordered delivery and flow control; UDP is connectionless with no delivery guarantees", True),
        ("TCP is always faster", False), ("UDP requires a handshake", False)],
       "TCP = reliable stream; UDP = best-effort datagrams.", "UDP suits latency-sensitive traffic (games, voice, DNS); QUIC (HTTP/3) builds "
       "reliability and encryption on top of UDP.", tags=["tcp", "udp"], seconds=35),
    mc("nw-002", "cs-fundamentals", "Computer Networks", "Beginner", "First step loading a website", "When a browser loads `https://example.com` for the first time, what must happen before a TCP connection can be opened?",
       [("The TLS handshake", False), ("DNS resolution of example.com to an IP address", True), ("The HTTP response", False), ("Rendering the page", False)],
       "DNS lookup.", "Order: DNS → TCP handshake → TLS handshake → HTTP request/response → parsing and rendering (plus caching at each step).",
       tags=["dns", "http"], seconds=35),
    mc("nw-003", "cs-fundamentals", "Computer Networks", "Intermediate", "Purpose of the TLS handshake", "What does the TLS handshake accomplish?",
       [("Compresses the page", False), ("Authenticates the server via its certificate and establishes shared session keys for encryption", True),
        ("Resolves DNS", False), ("Caches cookies", False)],
       "Authentication + key agreement.", "Certificates are validated against trusted CAs and the hostname. TLS 1.3 completes in one round trip "
       "and supports 0-RTT resumption.", tags=["tls", "https"], seconds=40),
    mc("nw-004", "cs-fundamentals", "Computer Networks", "Intermediate", "HTTP/2 multiplexing", "What is a key improvement of HTTP/2 over HTTP/1.1?",
       [("Plain-text headers", False), ("Multiplexing many concurrent requests over one connection, reducing head-of-line blocking at the HTTP layer", True),
        ("It removes TCP", False, "HTTP/3 uses QUIC over UDP."), ("It disables caching", False)],
       "Multiplexed streams over a single connection (plus header compression).", "TCP-level head-of-line blocking remains, which HTTP/3 "
       "addresses with QUIC.", tags=["http2"], seconds=40),

    # ---------------- Security ----------------
    mc("se-001", "security", "Web Security", "Beginner", "Preventing SQL injection", "What is the primary defence against SQL injection?",
       [("Hiding error messages", False), ("Parameterised queries / prepared statements (or an ORM that parameterises)", True),
        ("Client-side validation", False), ("Using POST instead of GET", False)],
       "Parameterised queries.", "Add least-privilege database accounts and input validation as defence in depth; never build SQL by string "
       "concatenation with user input.", tags=["sql-injection", "owasp"], seconds=30),
    mc("se-002", "security", "Web Security", "Intermediate", "Preventing XSS", "Which measures best prevent cross-site scripting (XSS)?",
       [("Using HTTPS only", False), ("Context-aware output encoding (as frameworks like React and Razor do by default) plus a Content Security Policy", True),
        ("Storing tokens in localStorage", False), ("Disabling cookies", False)],
       "Encode output and add CSP.", "Danger zones: `dangerouslySetInnerHTML`, `innerHTML`, `Html.Raw`. Sanitise rich HTML with a vetted "
       "library and mark session cookies HttpOnly.", tags=["xss", "owasp"], seconds=40),
    mc("se-003", "security", "Web Security", "Intermediate", "CSRF mitigation", "Which mitigates cross-site request forgery for cookie-authenticated apps?",
       [("Using JSON", False), ("Anti-forgery tokens and SameSite cookies", True), ("Longer passwords", False), ("HTTP/2", False)],
       "Anti-forgery tokens + SameSite.", "CSRF abuses the browser automatically attaching cookies. APIs authenticated with bearer tokens in the "
       "Authorization header are not vulnerable to classic CSRF.", tags=["csrf", "owasp"], seconds=40),
    mc("se-004", "security", "Web Security", "Intermediate", "Storing passwords", "How should passwords be stored?",
       [("Encrypted with AES so they can be recovered", False), ("Hashed with a slow, salted algorithm such as PBKDF2, bcrypt or Argon2", True),
        ("Hashed with MD5", False), ("In plain text in a secure database", False)],
       "Slow, salted password hashing.", "Fast hashes (MD5, SHA-256) are brute-forced quickly on GPUs. ASP.NET Core Identity uses PBKDF2 with "
       "a per-user salt and many iterations.", tags=["passwords", "hashing"], seconds=35),
    mc("se-005", "security", "Web Security", "Advanced", "Validating JWTs", "Which JWT validation step is essential to prevent token forgery?",
       [("Decoding the payload", False), ("Verifying the signature with the expected key and algorithm, plus issuer, audience and expiry", True),
        ("Checking the token length", False), ("Base64-encoding it again", False)],
       "Verify signature (pinning the algorithm), issuer, audience and lifetime.", "Never accept `alg: none` or let the token choose the "
       "algorithm; reject tokens with unexpected issuers or audiences.", tags=["jwt"], seconds=45),
    mc("se-006", "security", "Web Security", "Beginner", "Least privilege", "What does the principle of least privilege mean?",
       [("Give every user admin rights to avoid support tickets", False), ("Grant each user, service and process only the permissions required for its task", True),
        ("Use the fewest servers possible", False), ("Log less data", False)],
       "Minimal necessary permissions.", "Applies to database accounts, cloud IAM roles, API scopes and container users (run as non-root).",
       tags=["access-control"], seconds=30),

    # ---------------- Cloud ----------------
    mc("cl-001", "cloud", "Cloud Fundamentals", "Beginner", "Cloud service models", "In which model does the provider manage the runtime and OS while you deploy only your application code?",
       [("IaaS", False), ("PaaS", True), ("On-premises", False), ("SaaS", False, "With SaaS you use a finished application.")],
       "PaaS.", "IaaS gives VMs and networks you manage; PaaS (e.g. Azure App Service) runs your code; SaaS delivers complete software.",
       tags=["cloud-models"], seconds=30),
    mc("cl-002", "cloud", "Cloud Fundamentals", "Intermediate", "Autoscaling", "What does horizontal autoscaling do?",
       [("Increases VM size during the night", False), ("Adds or removes instances automatically based on metrics such as CPU, memory or queue length", True),
        ("Backs up databases", False), ("Changes DNS records", False)],
       "Metric-driven instance count changes.", "Services must be stateless and start quickly; set min/max limits and cool-down periods.",
       tags=["autoscaling"], seconds=30),
    mc("cl-003", "cloud", "Cloud Fundamentals", "Intermediate", "Availability zones", "Why deploy instances across multiple availability zones?",
       [("To reduce latency to all users worldwide", False), ("To survive the failure of a single data centre within a region", True),
        ("It is cheaper", False), ("To avoid backups", False)],
       "Zone-level fault tolerance.", "Use multiple regions for disaster recovery or global latency; multi-AZ protects against data-centre outages.",
       tags=["high-availability"], seconds=30),
    mc("cl-004", "cloud", "Cloud Fundamentals", "Beginner", "Object storage", "Which workload is object storage (S3, Azure Blob) best suited for?",
       [("Low-latency relational transactions", False), ("Storing large amounts of unstructured files such as images, backups and logs", True),
        ("CPU-bound computation", False), ("In-memory caching", False)],
       "Unstructured files at scale.", "Serve user uploads via pre-signed URLs or a CDN instead of streaming them through the API.",
       tags=["storage"], seconds=30),
]

by_file = {}
for item in QUESTIONS:
    by_file.setdefault(item["tech"], []).append(item)
for tech, items in by_file.items():
    write(f"questions/{tech}.json", items)
