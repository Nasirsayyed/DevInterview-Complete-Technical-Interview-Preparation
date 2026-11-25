from seedlib import mc, q, write

T = "javascript"
JF, CL, AS, PR, BR = ("JavaScript Fundamentals", "Closures & Scope", "Asynchronous JavaScript", "Prototypes & this", "Browser Internals")
JS = [
    mc("js-001", T, JF, "Beginner", "typeof null", "What does `console.log(typeof null)` print?",
       [("\"null\"", False), ("\"object\"", True), ("\"undefined\"", False), ("It throws a TypeError", False)],
       "\"object\" — a long-standing quirk of the language.",
       "`typeof null === 'object'` dates back to the first JavaScript implementation and was kept for compatibility. Check for null with "
       "`value === null`.", code="console.log(typeof null);", lang="javascript", tags=["types"], seconds=30),

    mc("js-002", T, JF, "Beginner", "== vs ===", "What is the difference between `==` and `===`?",
       [("No difference", False), ("`===` compares without type coercion; `==` converts operands to a common type first", True),
        ("`==` is faster and preferred", False), ("`===` compares only references", False)],
       "Strict equality skips type coercion.",
       "`0 == ''` and `null == undefined` are true, but `0 === ''` is false. Prefer `===`; `== null` is an accepted idiom for checking "
       "both null and undefined.", tags=["equality"], seconds=30),

    mc("js-003", T, JF, "Intermediate", "Floating-point comparison", "What does `console.log(0.1 + 0.2 === 0.3)` print?",
       [("true", False), ("false", True), ("NaN", False), ("It throws", False)],
       "false — 0.1 + 0.2 is 0.30000000000000004.",
       "Numbers are IEEE-754 doubles, and many decimal fractions are not exactly representable. Compare with a tolerance "
       "(`Math.abs(a - b) < Number.EPSILON`) or use integer cents / decimal libraries for money.",
       code="console.log(0.1 + 0.2 === 0.3);", lang="javascript", tags=["numbers"], seconds=35),

    mc("js-004", T, CL, "Intermediate", "var in loops with setTimeout", "What does this print?",
       [("0 1 2", False, "That is the output with let."), ("3 3 3", True), ("undefined ×3", False), ("2 2 2", False)],
       "3 3 3",
       "`var` is function-scoped, so all three callbacks close over the **same** `i`, which is 3 when they run. `let` creates a new "
       "binding per iteration, printing 0 1 2.",
       code="for (var i = 0; i < 3; i++) {\n  setTimeout(() => console.log(i), 0);\n}", lang="javascript",
       tags=["closures", "var-let"], seconds=50),

    mc("js-005", T, CL, "Intermediate", "What is a closure?", "Which statement best describes a closure?",
       [("A function that has finished executing", False),
        ("A function bundled with references to the variables of the scope in which it was created, usable after that scope returns", True),
        ("A private class field", False), ("A way to close browser windows", False)],
       "A function plus its captured lexical environment.",
       "Closures enable data privacy (module pattern), function factories, memoisation and callbacks that remember state. They keep "
       "captured variables alive, which can retain memory unintentionally.", tags=["closures"], seconds=40),

    mc("js-006", T, CL, "Intermediate", "Temporal dead zone", "What happens when code reads a `let` variable before its declaration in the same scope?",
       [("It returns undefined", False), ("It throws a ReferenceError (temporal dead zone)", True),
        ("It returns null", False), ("It reads the global variable", False)],
       "ReferenceError.",
       "`let`, `const` and `class` declarations are hoisted but uninitialised until their declaration runs; access in between throws. "
       "`var` is hoisted and initialised to undefined.", tags=["hoisting", "tdz"], seconds=40),

    mc("js-007", T, AS, "Intermediate", "Microtasks vs macrotasks", "What is the output order?",
       [("A B C D", False), ("A D C B", True), ("A C D B", False), ("A D B C", False)],
       "A D C B",
       "Synchronous code runs first (A, D). Then the microtask queue (promise callbacks) is drained (C) before the next macrotask "
       "(the setTimeout callback, B).",
       code="console.log('A');\nsetTimeout(() => console.log('B'), 0);\nPromise.resolve().then(() => console.log('C'));\nconsole.log('D');",
       lang="javascript", tags=["event-loop", "promises"], seconds=60),

    mc("js-008", T, AS, "Intermediate", "Promise combinators", "Which combinator waits for **all** promises and never rejects?",
       [("Promise.all", False, "Rejects as soon as one promise rejects."), ("Promise.race", False),
        ("Promise.allSettled", True), ("Promise.any", False, "Rejects only if all reject, but resolves on the first success.")],
       "Promise.allSettled.",
       "`allSettled` resolves with `{status: 'fulfilled', value}` / `{status: 'rejected', reason}` objects for each input — ideal when "
       "partial failure is acceptable.", tags=["promises"], seconds=45),

    mc("js-009", T, AS, "Beginner", "Return value of async functions", "An `async` function executes `return 5`. What does calling it return?",
       [("The number 5", False), ("A Promise that resolves to 5", True), ("undefined", False), ("A generator", False)],
       "A Promise resolved with 5.",
       "Async functions always return promises; thrown errors become rejections. Use `await fn()` or `fn().then(...)` to get the value.",
       tags=["async-await"], seconds=30),

    mc("js-010", T, PR, "Intermediate", "Arrow functions and this", "Which statement about arrow functions is true?",
       [("They create their own `this` based on how they are called", False),
        ("They do not have their own `this`; they capture `this` lexically from the enclosing scope", True),
        ("`bind` changes an arrow function's `this`", False), ("They can be used as constructors with new", False)],
       "Arrow functions use lexical `this`.",
       "That makes them ideal for callbacks inside methods, but unsuitable as object methods that need the receiver, and they have no "
       "`arguments` object or `prototype`.", tags=["this", "arrow-functions"], seconds=45),

    mc("js-011", T, PR, "Intermediate", "Prototype chain lookup", "How does JavaScript resolve `obj.method()` when `method` is not an own property of `obj`?",
       [("It throws immediately", False), ("It walks up the prototype chain (`Object.getPrototypeOf`) until it finds the property or reaches null", True),
        ("It searches the global scope", False), ("It calls the constructor again", False)],
       "Prototype chain lookup.",
       "`class` syntax is sugar over prototypes: methods live on `Class.prototype`, shared by all instances. Missing properties resolve "
       "to undefined after reaching `Object.prototype`.", tags=["prototypes"], seconds=40),

    mc("js-012", T, JF, "Beginner", "const objects", "What does `const` guarantee for `const user = { name: 'Ann' }`?",
       [("The object can never be changed", False), ("The binding cannot be reassigned, but the object's properties can still be mutated", True),
        ("The object is deep-frozen", False), ("Nothing", False)],
       "No reassignment; mutation is still allowed.",
       "Use `Object.freeze` for shallow immutability, or immutable update patterns (`{...user, name: 'Bob'}`) as React state requires.",
       tags=["const", "immutability"], seconds=35),

    mc("js-013", T, JF, "Intermediate", "Default sort order", "What does `console.log([10, 1, 3].sort())` print?",
       [("[1, 3, 10]", False), ("[1, 10, 3]", True), ("[10, 3, 1]", False), ("[3, 1, 10]", False)],
       "[1, 10, 3] — the default sort compares strings.",
       "Without a comparator, elements are converted to strings and compared by UTF-16 code units. Use `arr.sort((a, b) => a - b)` for "
       "numbers. `sort` mutates the array; `toSorted()` returns a new one.", code="console.log([10, 1, 3].sort());", lang="javascript",
       tags=["arrays", "sorting"], seconds=40),

    mc("js-014", T, BR, "Intermediate", "Event delegation", "What is event delegation?",
       [("Removing all event listeners", False),
        ("Attaching one listener to a common ancestor and using event bubbling plus event.target to handle events from many children", True),
        ("Dispatching events to a web worker", False), ("Using setTimeout for events", False)],
       "One listener on a parent handles events bubbling from children.",
       "It reduces the number of listeners and automatically covers elements added later. Use `event.target.closest('li')` to find the "
       "relevant element.", tags=["dom", "events"], seconds=40),

    mc("js-015", T, BR, "Intermediate", "Reflow vs repaint", "Which style change typically triggers **layout (reflow)** rather than only a repaint?",
       [("color", False), ("background-color", False), ("width", True), ("outline-color", False)],
       "Changing width changes geometry, forcing layout.",
       "Geometry changes (width, height, margins, font-size, adding DOM nodes) trigger layout, then paint and composite. `transform` and "
       "`opacity` can often be handled by the compositor alone, which is why they are preferred for animations.", tags=["rendering", "performance"], seconds=45),

    mc("js-016", T, BR, "Beginner", "Cookies vs Web Storage", "Which browser storage mechanism is automatically sent to the server with matching HTTP requests?",
       [("localStorage", False), ("sessionStorage", False), ("Cookies", True), ("IndexedDB", False)],
       "Cookies.",
       "Cookies travel with every matching request (keep them small; use HttpOnly, Secure and SameSite for session cookies). "
       "localStorage/sessionStorage are only readable by JavaScript and vulnerable to theft via XSS, so avoid storing tokens there.",
       tags=["storage", "cookies"], seconds=35),

    mc("js-017", T, JF, "Intermediate", "Debounce vs throttle", "Which describes **debouncing** an input handler?",
       [("Run the handler at most once every N ms", False, "That is throttling."),
        ("Delay the handler until N ms have passed without another event", True),
        ("Run the handler twice", False), ("Cancel the event", False)],
       "Debounce waits for a pause; throttle limits frequency.",
       "Debounce suits search-as-you-type (fire after the user stops typing). Throttle suits scroll/resize handlers that should run "
       "periodically during continuous events.", tags=["performance"], seconds=40),

    q("js-018", T, AS, "WrittenAnswer", "Intermediate", "Explain the event loop",
      "Explain how the JavaScript event loop works, including the call stack, the task (macrotask) queue and the microtask queue.",
      answer="Synchronous code runs on the call stack; when it empties, all microtasks run, then one macrotask, repeating (with rendering between tasks in browsers).",
      explanation="JavaScript runs on a single thread with a **call stack**. Async APIs (timers, network, DOM events) are handled by the "
                  "host and queue callbacks when ready. When the stack is empty the event loop: (1) drains the **microtask queue** "
                  "(promise reactions, `queueMicrotask`, MutationObserver) completely; (2) the browser may render; (3) takes the next "
                  "**macrotask** (setTimeout, I/O, UI events) and repeats. Long synchronous work blocks rendering and input — split "
                  "it up or move it to a Web Worker.",
      reference="The call stack executes synchronous code; asynchronous host APIs enqueue callbacks. When the stack is empty the event "
                "loop drains all microtasks (promise callbacks), allows rendering, then processes one macrotask (timers, I/O, events) and "
                "repeats. Microtasks run before the next macrotask, so promise callbacks run before setTimeout(0). Blocking the stack "
                "freezes the UI.", tags=["event-loop"], seconds=240),
]

