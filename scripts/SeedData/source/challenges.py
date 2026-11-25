"""Coding challenges. Expected outputs are produced by the reference solutions below, so test data is correct by construction."""
import heapq
import random
import sqlite3
from collections import deque
from functools import lru_cache

from seedlib import write

rng = random.Random(20251125)

STARTER = {
    "python": "import sys\n\n\ndef main():\n    data = sys.stdin.read().split()\n    # Parse the input as described in the problem and print the answer.\n\n\nif __name__ == \"__main__\":\n    main()\n",
    "javascript": "const data = require('fs').readFileSync(0, 'utf8').trim().split(/\\s+/);\n// Parse the input as described in the problem and print the answer with console.log.\n",
    "csharp": "using System;\nusing System.Collections.Generic;\nusing System.Linq;\n\npublic static class Program\n{\n    public static void Main()\n    {\n        var input = Console.In.ReadToEnd();\n        // Parse the input as described in the problem and write the answer with Console.WriteLine.\n    }\n}\n",
    "java": "import java.io.*;\nimport java.util.*;\n\npublic class Main {\n    public static void main(String[] args) throws IOException {\n        BufferedReader in = new BufferedReader(new InputStreamReader(System.in));\n        // Parse the input as described in the problem and print the answer with System.out.println.\n    }\n}\n",
    "cpp": "#include <bits/stdc++.h>\nusing namespace std;\n\nint main() {\n    ios::sync_with_stdio(false);\n    cin.tie(nullptr);\n    // Parse the input as described in the problem and print the answer with cout.\n    return 0;\n}\n",
    "kotlin": "fun main() {\n    val input = generateSequence(::readLine).joinToString(\"\\n\")\n    // Parse the input as described in the problem and print the answer with println.\n}\n",
}
SQL_STARTER = {"sql": "-- Write a single SELECT statement. Tables are described in the problem.\nSELECT 1;\n"}


def challenge(key, title, category, difficulty, statement, input_desc, output_desc, constraints, time_c, space_c, hints,
              solution, solve, inputs, samples=2, premium=False, why=None, sql=False, time_limit=2000):
    tests = []
    for i, text in enumerate(inputs):
        out = solve(text)
        tests.append({"in": text, "out": out, "sample": i < samples, "why": (why or {}).get(i)})
    return {
        "key": key, "title": title, "category": category, "difficulty": difficulty, "statement": statement, "input": input_desc,
        "output": output_desc, "constraints": constraints, "timeComplexity": time_c, "spaceComplexity": space_c, "hints": hints,
        "solution": solution, "premium": premium, "timeLimitMs": time_limit, "memoryLimitMb": 256, "tests": tests,
        "starter": SQL_STARTER if sql else STARTER,
    }


def lines(*parts):
    return "\n".join(str(p) for p in parts) + "\n"


def arr(values):
    return " ".join(str(v) for v in values)


# ---------- reference solutions ----------
def two_sum(t):
    tok = t.split(); n = int(tok[0]); a = list(map(int, tok[1:1 + n])); target = int(tok[1 + n])
    seen = {}
    for i, v in enumerate(a):
        if target - v in seen:
            return f"{seen[target - v]} {i}"
        seen.setdefault(v, i)
    return "-1 -1"


def two_sum_case(n):
    a = [rng.randint(-1000, 1000) for _ in range(n)]
    i, j = sorted(rng.sample(range(n), 2))
    target = a[i] + a[j]
    # make the pair unique by nudging other values that could also form the target
    seen = set()
    for k in range(n):
        if k in (i, j):
            continue
        while (target - a[k]) in [a[x] for x in range(n) if x != k] or a[k] in seen:
            a[k] = rng.randint(2000, 9000)
        seen.add(a[k])
    return lines(n, arr(a), target)


def reverse_words(t):
    return " ".join(t.strip().split()[::-1])


def is_palindrome(t):
    s = [c.lower() for c in t.strip("\n") if c.isalnum()]
    return "true" if s == s[::-1] else "false"


def valid_parens(t):
    pairs = {")": "(", "]": "[", "}": "{"}
    st = []
    for c in t.strip():
        if c in "([{":
            st.append(c)
        elif c in pairs:
            if not st or st.pop() != pairs[c]:
                return "false"
    return "true" if not st else "false"


def reverse_list(t):
    tok = t.split(); n = int(tok[0])
    return arr(tok[1:1 + n][::-1]) if n else "empty"


