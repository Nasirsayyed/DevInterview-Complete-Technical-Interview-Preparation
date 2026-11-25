from seedlib import mc, q, write

T = "csharp"
F, O, C, L, D, A, M, E, R = ("C# Fundamentals", "OOP in C#", "Collections & Generics", "LINQ", "Delegates & Events",
                             "Async & Multithreading", "Memory Management", "Exception Handling", "Reflection & Extension Methods")

QUESTIONS = [
    mc("cs-001", T, F, "Beginner", "Value types in C#", "Which of the following is a **value type** in C#?",
       [("string", False, "string is a reference type (with value-like equality)."),
        ("int[]", False, "Arrays are always reference types, even arrays of value types."),
        ("DateTime", True, "DateTime is a struct, so it is a value type."),
        ("object", False, "object is the root reference type.")],
       "DateTime — it is a struct.",
       "Value types (structs, enums and the built-in numeric types) hold their data directly and are copied on assignment. "
       "Reference types (classes, arrays, delegates, string, object) hold a reference to an object on the managed heap. "
       "`DateTime`, `Guid`, `TimeSpan` and `decimal` are common BCL structs.", tags=["types", "struct"], seconds=45),

    mc("cs-002", T, F, "Beginner", "String immutability", "What does this code print?",
       [("hello", True), ("hello world", False), ("It does not compile", False), ("An empty line", False)],
       "hello",
       "Strings are immutable. `b += \" world\"` creates a **new** string and assigns it to `b`; `a` still refers to the "
       "original \"hello\" instance. This is why repeated concatenation in loops should use `StringBuilder`.",
       code='string a = "hello";\nstring b = a;\nb += " world";\nConsole.WriteLine(a);', lang="csharp",
       tags=["string", "immutability"], seconds=45),

    mc("cs-003", T, F, "Intermediate", "const vs readonly", "Which statement correctly describes `const` and `readonly` fields?",
       [("Both are evaluated at runtime and can be set in any method", False),
        ("const values are compile-time constants copied into calling assemblies; readonly fields are assigned at runtime in their declaration or a constructor", True),
        ("readonly fields are implicitly static and const fields are per-instance", False),
        ("const can hold any reference type initialised with new", False)],
       "const = compile-time constant (implicitly static, baked into callers); readonly = set once at runtime (declaration or constructor).",
       "`const` must be a compile-time constant of a primitive, enum or string type and is implicitly static. Because its value is "
       "embedded into the IL of consuming assemblies, changing a public const requires recompiling consumers. `readonly` fields can "
       "be instance or static, can hold any type, and are assigned in the declaration or a constructor — useful for values known only "
       "at runtime such as configuration or injected dependencies.", tags=["const", "readonly"]),

    q("cs-004", T, F, "TrueFalse", "Intermediate", "Parameterless struct constructors",
      "True or false: since C# 10 a `struct` may declare an explicit parameterless constructor.",
      [("True", True, "C# 10 added explicit parameterless constructors and field initializers for structs."), ("False", False)],
      "True.",
      "Before C# 10 structs always had an implicit parameterless constructor that zero-initialised all fields. C# 10 allows declaring "
      "one explicitly. Note that `default(MyStruct)` and array elements still bypass it and are zero-initialised.",
      tags=["struct", "csharp10"], seconds=40),

    mc("cs-005", T, F, "Beginner", "Comparing strings with ==", "What does `==` compare when both operands are of type `string`?",
       [("The memory addresses of the two strings", False),
        ("Their character content, because string overloads the == operator", True),
        ("Only their lengths", False),
        ("Their hash codes", False)],
       "Their content — `string` overloads `==` to perform an ordinal value comparison.",
       "For most reference types `==` compares references, but `System.String` overloads `==` and `!=` to compare values "
       "(ordinal, case-sensitive). If one operand is typed as `object`, the overload is not used and the comparison becomes a "
       "reference comparison — a classic bug. Use `string.Equals(a, b, StringComparison.OrdinalIgnoreCase)` for case-insensitive checks.",
       tags=["string", "equality"], seconds=45),

    mc("cs-006", T, O, "Intermediate", "Interfaces vs abstract classes", "Which statement is true in modern C# (8.0 and later)?",
       [("A class can inherit from several abstract classes", False, "C# supports single class inheritance only."),
        ("Interfaces can provide default method implementations but cannot declare instance fields", True),
        ("Abstract classes cannot have constructors", False, "They can; constructors run when a derived class is instantiated."),
        ("Interface members are private by default", False, "Interface members are public by default.")],
       "Interfaces may have default implementations (C# 8+) but no instance state; a class inherits from one base class.",
       "Use an abstract class when related types share state and implementation; use interfaces to describe capabilities that unrelated "
       "types can implement (a class can implement many). Default interface methods help evolve interfaces without breaking implementers, "
       "but interfaces still cannot hold instance fields.", tags=["interface", "abstract-class"]),

    mc("cs-007", T, O, "Intermediate", "override vs new", "What does this program print?",
       [("BA", True), ("BC", False), ("AA", False), ("AC", False)],
       "BA",
       "`B.Name` **overrides** the virtual method, so a call through an `A` reference dispatches to `B` at runtime. `C.Name` uses `new`, "
       "which **hides** the base method: the call is bound at compile time to the static type `A`, so `A.Name` runs. Hiding with `new` "
       "rarely expresses the intent and usually indicates a design smell.",
       code='class A { public virtual string Name() => "A"; }\nclass B : A { public override string Name() => "B"; }\n'
            'class C : A { public new string Name() => "C"; }\n\nA b = new B();\nA c = new C();\nConsole.WriteLine(b.Name() + c.Name());',
       lang="csharp", tags=["polymorphism", "virtual"], seconds=90),

    mc("cs-008", T, C, "Intermediate", "Dictionary lookup complexity",
       "What is the average time complexity of `Dictionary<TKey, TValue>.TryGetValue`?",
       [("O(1)", True), ("O(log n)", False), ("O(n)", False), ("O(n log n)", False)],
       "O(1) on average.",
       "`Dictionary` is a hash table: the key's `GetHashCode` selects a bucket and `Equals` resolves collisions. With a good hash "
       "distribution lookups are O(1) on average; many colliding keys degrade it towards O(n). `SortedDictionary` (a red-black tree) "
       "gives O(log n) with ordered keys.", tags=["dictionary", "complexity"], seconds=40),

    q("cs-009", T, C, "MultipleSelect", "Intermediate", "Collections that enforce uniqueness",
      "Which collections **reject duplicate elements**? Select all that apply.",
      [("HashSet<T>", True, "Add returns false for duplicates."), ("SortedSet<T>", True, "Also a set, kept in sorted order."),
       ("List<T>", False, "Lists allow duplicates."), ("Queue<T>", False, "Queues allow duplicates.")],
      "HashSet<T> and SortedSet<T>.",
      "Set types model mathematical sets: `Add` returns `false` instead of inserting a duplicate. `HashSet<T>` offers O(1) average "
      "membership tests; `SortedSet<T>` keeps elements ordered with O(log n) operations. Dictionary keys are also unique.",
      tags=["hashset", "collections"], seconds=50),

    mc("cs-010", T, C, "Advanced", "Generic variance",
       "Why does `IEnumerable<object> items = new List<string>();` compile, while `List<object> items = new List<string>();` does not?",
       [("List<T> is a class and classes can never be converted", False),
        ("IEnumerable<T> is declared covariant (out T); List<T> is invariant because it also accepts T as input", True),
        ("Because string is sealed", False),
        ("The first line compiles only with an explicit cast", False)],
       "IEnumerable<out T> is covariant; List<T> is invariant.",
       "Covariance (`out T`) is safe when T only flows **out** of the type — you can read strings as objects. `List<T>` also lets "
       "you **write** T (`Add`), so treating a `List<string>` as `List<object>` would allow adding an `int`. Variance is supported "
       "only on interfaces and delegates, and only for reference type arguments.", tags=["generics", "covariance"], seconds=90),

    mc("cs-011", T, L, "Intermediate", "Deferred execution", "What does this code print?",
       [("2", False), ("3", True, "The query runs at Count(), after 4 was added: 2, 3, 4."), ("4", False), ("1", False)],
       "3",
       "Most LINQ operators (`Where`, `Select`, …) use **deferred execution**: they build a query that runs when enumerated. "
       "`Count()` enumerates after `4` was added, so the result is 3 (values 2, 3, 4). Call `ToList()` to snapshot results, and beware "
       "of enumerating an expensive query multiple times.",
       code="var numbers = new List<int> { 1, 2, 3 };\nvar query = numbers.Where(n => n > 1);\nnumbers.Add(4);\nConsole.WriteLine(query.Count());",
       lang="csharp", tags=["linq", "deferred-execution"], seconds=60),

    mc("cs-012", T, L, "Intermediate", "Single() semantics",
       "What happens when `Single()` is called on a sequence that contains more than one element?",
       [("It returns the first element", False), ("It returns the default value", False),
        ("It throws InvalidOperationException", True), ("It returns the last element", False)],
       "It throws InvalidOperationException.",
       "`Single()` asserts the sequence has **exactly one** element and throws if it is empty or has more. `SingleOrDefault()` returns "
       "default for an empty sequence but still throws for more than one. `First()` / `FirstOrDefault()` just take the first match. "
       "Use `Single` when duplicates would indicate a data bug you want to surface.", tags=["linq"], seconds=45),

    mc("cs-013", T, L, "Advanced", "IQueryable vs IEnumerable", "What is the key difference between `IQueryable<T>` and `IEnumerable<T>`?",
       [("IQueryable is always faster", False),
        ("IQueryable builds an expression tree that a provider (e.g. EF Core) translates, such as into SQL; IEnumerable operators run as in-memory delegates", True),
        ("IEnumerable cannot be used with LINQ", False),
        ("IQueryable executes immediately while IEnumerable is deferred", False)],
       "IQueryable carries expression trees for a query provider to translate; IEnumerable executes LINQ in memory.",
       "With `IQueryable`, filters like `Where` become part of the SQL sent to the database. Calling `AsEnumerable()` or `ToList()` too "
       "early switches to LINQ-to-Objects, so subsequent filtering happens in memory after loading more rows than needed. Both are deferred.",
       tags=["linq", "ef-core", "iqueryable"], seconds=75),

    mc("cs-014", T, D, "Intermediate", "What the event keyword adds",
       "Compared with a public delegate field, what does declaring a member with the `event` keyword change?",
       [("Events can only have one subscriber", False),
        ("Outside code can only subscribe (+=) and unsubscribe (-=); only the declaring type can raise or reassign it", True),
        ("Events are invoked asynchronously", False),
        ("Events cannot be static", False)],
       "It encapsulates the delegate: external code may only += / -=.",
       "With a public delegate field, any caller could overwrite all subscribers with `=` or invoke the delegate. `event` restricts "
       "external access to add/remove accessors, enforcing the publisher/subscriber contract. Remember to unsubscribe from long-lived "
       "publishers to avoid memory leaks.", tags=["events", "delegates"]),

    mc("cs-015", T, D, "Beginner", "Func delegate signature", "What does `Func<int, int, string>` represent?",
       [("A method taking a string and returning two ints", False),
        ("A method taking two int parameters and returning a string", True),
        ("A method taking three parameters and returning void", False),
        ("An event with three handlers", False)],
       "Two int inputs, string return value — the last type argument of Func is the return type.",
       "`Func<..., TResult>` delegates return a value (last type parameter); `Action<...>` delegates return void; `Predicate<T>` "
       "returns bool. Lambdas like `(a, b) => (a + b).ToString()` convert to these delegate types.", tags=["delegates", "func"], seconds=40),

    mc("cs-016", T, A, "Intermediate", "What await does", "When execution reaches `await` on a task that has **not** completed, what happens?",
       [("The current thread blocks until the task completes", False),
        ("The method returns to its caller and the remainder runs as a continuation when the task completes", True),
        ("A new thread is always created to run the rest of the method", False),
        ("An exception is thrown", False)],
       "Control returns to the caller; the rest of the method is scheduled as a continuation.",
       "`await` does not block. The compiler turns the method into a state machine; if the awaitable is incomplete, the method returns "
       "an incomplete Task and resumes later (on the captured SynchronizationContext if any, otherwise the thread pool). This frees the "
       "thread to serve other work — the main benefit for I/O-bound server code.", tags=["async", "await"]),

    mc("cs-017", T, A, "Advanced", "Sync-over-async deadlock",
       "Why can calling `.Result` on an incomplete task deadlock in a UI app or classic ASP.NET (with a synchronization context)?",
       [("Because .Result is not thread-safe", False),
        ("The blocked thread owns the synchronization context that the awaited continuation needs to resume on, so neither can proceed", True),
        ("Because the task is garbage collected", False),
        ("Because .Result always starts the task twice", False)],
       "The continuation waits for the context the blocked thread is holding.",
       "The async method captured a single-threaded SynchronizationContext. `.Result` blocks that thread; when the awaited operation "
       "completes, the continuation is posted to the same context and never runs. ASP.NET Core has no SynchronizationContext, but "
       "blocking still wastes thread-pool threads and can cause starvation. Prefer async all the way; `ConfigureAwait(false)` in libraries.",
       tags=["async", "deadlock"], seconds=90),

    mc("cs-018", T, A, "Intermediate", "Running independent calls concurrently",
       "Which approach runs three independent I/O-bound calls concurrently?",
       [("await each call one after another", False),
        ("Start all three tasks, then await Task.WhenAll(t1, t2, t3)", True),
        ("Call .Wait() on each task in a loop", False),
        ("Wrap each await in Task.Run", False)],
       "Start the tasks first, then `await Task.WhenAll(...)`.",
       "Awaiting sequentially makes total latency the sum of all calls. Creating the tasks first lets the I/O overlap, so latency becomes "
       "roughly the slowest call. `Task.Run` is for CPU-bound work and adds nothing for I/O. Handle failures: `await Task.WhenAll` throws "
       "the first exception; inspect each task for details, and pass a CancellationToken.", tags=["async", "task-whenall"], seconds=60),

    mc("cs-019", T, A, "Advanced", "When async void is acceptable", "When is an `async void` method appropriate?",
       [("For any method that does not return a value", False),
        ("Only for event handlers, because callers cannot await it or catch its exceptions", True),
        ("For CPU-bound work", False),
        ("For library entry points", False)],
       "Event handlers only.",
       "An `async void` method cannot be awaited, so the caller cannot know when it completes, and exceptions are raised on the "
       "SynchronizationContext (often crashing the process). Use `async Task` everywhere else.", tags=["async"], seconds=50),

    mc("cs-020", T, A, "Advanced", "await inside lock", "Why does the compiler reject `await` inside a `lock` block?",
       [("lock statements are deprecated", False),
        ("Monitor-based locks are thread-affine; after await the continuation may run on another thread that does not own the lock", True),
        ("Because await always throws inside try blocks", False),
        ("It is allowed; the compiler accepts it", False)],
       "Monitor locks must be released by the acquiring thread; await may resume elsewhere (error CS1996).",
       "Holding a lock across an await could also block other threads for unbounded time. For async mutual exclusion use "
       "`SemaphoreSlim(1, 1)` with `await WaitAsync()` and release in `finally`.", tags=["async", "lock", "semaphore"], seconds=75),

    mc("cs-021", T, M, "Intermediate", "Large Object Heap", "Which objects are allocated on the Large Object Heap (LOH) in .NET?",
       [("Objects of 85,000 bytes or more", True), ("All arrays", False), ("Objects that survive generation 1", False), ("All strings", False)],
       "Objects of at least 85,000 bytes.",
       "The LOH holds large objects (≥ 85,000 bytes, typically big arrays/strings). It is collected with generation 2 and is not compacted "
       "by default, so frequent large allocations cause fragmentation and expensive gen-2 GCs. Pool buffers with `ArrayPool<T>` to reduce "
       "LOH pressure.", tags=["gc", "loh"], seconds=50),

    mc("cs-022", T, M, "Intermediate", "using declarations", "What does a `using` statement or declaration guarantee for an `IDisposable`?",
       [("The object is garbage collected immediately", False),
        ("Dispose is called when the scope exits, even if an exception is thrown", True),
        ("The object can no longer be referenced", False),
        ("The object's finalizer is suppressed automatically", False)],
       "Dispose runs at scope exit (compiled to try/finally).",
       "`using` compiles to a try/finally that calls `Dispose()`. It releases unmanaged resources (file handles, sockets, DB connections) "
       "deterministically; it does not free managed memory. Use `await using` for `IAsyncDisposable`.", tags=["idisposable", "using"], seconds=45),

    q("cs-023", T, M, "TrueFalse", "Advanced", "GC and Dispose",
      "True or false: the garbage collector automatically calls `Dispose()` on unreachable objects that implement `IDisposable`.",
      [("True", False), ("False", True, "The GC only runs finalizers; Dispose must be called explicitly.")],
      "False.",
      "The GC knows nothing about `IDisposable`. It may run a **finalizer** (`~ClassName`) on objects that declare one, at an "
      "unpredictable time. The dispose pattern calls `GC.SuppressFinalize(this)` in `Dispose` so the finalizer is skipped when "
      "cleanup already happened.", tags=["gc", "idisposable"], seconds=40),

    mc("cs-024", T, E, "Intermediate", "throw vs throw ex", "In a catch block, what is the difference between `throw;` and `throw ex;`?",
       [("No difference", False),
        ("`throw;` rethrows preserving the original stack trace; `throw ex;` resets the stack trace to the current location", True),
        ("`throw ex;` is required to rethrow", False),
        ("`throw;` swallows the exception", False)],
       "`throw;` preserves the original stack trace.",
       "Use `throw;` to rethrow. `throw ex;` loses where the exception originated, making diagnostics hard. To wrap, throw a new "
       "exception with the original as `InnerException`. `ExceptionDispatchInfo.Capture(ex).Throw()` preserves the trace across threads.",
       tags=["exceptions"], seconds=45),

    mc("cs-025", T, E, "Beginner", "finally blocks", "Which statement about a `finally` block is correct?",
       [("It runs only when no exception occurs", False),
        ("It runs whether or not an exception is thrown, including when try contains a return statement", True),
        ("It runs before the catch block", False),
        ("It can change the value already returned by return in try for value types", False)],
       "It always runs when control leaves the try (normal exit, return or exception).",
       "finally is for cleanup. Exceptions: it may not run if the process terminates (e.g. `Environment.FailFast`, stack overflow).",
       tags=["exceptions", "finally"], seconds=40),

    mc("cs-026", T, R, "Beginner", "Extension method requirements", "What is required to declare an extension method?",
       [("An instance method marked with [Extension]", False),
        ("A static method in a non-generic static class whose first parameter is prefixed with `this`", True),
        ("A method in a partial class", False),
        ("Inheriting from the extended type", False)],
       "Static method, static (non-nested, non-generic) class, `this` modifier on the first parameter.",
       "Extension methods are syntactic sugar for static calls; they only see public members and are resolved at compile time when "
       "their namespace is imported. Instance methods always win over extensions with the same signature.", tags=["extension-methods"], seconds=45),

    mc("cs-027", T, R, "Advanced", "Cost of reflection", "What is the main performance cost of reflection-based member access?",
       [("Reflection cannot access public members", False),
        ("Runtime metadata lookup and late-bound invocation are much slower than compiled calls", True),
        ("Reflection forces garbage collection", False),
        ("Reflection disables JIT compilation", False)],
       "Metadata lookups and late-bound invocation are slow compared with direct calls.",
       "Cache `MethodInfo`/`PropertyInfo`, or compile delegates/expression trees once and reuse them. Source generators eliminate runtime "
       "reflection entirely and are trim/AOT-friendly (e.g. System.Text.Json source generation).", tags=["reflection", "performance"]),

    q("cs-028", T, F, "WrittenAnswer", "Intermediate", "Boxing and unboxing",
      "Explain **boxing** and **unboxing** in C#. When do they happen, what do they cost, and how can you avoid them?",
      answer="Boxing wraps a value type in a heap object (e.g. assigning int to object); unboxing copies it back with an explicit cast.",
      explanation="**Boxing** allocates an object on the heap and copies the value into it when a value type is converted to `object` or "
                  "an interface it implements (e.g. `object o = 42;`, passing a struct to a non-generic `ArrayList`, calling "
                  "`string.Format(\"{0}\", 42)` in older APIs). **Unboxing** is the explicit cast back (`int i = (int)o;`) which checks "
                  "the type and copies the value.\n\nCosts: a heap allocation, a copy and extra GC pressure — significant in hot loops.\n\n"
                  "Avoid it with generics (`List<int>` instead of `ArrayList`), generic constraints, overloads that take the value type, "
                  "and by implementing `IEquatable<T>` on structs so `Equals(T)` does not box.",
      reference="Boxing converts a value type instance to object or to an interface type it implements by allocating a heap object and "
                "copying the value. Unboxing is the explicit conversion back, which verifies the runtime type and copies the value out. "
                "Boxing costs an allocation and GC work; it happens implicitly with non-generic collections, object parameters and "
                "interface calls on structs. Generics, typed overloads and IEquatable<T> avoid it.",
      tags=["boxing", "performance"], seconds=240),

    q("cs-029", T, A, "ScenarioBased", "Advanced", "Reducing latency of chained service calls",
      "An ASP.NET Core endpoint calls three independent downstream services **sequentially**; each takes ~300 ms. "
      "Describe how you would reduce the endpoint's latency and what failure cases you would handle.",
      answer="Run the calls concurrently with Task.WhenAll, propagate CancellationToken, add timeouts/retries via resilience policies, "
             "and decide how partial failures are handled.",
      explanation="1. Start all three calls without awaiting, then `await Task.WhenAll(...)`: latency drops from ~900 ms to ~300 ms.\n"
                  "2. Use typed `HttpClient`s from `IHttpClientFactory` with timeouts, retries with jitter and a circuit breaker "
                  "(Microsoft.Extensions.Http.Resilience / Polly).\n3. Pass the request's `CancellationToken` so abandoned requests stop work.\n"
                  "4. Decide on partial failure: fail fast, or return degraded data for optional calls (catch per task).\n"
                  "5. Consider caching results that change rarely, and measure with distributed tracing before and after.",
      reference="Execute independent I/O concurrently (Task.WhenAll), use IHttpClientFactory with timeout, retry with backoff and circuit "
                "breaker, flow CancellationToken, handle partial failures explicitly (fallbacks for optional data), cache stable data, "
                "and verify with tracing/metrics.", tags=["async", "performance", "resilience"], seconds=300, premium=True),

    q("cs-030", T, C, "Debugging", "Intermediate", "Modifying a list while iterating",
      "What happens when this code runs, and how would you fix it?",
      [("It removes 2 and 4 successfully", False),
       ("It throws InvalidOperationException: Collection was modified", True, "List<T> enumerators detect modification and throw."),
       ("It removes only 2", False), ("It loops forever", False)],
      "InvalidOperationException; use `list.RemoveAll(n => n % 2 == 0)` or iterate backwards with a for loop.",
      "`List<T>` tracks a version number; modifying the list invalidates active enumerators and the next `MoveNext` throws. Fixes: "
      "`list.RemoveAll(predicate)`, a reverse `for` loop, or build a new list with LINQ (`list.Where(n => n % 2 != 0).ToList()`).",
      code="var list = new List<int> { 1, 2, 3, 4 };\nforeach (var n in list)\n{\n    if (n % 2 == 0) list.Remove(n);\n}",
      lang="csharp", tags=["collections", "debugging"], seconds=75),
]

write("questions/csharp.json", QUESTIONS)