T = "typescript"
TS = [
    mc("ts-001", T, "Type System", "Beginner", "unknown vs any", "How does `unknown` differ from `any`?",
       [("They are identical", False), ("A value of type unknown must be narrowed (e.g. with typeof) before it can be used; any disables type checking", True),
        ("unknown is only for functions", False), ("any is safer", False)],
       "`unknown` is the type-safe top type.", "Prefer `unknown` for values from JSON, catch clauses or external input, then narrow with "
       "type guards or a schema validator (e.g. zod).", tags=["types"], seconds=40),

    mc("ts-002", T, "Type System", "Intermediate", "interface vs type alias", "Which capability is specific to `interface` declarations?",
       [("Describing object shapes", False), ("Declaration merging: multiple declarations with the same name combine", True),
        ("Union types", False, "Unions require type aliases."), ("Generics", False)],
       "Declaration merging.", "Interfaces can be reopened (useful for augmenting library types); type aliases can express unions, "
       "intersections, mapped and conditional types. For plain object shapes either works.", tags=["interfaces"], seconds=45),

    mc("ts-003", T, "Generics & Utility Types", "Beginner", "Partial<T>", "What does `Partial<User>` produce?",
       [("A type with all User properties required", False), ("A type with all User properties optional", True),
        ("A type with only the first property", False), ("A readonly copy", False)],
       "All properties become optional.", "Related utilities: `Required<T>`, `Readonly<T>`, `Pick<T, K>`, `Omit<T, K>`, "
       "`Record<K, V>`. `Partial` is common for patch/update payloads.", tags=["utility-types"], seconds=30),

    mc("ts-004", T, "Type System", "Intermediate", "Discriminated unions",
       "Given `type Shape = { kind: 'circle'; r: number } | { kind: 'square'; side: number }`, how does TypeScript know `shape.r` exists inside `if (shape.kind === 'circle')`?",
       [("It doesn't; you must cast", False), ("Checking the literal discriminant property narrows the union to the matching member", True),
        ("Because r is declared first", False), ("Runtime reflection", False)],
       "Narrowing on a literal discriminant.", "Add an exhaustive `switch` with a `never` check in the default branch so adding a new "
       "variant produces a compile error where it is not handled.", tags=["narrowing", "unions"], seconds=50),

    mc("ts-005", T, "Generics & Utility Types", "Advanced", "Typed property access",
       "Which signature returns the correctly typed value of a property for any object?",
       [("function get(obj: any, key: string): any", False),
        ("function get<T, K extends keyof T>(obj: T, key: K): T[K]", True),
        ("function get<T>(obj: T, key: string): T", False), ("function get(obj: object, key: keyof object)", False)],
       "Constrain K to keyof T and return the indexed access type T[K].",
       "`get(user, 'age')` then has type `number`, and `get(user, 'nope')` is a compile error.", tags=["generics", "keyof"], seconds=60),
]