def merge_lists(t):
    rows = t.strip("\n").split("\n")
    a = list(map(int, rows[1].split())) if int(rows[0]) else []
    b = list(map(int, rows[3].split())) if int(rows[2]) else []
    merged = sorted(a + b)
    return arr(merged) if merged else "empty"


def queue_ops(t):
    rows = t.strip().split("\n")[1:]
    q, out = deque(), []
    for r in rows:
        p = r.split()
        if p[0] == "push":
            q.append(int(p[1]))
        elif p[0] == "pop":
            out.append(str(q.popleft()) if q else "-1")
        elif p[0] == "peek":
            out.append(str(q[0]) if q else "-1")
        else:
            out.append("true" if not q else "false")
    return "\n".join(out)


def level_order(t):
    tok = t.split()
    if not tok or tok[0] == "null":
        return "empty"
    vals = [None if v == "null" else int(v) for v in tok]
    nodes = [[v, None, None] if v is not None else None for v in vals]
    kids = deque(nodes[1:])
    for node in nodes:
        if node is None:
            continue
        if kids: node[1] = kids.popleft()
        if kids: node[2] = kids.popleft()
    res, level = [], [nodes[0]]
    while level:
        res.append(arr([n[0] for n in level]))
        level = [c for n in level for c in (n[1], n[2]) if c is not None]
    return "\n".join(res)


def random_tree(size):
    vals = [str(rng.randint(1, 99))]
    slots = 2
    while len([v for v in vals if v != "null"]) < size:
        vals.append(str(rng.randint(1, 99)) if rng.random() < 0.75 else "null")
    while vals[-1] == "null":
        vals.pop()
    return " ".join(vals) + "\n"


def islands(t):
    rows = t.strip().split("\n"); r, c = map(int, rows[0].split()); g = [list(x) for x in rows[1:1 + r]]
    count = 0
    for i in range(r):
        for j in range(c):
            if g[i][j] == "1":
                count += 1
                stack = [(i, j)]; g[i][j] = "0"
                while stack:
                    x, y = stack.pop()
                    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                        nx, ny = x + dx, y + dy
                        if 0 <= nx < r and 0 <= ny < c and g[nx][ny] == "1":
                            g[nx][ny] = "0"; stack.append((nx, ny))
    return str(count)


def grid(r, c, p):
    return lines(f"{r} {c}", *["".join("1" if rng.random() < p else "0" for _ in range(c)) for _ in range(r)])


def first_occurrence(t):
    tok = t.split(); n = int(tok[0]); a = list(map(int, tok[1:1 + n])); x = int(tok[1 + n])
    lo, hi, ans = 0, n - 1, -1
    while lo <= hi:
        mid = (lo + hi) // 2
        if a[mid] >= x:
            if a[mid] == x: ans = mid
            hi = mid - 1
        else:
            lo = mid + 1
    return str(ans)


def merge_intervals(t):
    tok = t.split(); n = int(tok[0]); iv = sorted((int(tok[1 + 2 * i]), int(tok[2 + 2 * i])) for i in range(n))
    out = []
    for s, e in iv:
        if out and s <= out[-1][1]:
            out[-1][1] = max(out[-1][1], e)
        else:
            out.append([s, e])
    return "\n".join(f"{s} {e}" for s, e in out)


def gen_parens(t):
    n = int(t.strip()); res = []
    def go(s, o, c):
        if len(s) == 2 * n: res.append(s); return
        if o < n: go(s + "(", o + 1, c)
        if c < o: go(s + ")", o, c + 1)
    go("", 0, 0)
    return "\n".join(sorted(res))


def climb(t):
    n = int(t.strip()); a, b = 1, 1
    for _ in range(n): a, b = b, a + b
    return str(a)


def lcs(t):
    x, y = t.strip("\n").split("\n")[:2]
    dp = [[0] * (len(y) + 1) for _ in range(len(x) + 1)]
    for i in range(1, len(x) + 1):
        for j in range(1, len(y) + 1):
            dp[i][j] = dp[i - 1][j - 1] + 1 if x[i - 1] == y[j - 1] else max(dp[i - 1][j], dp[i][j - 1])
    return str(dp[-1][-1])


def edit_distance(t):
    x, y = t.strip("\n").split("\n")[:2]
    prev = list(range(len(y) + 1))
    for i in range(1, len(x) + 1):
        cur = [i] + [0] * len(y)
        for j in range(1, len(y) + 1):
            cur[j] = prev[j - 1] if x[i - 1] == y[j - 1] else 1 + min(prev[j - 1], prev[j], cur[j - 1])
        prev = cur
    return str(prev[-1])


