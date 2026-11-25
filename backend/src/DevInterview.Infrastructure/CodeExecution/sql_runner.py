"""DevInterview SQL challenge runner (executed inside the sandbox container).

stdin  : setup SQL (CREATE TABLE / INSERT statements) for the test case.
main.sql: the candidate's query (a single SELECT, optionally preceded by CTEs).
stdout : one line per result row, columns separated by '|'. NULL is printed as NULL.
Uses an in-memory SQLite database, so nothing persists between test cases.
"""
import sqlite3
import sys


def format_value(value) -> str:
    if value is None:
        return "NULL"
    if isinstance(value, float):
        # Whole numbers print without a decimal part; others are rounded to 2 places.
        return str(int(value)) if value.is_integer() else f"{value:.2f}"
    return str(value)


def main() -> int:
    setup = sys.stdin.read()
    with open("main.sql", encoding="utf-8") as f:
        query = f.read().strip().rstrip(";")
    if not query:
        print("Empty query", file=sys.stderr)
        return 1
    lowered = query.lower()
    if not (lowered.startswith("select") or lowered.startswith("with")):
        print("Only SELECT queries (optionally with CTEs) are allowed.", file=sys.stderr)
        return 1
    conn = sqlite3.connect(":memory:")
    try:
        conn.executescript(setup)
        rows = conn.execute(query).fetchall()
    except sqlite3.Error as exc:
        print(f"SQL error: {exc}", file=sys.stderr)
        return 1
    for row in rows:
        print("|".join(format_value(v) for v in row))
    return 0


if __name__ == "__main__":
    sys.exit(main())