T = "react"
RF, RH, RS, RR, RP = ("React Fundamentals", "React Hooks", "State Management & Redux", "React Router", "Performance Optimization")
REACT = [
    mc("re-001", T, RF, "Beginner", "Keys in lists", "Why does React need a stable `key` prop on list items?",
       [("For CSS styling", False), ("So reconciliation can match items between renders and preserve their state and DOM nodes", True),
        ("Keys are required by HTML", False), ("To sort the list", False)],
       "Keys identify items across renders.",
       "Use stable ids from your data. Array indexes as keys break when items are inserted, removed or reordered — state (e.g. input "
       "values) ends up attached to the wrong item.", tags=["lists", "reconciliation"], seconds=40),

    mc("re-002", T, RF, "Beginner", "Props vs state", "Which statement is correct?",
       [("Components modify their own props", False),
        ("Props are inputs passed by the parent and read-only for the child; state is owned and updated by the component", True),
        ("State is shared by all components automatically", False), ("Props trigger no re-render", False)],
       "Props flow down and are read-only; state is local and updated via setters.",
       "Lift state up to the closest common ancestor when siblings need it, or use context/a store for widely shared state.",
       tags=["props", "state"], seconds=35),

    mc("re-003", T, RH, "Intermediate", "Rules of Hooks", "Which is a rule of Hooks?",
       [("Hooks may be called inside loops", False), ("Call Hooks only at the top level of React function components or custom Hooks, never conditionally", True),
        ("Hooks must be called inside class components", False), ("Each Hook can be used only once per app", False)],
       "Top level only, same order every render.",
       "React identifies hook state by call order. A conditional hook shifts the order and corrupts state; the eslint-plugin-react-hooks "
       "rules enforce this.", tags=["hooks"], seconds=40),

    mc("re-004", T, RH, "Intermediate", "useEffect with an empty dependency array",
       "When does `useEffect(() => { ...; return cleanup; }, [])` run?",
       [("Before every render", False), ("After the first render (mount); cleanup runs on unmount", True),
        ("Only when props change", False), ("Never", False)],
       "Once after mount, cleanup on unmount.",
       "In development StrictMode, React mounts, unmounts and remounts components once to surface missing cleanup, so effects run twice. "
       "Effects run after paint; use useLayoutEffect for synchronous DOM measurement.", tags=["useEffect"], seconds=45),

    mc("re-005", T, RH, "Intermediate", "Batched state updates", "Starting from 0, what is `count` after one click?",
       [("3", False, "That would require functional updates."), ("1", True), ("0", False), ("It throws", False)],
       "1",
       "All three calls read the same `count` (0) from this render's closure, so each schedules `setCount(1)`; React batches them. Use the "
       "updater form `setCount(c => c + 1)` to apply each update to the latest value (result 3).",
       code="const [count, setCount] = useState(0);\n\nfunction handleClick() {\n  setCount(count + 1);\n  setCount(count + 1);\n  setCount(count + 1);\n}",
       lang="javascript", tags=["useState", "batching"], seconds=60),

    mc("re-006", T, RH, "Intermediate", "useMemo vs useCallback", "What does `useCallback(fn, deps)` memoise?",
       [("The result of calling fn", False, "That is useMemo."), ("The function instance itself, keeping its identity stable between renders", True),
        ("The component", False), ("Network requests", False)],
       "The function reference.",
       "`useCallback(fn, deps)` equals `useMemo(() => fn, deps)`. It matters when passing callbacks to memoised children or using them as "
       "effect dependencies; otherwise it adds overhead without benefit.", tags=["useCallback", "useMemo"], seconds=40),

    mc("re-007", T, RH, "Intermediate", "useRef", "What is `useRef` commonly used for?",
       [("Triggering re-renders", False),
        ("Holding a mutable value that persists across renders without causing re-renders (and referencing DOM nodes)", True),
        ("Fetching data", False), ("Global state", False)],
       "Persistent mutable box, no re-render on change.", "Examples: DOM refs for focus, storing interval ids, keeping the latest value "
       "for callbacks. Don't read refs during render to compute output.", tags=["useRef"], seconds=40),

    mc("re-008", T, RH, "Advanced", "Stale closures in effects",
       "An interval created in `useEffect(() => { const id = setInterval(() => console.log(count), 1000); return () => clearInterval(id); }, [])` always logs 0. Why?",
       [("setInterval is broken in React", False),
        ("The callback closed over `count` from the first render; the effect never re-runs. Use a functional update, a ref, or add count to the dependencies", True),
        ("console.log is batched", False), ("StrictMode disables intervals", False)],
       "Stale closure over the initial render's state.", "Each render has its own props/state snapshot. Keep dependency arrays honest; "
       "for intervals updating state, `setCount(c => c + 1)` avoids needing `count` in the closure.", tags=["closures", "useEffect"], seconds=75),

    mc("re-009", T, RS, "Intermediate", "Redux reducers", "Which is a requirement for a Redux reducer?",
       [("It may call APIs", False), ("It must be a pure function that computes new state from the previous state and an action without mutating it", True),
        ("It must be async", False), ("It must update the DOM", False)],
       "Pure, no side effects, no mutation.",
       "Redux Toolkit's `createSlice` lets you write 'mutating' code because Immer produces immutable updates under the hood. Side effects "
       "belong in thunks, listeners or RTK Query.", tags=["redux"], seconds=45),

    mc("re-010", T, RS, "Intermediate", "Context and re-renders", "What happens when a Context provider's `value` changes?",
       [("Nothing re-renders", False), ("Every component that consumes that context re-renders", True),
        ("Only the provider re-renders", False), ("The whole app remounts", False)],
       "All consumers re-render.",
       "Passing a new object literal each render (`value={{ user, setUser }}`) re-renders consumers every time; memoise the value, split "
       "contexts by update frequency, or use a store with selectors for high-frequency state.", tags=["context"], seconds=45),

    mc("re-011", T, RR, "Beginner", "Link vs anchor", "Why use React Router's `<Link>` instead of `<a href>` for internal navigation?",
       [("Links are styled automatically", False), ("It performs client-side navigation via the History API without a full page reload", True),
        ("Anchors are not allowed in React", False), ("Links work offline", False)],
       "Client-side routing without reloading.", "A full reload loses in-memory state and re-downloads the app. `<NavLink>` adds active styling.",
       tags=["routing"], seconds=30),

    mc("re-012", T, RR, "Intermediate", "Reading route parameters", "With the route `/users/:userId`, how does a component read `userId`?",
       [("props.match.userId in all versions", False), ("const { userId } = useParams()", True),
        ("window.userId", False), ("useLocation().userId", False)],
       "useParams().", "`useSearchParams` reads query strings, `useNavigate` navigates programmatically, and data routers offer "
       "loaders/actions for route-level data fetching.", tags=["routing", "hooks"], seconds=35),

    mc("re-013", T, RP, "Intermediate", "React.memo", "What does wrapping a component in `React.memo` do?",
       [("Caches API responses", False), ("Skips re-rendering when its props are shallowly equal to the previous props", True),
        ("Makes state immutable", False), ("Moves rendering to a worker", False)],
       "Skips renders for unchanged props.", "It only helps if props are referentially stable — combine with useMemo/useCallback for "
       "object and function props, and measure with the React Profiler before optimising.", tags=["memo", "performance"], seconds=40),

    mc("re-014", T, RP, "Advanced", "Rendering very large lists", "A page must display 20,000 rows smoothly. What is the most effective technique?",
       [("Wrap each row in React.memo only", False), ("Virtualisation/windowing: render only the rows visible in the viewport (e.g. react-window)", True),
        ("Use index keys", False), ("Render everything inside useEffect", False)],
       "Virtualise the list.", "Windowing keeps the DOM small regardless of data size. Combine with pagination or infinite loading for "
       "data fetching.", tags=["virtualization", "performance"], seconds=45),

    mc("re-015", T, RF, "Intermediate", "Controlled components", "What makes an `<input>` a controlled component?",
       [("It has an id", False), ("Its value is driven by React state via the value prop and updated through onChange", True),
        ("It is inside a form", False), ("It uses useRef", False, "Reading values via refs is the uncontrolled approach.")],
       "React state is the single source of truth for its value.", "Controlled inputs enable instant validation and formatting; "
       "uncontrolled inputs (refs/defaultValue) can be simpler for large forms; libraries like React Hook Form mix both.",
       tags=["forms"], seconds=40),

    q("re-016", T, RF, "WrittenAnswer", "Intermediate", "How React updates the UI",
      "Explain what happens when state changes in a React component, from the state update to the DOM change.",
      answer="React schedules a re-render, calls the component to produce new elements, reconciles them against the previous tree, and commits only the differences to the DOM.",
      explanation="1. `setState` schedules an update (batched).\n2. **Render phase:** React calls the component (and children not bailed out) "
                  "to get a new element tree — pure computation.\n3. **Reconciliation:** it diffs new vs previous elements by type and "
                  "key; same type keeps the instance and state, different type remounts.\n4. **Commit phase:** it applies the minimal DOM "
                  "mutations, then runs layout effects and, after paint, passive effects (useEffect).\nWith concurrent rendering, the "
                  "render phase can be interrupted for urgent updates.",
      reference="State updates are batched and schedule a render; React re-invokes components to produce a new element tree, reconciles "
                "it with the previous tree using element type and keys, then commits minimal DOM changes and runs effects. Rendering is "
                "pure and may be interrupted in concurrent mode; commits are synchronous.", tags=["reconciliation", "virtual-dom"], seconds=240),
]