def first_unique(t):
    s = t.strip(); counts = {}
    for c in s: counts[c] = counts.get(c, 0) + 1
    for i, c in enumerate(s):
        if counts[c] == 1: return str(i)
    return "-1"


def group_anagrams(t):
    tok = t.split(); n = int(tok[0]); words = tok[1:1 + n]; groups = {}
    for w in words: groups.setdefault("".join(sorted(w)), []).append(w)
    out = sorted(" ".join(sorted(g)) for g in groups.values())
    return "\n".join(out)


def count_primes(t):
    n = int(t.strip())
    if n < 3: return "0"
    sieve = bytearray([1]) * n; sieve[0] = sieve[1] = 0
    for i in range(2, int(n ** 0.5) + 1):
        if sieve[i]: sieve[i * i::i] = bytearray(len(range(i * i, n, i)))
    return str(sum(sieve))


def sliding_max(t):
    tok = t.split(); n, k = int(tok[0]), int(tok[1]); a = list(map(int, tok[2:2 + n]))
    dq, out = deque(), []
    for i, v in enumerate(a):
        while dq and a[dq[-1]] <= v: dq.pop()
        dq.append(i)
        if dq[0] <= i - k: dq.popleft()
        if i >= k - 1: out.append(a[dq[0]])
    return arr(out)


def dijkstra(t):
    rows = t.strip().split("\n"); n, m = map(int, rows[0].split()); adj = [[] for _ in range(n + 1)]
    for r in rows[1:1 + m]:
        u, v, w = map(int, r.split()); adj[u].append((v, w)); adj[v].append((u, w))
    dist = [None] * (n + 1); dist[1] = 0; pq = [(0, 1)]
    while pq:
        d, u = heapq.heappop(pq)
        if d > dist[u]: continue
        for v, w in adj[u]:
            if dist[v] is None or d + w < dist[v]:
                dist[v] = d + w; heapq.heappush(pq, (dist[v], v))
    return arr(-1 if d is None else d for d in dist[1:])


def graph_case(n, m):
    edges = set()
    while len(edges) < m:
        u, v = rng.randint(1, n), rng.randint(1, n)
        if u != v: edges.add((min(u, v), max(u, v)))
    return lines(f"{n} {m}", *[f"{u} {v} {rng.randint(1, 100)}" for u, v in edges])


def sql_solver(query):
    def solve(setup):
        conn = sqlite3.connect(":memory:"); conn.executescript(setup)
        rows = conn.execute(query).fetchall()
        def fmt(v):
            if v is None: return "NULL"
            if isinstance(v, float): return str(int(v)) if v.is_integer() else f"{v:.2f}"
            return str(v)
        return "\n".join("|".join(fmt(v) for v in r) for r in rows)
    return solve


EMP_SCHEMA = "CREATE TABLE Employees (Id INTEGER PRIMARY KEY, Name TEXT, Salary INTEGER, DepartmentId INTEGER);\n"
DEPT_SCHEMA = "CREATE TABLE Departments (Id INTEGER PRIMARY KEY, Name TEXT);\n"


def emp_inserts(rows):
    return "".join(f"INSERT INTO Employees VALUES ({i}, '{n}', {s}, {d});\n" for i, n, s, d in rows)