T = "html"
HTML = [
    mc("ht-001", T, "Semantics & Accessibility", "Beginner", "Navigation landmark", "Which element should wrap a site's main navigation links?",
       [("<div class=\"nav\">", False), ("<nav>", True), ("<menu>", False), ("<header>", False)],
       "<nav>.", "Semantic elements (`<header>`, `<nav>`, `<main>`, `<article>`, `<footer>`) create landmarks that screen-reader users "
       "can jump between.", tags=["semantics"], seconds=25),

    mc("ht-002", T, "Semantics & Accessibility", "Intermediate", "alt text for decorative images", "How should a purely decorative image be marked up?",
       [("Omit the alt attribute", False, "Missing alt makes screen readers announce the file name."), ("alt=\"\"", True),
        ("alt=\"image\"", False), ("alt=\"decorative\"", False)],
       "An empty alt attribute.", "Informative images need concise alt text describing their purpose; decorative ones use alt=\"\" (or CSS "
       "backgrounds) so assistive tech skips them.", tags=["accessibility", "images"], seconds=35),

    mc("ht-003", T, "Semantics & Accessibility", "Intermediate", "button vs clickable div", "Why prefer `<button>` over `<div onclick>` for actions?",
       [("Buttons load faster", False), ("Buttons are focusable, keyboard-operable (Enter/Space) and expose the button role to assistive technology by default", True),
        ("Divs cannot have click handlers", False), ("No difference", False)],
       "Native semantics and keyboard support for free.", "A div needs role=\"button\", tabindex=\"0\" and key handlers to approximate it. "
       "Remember buttons inside forms default to type=\"submit\".", tags=["accessibility"], seconds=40),

    mc("ht-004", T, "Forms & Media", "Beginner", "Associating labels", "How do you associate a `<label>` with an `<input id=\"email\">`?",
       [("<label name=\"email\">", False), ("<label for=\"email\">", True), ("<label id=\"email\">", False), ("Place the label after the input", False)],
       "Use the for attribute (or wrap the input in the label).", "Clicking the label focuses the input and screen readers announce the "
       "label. Placeholders are not a substitute for labels.", tags=["forms", "accessibility"], seconds=30),

    mc("ht-005", T, "Forms & Media", "Beginner", "input type=email", "What does `<input type=\"email\">` provide?",
       [("Server-side validation", False), ("Built-in format validation and an email-optimised keyboard on mobile devices", True),
        ("Automatic email sending", False), ("Encryption", False)],
       "Client-side validation and better mobile keyboards.", "Client-side validation is a UX feature only; always validate on the server too.",
       tags=["forms"], seconds=30),

    mc("ht-006", T, "Semantics & Accessibility", "Intermediate", "defer vs async scripts", "How does `<script defer>` differ from `<script async>`?",
       [("No difference", False), ("defer scripts execute in document order after parsing completes; async scripts execute as soon as they download, in any order", True),
        ("async blocks parsing", False), ("defer only works for inline scripts", False)],
       "defer = ordered, after parsing; async = ASAP, unordered.", "Both download in parallel without blocking parsing. Use defer for app "
       "code with dependencies and async for independent scripts like analytics. Module scripts are deferred by default.", tags=["performance", "scripts"], seconds=45),

    mc("ht-007", T, "Semantics & Accessibility", "Advanced", "First rule of ARIA", "What is the 'first rule' of ARIA use?",
       [("Add role attributes to every element", False), ("Use a native HTML element with the required semantics and behaviour instead of repurposing an element with ARIA whenever possible", True),
        ("ARIA replaces the need for labels", False), ("aria-hidden on all decorative text", False)],
       "Prefer native HTML; ARIA only adds semantics, not behaviour.", "Incorrect ARIA is worse than none. Test with a screen reader and "
       "keyboard-only navigation.", tags=["aria", "accessibility"], seconds=45),
]

T = "css"
CSS = [
    mc("css-001", T, "Cascade & Specificity", "Intermediate", "Specificity", "Which selector has the highest specificity?",
       [("div p", False), (".card .title", False), ("#header", True), ("ul li a", False)],
       "#header — an ID selector (1,0,0).", "Specificity is compared as (IDs, classes/attributes/pseudo-classes, elements). `.card .title` "
       "is (0,2,0), `ul li a` is (0,0,3). Inline styles and !important override normal rules; `:where()` has zero specificity.",
       tags=["specificity"], seconds=40),

    mc("css-002", T, "Cascade & Specificity", "Beginner", "box-sizing: border-box", "With `box-sizing: border-box`, what does `width: 200px` include?",
       [("Only the content", False), ("Content, padding and border (but not margin)", True), ("Content and margin", False), ("Everything including margin", False)],
       "Content + padding + border.", "The default `content-box` adds padding and border on top of width, which makes layouts harder to "
       "reason about; many resets apply border-box globally.", tags=["box-model"], seconds=30),

    mc("css-003", T, "Layout: Flexbox & Grid", "Intermediate", "Flexbox vs Grid", "When is CSS Grid generally a better fit than Flexbox?",
       [("For one-dimensional alignment of items in a row", False), ("For two-dimensional layouts that control rows and columns together", True),
        ("Grid does not support responsive design", False), ("Never; Flexbox replaced Grid", False)],
       "Grid is two-dimensional; Flexbox is one-dimensional.", "They complement each other: Grid for page/section layout, Flexbox for "
       "distributing items within a component (toolbars, button groups).", tags=["grid", "flexbox"], seconds=40),

    mc("css-004", T, "Layout: Flexbox & Grid", "Beginner", "Centering with Flexbox", "Which declarations on the parent centre a child horizontally and vertically?",
       [("text-align: center; vertical-align: middle", False), ("display: flex; justify-content: center; align-items: center", True),
        ("margin: auto on the parent", False), ("position: center", False)],
       "Flex container with justify-content and align-items set to center.", "With Grid: `display: grid; place-items: center;`. The parent "
       "needs a height for vertical centring to be visible.", tags=["flexbox"], seconds=30),

    mc("css-005", T, "Layout: Flexbox & Grid", "Intermediate", "Absolute positioning", "An element with `position: absolute` is positioned relative to…",
       [("The viewport, always", False), ("Its nearest positioned ancestor (position other than static), or the initial containing block if none", True),
        ("Its previous sibling", False), ("The body element's padding box", False)],
       "The nearest positioned ancestor.", "Absolutely positioned elements leave normal flow. `position: fixed` is relative to the viewport "
       "(unless an ancestor has a transform); `sticky` toggles between relative and fixed.", tags=["positioning"], seconds=40),

    mc("css-006", T, "Responsive Design", "Beginner", "Mobile-first media queries", "Which media query pattern represents a mobile-first approach?",
       [("Base styles for desktop with max-width overrides", False), ("Base styles for small screens, then @media (min-width: …) overrides for larger screens", True),
        ("Separate stylesheets per device model", False), ("Only using px units", False)],
       "Min-width breakpoints layered on mobile base styles.", "Mobile-first ships less CSS to small devices and progressively enhances. "
       "Container queries let components adapt to their container instead of the viewport.", tags=["responsive"], seconds=35),

    mc("css-007", T, "Responsive Design", "Intermediate", "rem vs em", "What is a `rem` relative to?",
       [("The parent element's font-size", False, "That is em (for font-size)."), ("The root (html) element's font-size", True),
        ("The viewport width", False), ("The element's line-height", False)],
       "The root font size.", "rem gives predictable sizing that respects user font preferences; em compounds through nested elements.",
       tags=["units"], seconds=30),

    mc("css-008", T, "Cascade & Specificity", "Advanced", "z-index and stacking contexts",
       "A modal with `z-index: 9999` still appears below a header with `z-index: 10`. What is the most likely cause?",
       [("z-index maximum is 1000", False), ("The modal is inside an ancestor that creates its own stacking context (e.g. transform, opacity < 1, or positioned with z-index) that is below the header", True),
        ("z-index only works with display: flex", False), ("The header uses !important", False)],
       "z-index only competes within the same stacking context.", "Properties such as `transform`, `filter`, `opacity < 1`, "
       "`position: fixed`, `isolation: isolate` and positioned elements with z-index create stacking contexts. Render modals in a portal at "
       "the document root.", tags=["z-index", "stacking-context"], seconds=60),

    mc("css-009", T, "Layout: Flexbox & Grid", "Intermediate", "Margin collapsing",
       "Two stacked block elements have `margin-bottom: 20px` and `margin-top: 30px`. What is the vertical gap between them?",
       [("50px", False), ("30px", True), ("20px", False), ("0px", False)],
       "30px — adjacent vertical margins collapse to the larger one.", "Collapsing does not happen in flex or grid containers, with padding "
       "or borders in between, or for horizontal margins. Using `gap` in flex/grid layouts avoids the surprise.", tags=["box-model"], seconds=40),
]

write("questions/javascript.json", JS)
write("questions/typescript.json", TS)
write("questions/react.json", REACT)
write("questions/html.json", HTML)
write("questions/css.json", CSS)