CHALLENGES = [
    challenge("ch-two-sum", "Two Sum", "Arrays", "Easy",
              "Given an array of integers and a target, return the indices of the **two distinct elements** whose sum equals the target. "
              "Exactly one valid pair exists.",
              "Line 1: n. Line 2: n space-separated integers. Line 3: the target.",
              "Two indices i and j (0-based, i < j) separated by a space.",
              "2 ≤ n ≤ 10^5; −10^9 ≤ values, target ≤ 10^9", "O(n)", "O(n)",
              ["A brute-force double loop is O(n²). Can you remember values you've already seen?",
               "For each value v, check whether target − v was seen before, using a hash map from value to index."],
              "Scan once, storing each value's index in a hash map. For element i, if `target - a[i]` is in the map, print the stored index and i. "
              "This is O(n) time and O(n) space, versus O(n²) for checking every pair.",
              two_sum, [lines(4, "2 7 11 15", 9), lines(5, "3 -1 4 8 6", 7), lines(2, "5 5", 10), lines(6, "1 -2 3 -4 5 -6", -10),
                        two_sum_case(20), two_sum_case(200)],
              why={0: "2 + 7 = 9 at indices 0 and 1.", 1: "-1 + 8 = 7."}),

    challenge("ch-reverse-words", "Reverse Words", "Strings", "Easy",
              "Reverse the order of the words in a sentence. Words are separated by one or more spaces; the output must use single spaces "
              "with no leading or trailing spaces.",
              "A single line of text.", "The words in reverse order separated by single spaces.", "1 ≤ length ≤ 10^4",
              "O(n)", "O(n)", ["Split on whitespace and ignore empty tokens.", "Reverse the list of words and join with one space."],
              "Split the line into tokens (collapsing repeated spaces), reverse the token list and join with a single space. In-place "
              "variants reverse the whole string and then each word.",
              reverse_words, ["the sky is blue\n", "  hello   world  \n", "single\n", "a b c d e\n", "DevInterview  helps   you prepare\n"],
              why={1: "Extra spaces are collapsed."}),

    challenge("ch-valid-palindrome", "Valid Palindrome", "Strings", "Easy",
              "Determine whether a string is a palindrome considering only **alphanumeric** characters and ignoring case.",
              "A single line of text (may contain spaces and punctuation).", "`true` or `false`.", "1 ≤ length ≤ 2·10^5",
              "O(n)", "O(1)", ["Skip characters that are not letters or digits.", "Compare characters from both ends with two pointers."],
              "Use two pointers from both ends, skipping non-alphanumeric characters and comparing lowercase versions. Stop when they meet.",
              is_palindrome, ["A man, a plan, a canal: Panama\n", "race a car\n", " \n", "No 'x' in Nixon\n", "0P\n", "abba\n", "ab_a\n"]),

    challenge("ch-valid-parentheses", "Valid Parentheses", "Stacks", "Easy",
              "Given a string containing only the characters `()[]{}`, determine whether every bracket is closed by the same type in the correct order.",
              "A single line containing the brackets.", "`true` or `false`.", "1 ≤ length ≤ 10^4", "O(n)", "O(n)",
              ["Which bracket must close next? The most recently opened one.", "Push opening brackets on a stack and pop when you see a closing one."],
              "Push every opening bracket. For a closing bracket, the stack must be non-empty and its top must be the matching opener. "
              "At the end the stack must be empty.",
              valid_parens, ["()[]{}\n", "(]\n", "([{}])\n", "((\n", "){\n", "{[()()]}[]\n", "([)]\n"]),

    challenge("ch-reverse-list", "Reverse a Linked List", "LinkedLists", "Easy",
              "The values of a singly linked list are given in order. Build the list, reverse it **in place** by re-linking nodes, and print the values of the reversed list.",
              "Line 1: n (number of nodes). Line 2: n integers (omitted when n = 0).", "The reversed values separated by spaces, or `empty` if the list is empty.",
              "0 ≤ n ≤ 5000", "O(n)", "O(1)", ["Keep three references: previous, current and next.", "Point current.next to previous, then advance."],
              "Iterate with `prev = null, cur = head`: save `next = cur.next`, set `cur.next = prev`, move `prev = cur`, `cur = next`. When cur is null, prev is the new head.",
              reverse_list, [lines(5, "1 2 3 4 5"), lines(2, "1 2"), lines(0), lines(1, "42"), lines(8, arr(range(10, 18)))]),

    challenge("ch-merge-sorted-lists", "Merge Two Sorted Lists", "LinkedLists", "Easy",
              "Merge two sorted linked lists into one sorted list by splicing their nodes together.",
              "Line 1: n. Line 2: n sorted integers (or empty). Line 3: m. Line 4: m sorted integers (or empty).",
              "The merged values separated by spaces, or `empty`.", "0 ≤ n, m ≤ 5000", "O(n + m)", "O(1)",
              ["Use a dummy head node to simplify edge cases.", "Repeatedly attach the smaller current node."],
              "With a dummy node and a tail pointer, attach the smaller of the two current nodes and advance that list. Append the remainder at the end.",
              merge_lists, ["3\n1 2 4\n3\n1 3 4\n", "0\n\n0\n\n", "0\n\n1\n0\n", "4\n-5 0 3 9\n2\n-6 10\n", "5\n1 1 1 1 1\n3\n1 1 2\n"]),

    challenge("ch-queue-two-stacks", "Queue Using Two Stacks", "Queues", "Medium",
              "Implement a FIFO queue using only two stacks. Process the operations and print the result of each `pop`, `peek` and `empty`.",
              "Line 1: q (number of operations). Next q lines: `push x`, `pop`, `peek` or `empty`.",
              "For each pop/peek, the value (or -1 if the queue is empty); for each empty, `true` or `false`. One result per line.",
              "1 ≤ q ≤ 10^5", "O(1) amortised per operation", "O(n)",
              ["Use one stack for incoming items and one for outgoing items.", "Only move items from the input stack to the output stack when the output stack is empty."],
              "Push onto `in`. For pop/peek, if `out` is empty, pop everything from `in` into `out` (reversing order), then use `out`'s top. "
              "Each element moves at most once, giving amortised O(1).",
              queue_ops, ["5\npush 1\npush 2\npeek\npop\nempty\n", "4\npop\npush 7\npop\nempty\n",
                          "8\npush 3\npush 4\npop\npush 5\npop\npop\npop\nempty\n"]),

    challenge("ch-level-order", "Binary Tree Level Order Traversal", "Trees", "Medium",
              "Given a binary tree in level-order form (with `null` for missing children), print its values level by level.",
              "A single line with node values in level order; `null` marks a missing child.",
              "One line per level with the values from left to right, or `empty` for an empty tree.", "0 ≤ nodes ≤ 2000",
              "O(n)", "O(w) where w is the maximum width", ["Breadth-first search processes nodes level by level.", "Record the queue size at the start of each level."],
              "Use a queue. For each level, dequeue exactly the number of nodes present at the start of the level, print them, and enqueue their children.",
              level_order, ["3 9 20 null null 15 7\n", "1\n", "null\n", "1 2 3 4 null null 5\n", random_tree(15), random_tree(40)]),

    challenge("ch-number-of-islands", "Number of Islands", "Graphs", "Medium",
              "Given a grid of `1` (land) and `0` (water), count the islands. An island is formed by horizontally or vertically adjacent land cells.",
              "Line 1: rows and columns. Next lines: each row as a string of 0/1.", "The number of islands.", "1 ≤ rows, cols ≤ 300",
              "O(rows × cols)", "O(rows × cols)", ["Every unvisited land cell starts a new island.", "Flood-fill (DFS/BFS) from it to mark the whole island visited."],
              "Scan the grid; when you find a `1`, increment the count and flood-fill all connected land to `0` using an explicit stack or queue (avoids recursion depth issues on large grids).",
              islands, [lines("4 5", "11110", "11010", "11000", "00000"), lines("4 5", "11000", "11000", "00100", "00011"),
                        lines("1 1", "0"), lines("3 3", "101", "010", "101"), grid(30, 40, 0.45), grid(120, 150, 0.4)]),

    challenge("ch-first-occurrence", "First Occurrence in a Sorted Array", "Searching", "Easy",
              "Given a sorted array (with possible duplicates) and a value x, return the index of the first occurrence of x, or -1.",
              "Line 1: n. Line 2: n sorted integers. Line 3: x.", "The 0-based index, or -1.", "1 ≤ n ≤ 10^6", "O(log n)", "O(1)",
              ["A linear scan is O(n); the array is sorted.", "When you find x, keep searching to the left."],
              "Binary search, but when `a[mid] == x` record mid and continue with `hi = mid - 1` to find an earlier occurrence.",
              first_occurrence, [lines(7, "1 2 2 2 3 4 5", 2), lines(5, "1 3 5 7 9", 4), lines(1, "5", 5), lines(6, "4 4 4 4 4 4", 4),
                                 lines(10, arr(sorted(rng.randint(0, 20) for _ in range(10))), 7),
                                 lines(2000, arr(sorted(rng.randint(-500, 500) for _ in range(2000))), 123)]),

    challenge("ch-merge-intervals", "Merge Intervals", "Sorting", "Medium",
              "Merge all overlapping intervals and print the resulting non-overlapping intervals in ascending order. Intervals that touch (e.g. [1,4] and [4,5]) overlap.",
              "Line 1: n. Then n pairs `start end`.", "One merged interval per line as `start end`.", "1 ≤ n ≤ 10^5; start ≤ end",
              "O(n log n)", "O(n)", ["Sort the intervals by start.", "Extend the last merged interval while the next one starts before (or at) its end."],
              "Sort by start. Keep a result list; if the current interval starts at or before the end of the last result interval, extend that end with max(); otherwise append it.",
              merge_intervals, [lines(4, "1 3", "2 6", "8 10", "15 18"), lines(2, "1 4", "4 5"), lines(1, "5 7"),
                                lines(5, "6 8", "1 9", "2 4", "4 7", "10 12"),
                                lines(30, *[f"{s} {s + rng.randint(0, 15)}" for s in (rng.randint(0, 200) for _ in range(30))])]),

    challenge("ch-generate-parentheses", "Generate Parentheses", "Recursion", "Medium",
              "Print every well-formed combination of n pairs of parentheses in lexicographic order (`(` sorts before `)`).",
              "A single integer n.", "Each combination on its own line, sorted.", "1 ≤ n ≤ 10", "O(4ⁿ / √n)", "O(n) recursion depth",
              ["Build strings character by character.", "You may add '(' while open < n and ')' while close < open."],
              "Backtracking: from a partial string with `open` and `close` counts, append '(' if open < n and ')' if close < open. Exploring '(' first yields lexicographic order.",
              gen_parens, ["3\n", "1\n", "2\n", "4\n"], samples=2),

    challenge("ch-climbing-stairs", "Climbing Stairs", "DynamicProgramming", "Easy",
              "You can climb 1 or 2 steps at a time. In how many distinct ways can you reach the top of a staircase with n steps?",
              "A single integer n.", "The number of distinct ways.", "1 ≤ n ≤ 80 (the answer fits in 64 bits)", "O(n)", "O(1)",
              ["ways(n) depends on ways(n − 1) and ways(n − 2).", "It is the Fibonacci sequence; keep only the last two values."],
              "`ways(n) = ways(n-1) + ways(n-2)` with ways(0) = ways(1) = 1. Iterate keeping two variables; use 64-bit integers (n = 80 exceeds 32 bits).",
              climb, ["2\n", "3\n", "1\n", "10\n", "45\n", "80\n"]),

    challenge("ch-lcs", "Longest Common Subsequence", "DynamicProgramming", "Medium",
              "Return the length of the longest common subsequence of two strings.",
              "Two lines, each containing a string of lowercase letters.", "The LCS length.", "1 ≤ lengths ≤ 1000", "O(n·m)", "O(n·m), or O(min(n, m)) optimised",
              ["dp[i][j] = LCS of the first i chars of A and first j chars of B.", "If A[i-1] == B[j-1] then dp[i][j] = dp[i-1][j-1] + 1, else the max of dropping one char."],
              "Classic 2-D DP over prefixes. Only the previous row is needed, which reduces memory to O(m).",
              lcs, ["abcde\nace\n", "abc\nabc\n", "abc\ndef\n", "bsbininm\njmjkbkjkv\n",
                    "".join(rng.choice("abc") for _ in range(300)) + "\n" + "".join(rng.choice("abc") for _ in range(300)) + "\n"]),

    challenge("ch-edit-distance", "Edit Distance", "DynamicProgramming", "Hard",
              "Compute the minimum number of single-character insertions, deletions or substitutions needed to turn word A into word B (Levenshtein distance).",
              "Two lines, each a non-empty lowercase word.", "The edit distance.", "1 ≤ lengths ≤ 2000", "O(n·m)", "O(m)",
              ["Define dp[i][j] as the distance between A[:i] and B[:j].", "dp[i][j] = dp[i-1][j-1] if the chars match, else 1 + min(insert, delete, substitute)."],
              "Fill the DP table row by row: base cases dp[i][0] = i and dp[0][j] = j. Keeping two rows reduces memory to O(m).",
              edit_distance, ["horse\nros\n", "intention\nexecution\n", "a\nabc\n", "same\nsame\n", "kitten\nsitting\n",
                              "".join(rng.choice("abcd") for _ in range(400)) + "\n" + "".join(rng.choice("abcd") for _ in range(350)) + "\n"],
              premium=True),

    challenge("ch-first-unique-char", "First Unique Character", "Hashing", "Easy",
              "Return the index of the first character in a string that does not repeat, or -1 if none exists.",
              "A single string of lowercase letters.", "The 0-based index or -1.", "1 ≤ length ≤ 10^5", "O(n)", "O(1) (26 letters)",
              ["Count occurrences first.", "Then scan again for the first character with count 1."],
              "Two passes: count frequencies in a hash map (or a 26-element array), then return the first index whose count is 1.",
              first_unique, ["leetcode\n", "loveleetcode\n", "aabb\n", "z\n", "aabbccddeeffg\n"]),

    challenge("ch-group-anagrams", "Group Anagrams", "Hashing", "Medium",
              "Group the words that are anagrams of each other. Sort the words inside each group alphabetically, then print the groups sorted alphabetically (by their line text).",
              "Line 1: n. Line 2: n lowercase words.", "One group per line, words separated by spaces.", "1 ≤ n ≤ 10^4; word length ≤ 100",
              "O(n · k log k)", "O(n · k)", ["Two words are anagrams if their sorted letters are equal.", "Use the sorted letters (or a letter-count signature) as a hash map key."],
              "Map each word to a canonical key (its sorted characters) and append it to that key's list. Then sort within groups and sort the group lines for deterministic output.",
              group_anagrams, [lines(6, "eat tea tan ate nat bat"), lines(1, "a"), lines(4, "abc bca cab xyz"), lines(5, "listen silent enlist google gogole")]),

    challenge("ch-count-primes", "Count Primes", "Mathematics", "Easy",
              "Count the prime numbers strictly less than n.",
              "A single integer n.", "The number of primes below n.", "0 ≤ n ≤ 5·10^6", "O(n log log n)", "O(n)",
              ["Trial division for every number is too slow for large n.", "Use the Sieve of Eratosthenes, starting to cross out at i·i."],
              "Sieve of Eratosthenes: mark multiples of each prime p starting from p² as composite, only for p ≤ √n. Count the remaining marks.",
              count_primes, ["10\n", "0\n", "2\n", "100\n", "1000000\n", "5000000\n"], time_limit=3000),

    challenge("ch-sliding-window-max", "Sliding Window Maximum", "Arrays", "Hard",
              "For every window of size k sliding from left to right over the array, output the maximum value in the window.",
              "Line 1: n and k. Line 2: n integers.", "The n − k + 1 window maxima separated by spaces.", "1 ≤ k ≤ n ≤ 2·10^5",
              "O(n)", "O(k)", ["Recomputing each window's max is O(n·k).", "Keep a deque of indices whose values are decreasing; the front is the window max."],
              "Maintain a monotonic deque of indices: pop from the back while the new value is ≥ the back's value, push the index, drop the front when it leaves the window. Each index is pushed and popped once.",
              sliding_max, [lines("8 3", "1 3 -1 -3 5 3 6 7"), lines("1 1", "5"), lines("5 5", "4 2 12 3 1"), lines("6 2", "9 8 7 6 5 4"),
                            lines("5000 37", arr(rng.randint(-10000, 10000) for _ in range(5000)))], premium=True),

    challenge("ch-shortest-path", "Shortest Paths from a Source", "Graphs", "Hard",
              "Given an undirected weighted graph with non-negative weights, print the shortest distance from node 1 to every node (1..n), or -1 if unreachable.",
              "Line 1: n and m. Next m lines: `u v w` edges.", "n distances separated by spaces.", "1 ≤ n ≤ 10^5; 0 ≤ m ≤ 2·10^5; 1 ≤ w ≤ 10^4",
              "O((n + m) log n)", "O(n + m)", ["BFS works only for unweighted graphs.", "Use Dijkstra's algorithm with a min-priority queue; skip stale queue entries."],
              "Dijkstra: initialise dist[1] = 0 and push (0, 1). Pop the smallest distance; skip if it is stale; relax neighbours and push improved distances. Non-negative weights make the greedy choice correct.",
              dijkstra, [lines("4 4", "1 2 1", "2 3 2", "1 3 5", "3 4 1"), lines("3 1", "2 3 4"), lines("1 0"),
                         graph_case(50, 120), graph_case(800, 2500)], premium=True),

    challenge("ch-sql-second-salary", "Second Highest Salary (SQL)", "Sql", "Easy",
              "Table `Employees(Id, Name, Salary, DepartmentId)`. Write a query returning the **second highest distinct salary** as a single value. Return NULL when it does not exist.",
              "The Employees table (populated per test case).", "One row with one column: the salary or NULL.", "Ties count once (distinct salaries).",
              None, None, ["Find the maximum salary below the overall maximum.", "Aggregates return NULL over an empty set — use that."],
              "`SELECT MAX(Salary) FROM Employees WHERE Salary < (SELECT MAX(Salary) FROM Employees);` — MAX over no rows yields NULL. A DENSE_RANK() = 2 subquery wrapped in MAX also works.",
              sql_solver("SELECT MAX(Salary) FROM Employees WHERE Salary < (SELECT MAX(Salary) FROM Employees)"),
              [EMP_SCHEMA + emp_inserts([(1, "Ann", 100, 1), (2, "Bob", 200, 1), (3, "Cy", 300, 2)]),
               EMP_SCHEMA + emp_inserts([(1, "Ann", 100, 1)]),
               EMP_SCHEMA + emp_inserts([(1, "Ann", 500, 1), (2, "Bob", 500, 2), (3, "Cy", 400, 2)]),
               EMP_SCHEMA + emp_inserts([(1, "Ann", 70, 1), (2, "Bob", 70, 1)])],
              sql=True),

    challenge("ch-sql-customers-no-orders", "Customers Without Orders (SQL)", "Sql", "Easy",
              "Tables `Customers(Id, Name)` and `Orders(Id, CustomerId, Total)`. Return the names of customers who have never placed an order, sorted by name.",
              "The Customers and Orders tables.", "One name per row, sorted ascending.", "Orders.CustomerId may be NULL for guest orders.",
              None, None, ["Think anti-join.", "NOT IN breaks when the subquery returns NULL — prefer NOT EXISTS or LEFT JOIN … IS NULL."],
              "`SELECT c.Name FROM Customers c WHERE NOT EXISTS (SELECT 1 FROM Orders o WHERE o.CustomerId = c.Id) ORDER BY c.Name;` — NOT EXISTS is NULL-safe, unlike NOT IN.",
              sql_solver("SELECT c.Name FROM Customers c WHERE NOT EXISTS (SELECT 1 FROM Orders o WHERE o.CustomerId = c.Id) ORDER BY c.Name"),
              ["CREATE TABLE Customers (Id INTEGER PRIMARY KEY, Name TEXT);\nCREATE TABLE Orders (Id INTEGER PRIMARY KEY, CustomerId INTEGER, Total INTEGER);\n"
               "INSERT INTO Customers VALUES (1,'Joe'),(2,'Henry'),(3,'Sam'),(4,'Max');\nINSERT INTO Orders VALUES (1,3,50),(2,1,20);\n",
               "CREATE TABLE Customers (Id INTEGER PRIMARY KEY, Name TEXT);\nCREATE TABLE Orders (Id INTEGER PRIMARY KEY, CustomerId INTEGER, Total INTEGER);\n"
               "INSERT INTO Customers VALUES (1,'Ava'),(2,'Ben');\nINSERT INTO Orders VALUES (1,NULL,10),(2,2,15);\n",
               "CREATE TABLE Customers (Id INTEGER PRIMARY KEY, Name TEXT);\nCREATE TABLE Orders (Id INTEGER PRIMARY KEY, CustomerId INTEGER, Total INTEGER);\n"
               "INSERT INTO Customers VALUES (1,'Zed'),(2,'Amy'),(3,'Kim');\n"],
              sql=True),

    challenge("ch-sql-top-earners", "Top Earner per Department (SQL)", "Sql", "Medium",
              "Tables `Employees(Id, Name, Salary, DepartmentId)` and `Departments(Id, Name)`. For every department, return `DepartmentName|EmployeeName|Salary` for the employee(s) with the highest salary in that department. Sort by department name, then employee name.",
              "The Employees and Departments tables.", "Rows formatted as Department|Employee|Salary.", "Ties: return every top earner.",
              None, None, ["Rank employees within each department.", "RANK() OVER (PARTITION BY DepartmentId ORDER BY Salary DESC) = 1 keeps ties."],
              "Use a window function in a CTE: `WITH r AS (SELECT e.*, RANK() OVER (PARTITION BY DepartmentId ORDER BY Salary DESC) rk FROM Employees e) "
              "SELECT d.Name, r.Name, r.Salary FROM r JOIN Departments d ON d.Id = r.DepartmentId WHERE rk = 1 ORDER BY d.Name, r.Name;`",
              sql_solver("WITH r AS (SELECT e.*, RANK() OVER (PARTITION BY DepartmentId ORDER BY Salary DESC) rk FROM Employees e) "
                         "SELECT d.Name, r.Name, r.Salary FROM r JOIN Departments d ON d.Id = r.DepartmentId WHERE rk = 1 ORDER BY d.Name, r.Name"),
              [EMP_SCHEMA + DEPT_SCHEMA + "INSERT INTO Departments VALUES (1,'IT'),(2,'Sales');\n" +
               emp_inserts([(1, "Joe", 70000, 1), (2, "Jim", 90000, 1), (3, "Henry", 80000, 2), (4, "Sam", 60000, 2), (5, "Max", 90000, 1)]),
               EMP_SCHEMA + DEPT_SCHEMA + "INSERT INTO Departments VALUES (1,'Ops');\n" + emp_inserts([(1, "Lee", 50000, 1)]),
               EMP_SCHEMA + DEPT_SCHEMA + "INSERT INTO Departments VALUES (1,'HR'),(2,'Legal'),(3,'R&D');\n" +
               emp_inserts([(1, "Ana", 40000, 1), (2, "Bo", 42000, 1), (3, "Cid", 70000, 2), (4, "Dee", 70000, 2), (5, "Eve", 99000, 3)])],
              sql=True, premium=True),
]

write("challenges.json", CHALLENGES)
