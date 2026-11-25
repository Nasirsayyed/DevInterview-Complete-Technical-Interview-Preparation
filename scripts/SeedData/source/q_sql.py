from seedlib import mc, q, write

T = "sql-server"
J, S, W, P, I, TX, QO, DD = ("Joins", "Subqueries & CTEs", "Window Functions", "Stored Procedures, Functions & Triggers",
                             "Indexes & Execution Plans", "Transactions & Isolation Levels", "Query Optimization", "Database Design")
SQL = [
    mc("sq-001", T, J, "Beginner", "INNER JOIN results", "What does an `INNER JOIN` return?",
       [("All rows from both tables", False), ("Only rows with matching values in both tables according to the join condition", True),
        ("All rows from the left table", False), ("The Cartesian product", False)],
       "Only matching rows.", "Rows without a match on either side are excluded. Use LEFT/RIGHT/FULL OUTER JOIN to keep unmatched rows "
       "(with NULLs for the missing side).", tags=["joins"], seconds=30),

    mc("sq-002", T, J, "Intermediate", "Filtering an outer join in WHERE",
       "Why does this query omit customers who have no orders, even though it uses LEFT JOIN?",
       [("LEFT JOIN never returns unmatched rows", False),
        ("The WHERE condition on o.Status is false/unknown for NULL order columns, turning the query into an inner join; move the condition into ON", True),
        ("Because Customers has no primary key", False), ("Because of the ORDER BY", False)],
       "Filtering the outer table's columns in WHERE removes the NULL-extended rows.",
       "For customers without orders, `o.Status` is NULL and `NULL = 'Paid'` is UNKNOWN, so WHERE drops the row. Put the predicate in the "
       "ON clause (`LEFT JOIN Orders o ON o.CustomerId = c.Id AND o.Status = 'Paid'`) to keep all customers.",
       code="SELECT c.Name, o.Id\nFROM Customers c\nLEFT JOIN Orders o ON o.CustomerId = c.Id\nWHERE o.Status = 'Paid';",
       lang="sql", tags=["joins", "outer-join"], seconds=75),

    mc("sq-003", T, J, "Beginner", "CROSS JOIN row count", "Table A has 3 rows and table B has 4 rows. How many rows does `SELECT * FROM A CROSS JOIN B` return?",
       [("7", False), ("12", True), ("4", False), ("3", False)], "12 (3 × 4).",
       "A CROSS JOIN produces the Cartesian product. Useful for generating combinations (e.g. dates × products), dangerous when "
       "accidentally caused by a missing join condition.", tags=["joins"], seconds=30),

    mc("sq-004", T, S, "Intermediate", "NOT IN and NULL",
       "Why can `SELECT * FROM Employees WHERE Id NOT IN (SELECT ManagerId FROM Employees)` return **no rows**?",
       [("NOT IN is not supported in subqueries", False),
        ("If the subquery returns any NULL, `x NOT IN (...)` evaluates to UNKNOWN for every row", True),
        ("Because Id is a primary key", False), ("Because the subquery references the same table", False)],
       "A NULL in the NOT IN list makes the predicate UNKNOWN.",
       "`x NOT IN (1, NULL)` means `x <> 1 AND x <> NULL`, and `x <> NULL` is UNKNOWN. Use `NOT EXISTS` (NULL-safe and usually optimised "
       "as an anti-semi-join) or filter `WHERE ManagerId IS NOT NULL` in the subquery.", tags=["subqueries", "null"], seconds=75),

    mc("sq-005", T, S, "Intermediate", "Recursive CTEs", "Which problem is a recursive CTE designed to solve?",
       [("Pivoting columns to rows", False), ("Traversing hierarchical data such as an employee → manager tree", True),
        ("Replacing indexes", False), ("Running code in parallel", False)],
       "Hierarchies and graphs (org charts, category trees, bill of materials).",
       "A recursive CTE has an anchor member and a recursive member combined with UNION ALL. SQL Server limits recursion to 100 levels by "
       "default (`OPTION (MAXRECURSION n)`).", tags=["cte", "recursion"], seconds=45),

    q("sq-006", T, S, "TrueFalse", "Intermediate", "Are CTEs materialised?",
      "True or false: SQL Server materialises a CTE once and reuses the stored result each time the CTE is referenced in the query.",
      [("True", False), ("False", True, "CTEs are inlined like views; each reference may be evaluated separately.")],
      "False.",
      "A CTE is a named query expression, inlined by the optimiser. Referencing it twice can execute it twice. To compute once and reuse, "
      "store results in a temp table (#temp) or table variable.", tags=["cte"], seconds=40),

    mc("sq-007", T, W, "Intermediate", "RANK vs DENSE_RANK",
       "For salaries 100, 100 and 90 ordered descending, what does `DENSE_RANK()` return?",
       [("1, 2, 3", False, "That is ROW_NUMBER()."), ("1, 1, 3", False, "That is RANK()."), ("1, 1, 2", True), ("1, 2, 2", False)],
       "1, 1, 2.",
       "ROW_NUMBER gives unique sequential numbers; RANK gives ties the same rank and leaves gaps; DENSE_RANK gives ties the same rank "
       "without gaps — handy for 'Nth highest' questions.", tags=["window-functions", "ranking"], seconds=50),

    mc("sq-008", T, W, "Intermediate", "Running totals", "Which expression computes a running total of `Amount` by `OrderDate`?",
       [("SUM(Amount) GROUP BY OrderDate", False),
        ("SUM(Amount) OVER (ORDER BY OrderDate ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)", True),
        ("COUNT(*) OVER ()", False), ("SUM(Amount) OVER (PARTITION BY OrderDate)", False)],
       "A windowed SUM with an ORDER BY frame.",
       "Specify `ROWS` explicitly: the default frame with ORDER BY is `RANGE UNBOUNDED PRECEDING`, which treats ties as peers and is "
       "often slower in SQL Server. Add `PARTITION BY CustomerId` for per-customer running totals.", tags=["window-functions"]),

    mc("sq-009", T, W, "Advanced", "Window functions in WHERE",
       "Why does `WHERE ROW_NUMBER() OVER (ORDER BY Score DESC) <= 3` fail?",
       [("ROW_NUMBER requires PARTITION BY", False),
        ("Window functions are evaluated in the SELECT phase, after WHERE in logical query processing; filter in an outer query or CTE", True),
        ("WHERE cannot contain numbers", False), ("Because ORDER BY is missing at the end", False)],
       "Logical processing order: FROM → WHERE → GROUP BY → HAVING → SELECT (window functions) → ORDER BY.",
       "Compute the row number in a CTE or derived table, then filter: "
       "`WITH r AS (SELECT *, ROW_NUMBER() OVER (ORDER BY Score DESC) rn FROM T) SELECT * FROM r WHERE rn <= 3;`",
       tags=["window-functions", "logical-order"], seconds=75),

    mc("sq-010", T, P, "Intermediate", "Functions vs stored procedures", "Which is true of SQL Server user-defined functions compared with stored procedures?",
       [("Functions can modify permanent tables", False),
        ("Functions can be used inside SELECT/WHERE but cannot have side effects such as modifying permanent tables", True),
        ("Stored procedures can be used inside a SELECT list", False), ("Functions support TRY/CATCH and transactions", False)],
       "Functions are side-effect-free and composable in queries; procedures can do DML, transactions and return multiple result sets.",
       "Scalar UDFs historically ran row-by-row and blocked parallelism (SQL Server 2019 can inline many). Inline table-valued functions "
       "are expanded like parameterised views and usually perform well.", tags=["functions", "stored-procedures"]),

    mc("sq-011", T, P, "Intermediate", "Trigger firing", "An `UPDATE` statement modifies 500 rows. How many times does an AFTER UPDATE trigger fire?",
       [("500 times, once per row", False), ("Once per statement; the inserted/deleted pseudo-tables contain all 500 rows", True),
        ("Twice", False), ("It depends on the number of columns", False)],
       "Once per statement.",
       "SQL Server triggers are statement-level. Write set-based trigger logic that joins `inserted` and `deleted`; code that assumes a "
       "single row (e.g. `SELECT @id = Id FROM inserted`) is a classic bug.", tags=["triggers"], seconds=50),

    mc("sq-012", T, I, "Beginner", "Clustered index", "Which statement about a clustered index is correct?",
       [("A table can have many clustered indexes", False),
        ("It defines the order in which the table's data rows are stored, so a table can have only one", True),
        ("It is stored separately from the data", False), ("It cannot be created on a primary key", False)],
       "The clustered index is the table's data; one per table.",
       "The leaf level of the clustered index contains the rows. Nonclustered indexes store keys plus a row locator (the clustering key). "
       "A narrow, unique, ever-increasing clustering key (e.g. INT IDENTITY) minimises fragmentation and nonclustered index size.",
       tags=["indexes"], seconds=40),

    mc("sq-013", T, I, "Intermediate", "INCLUDE columns", "What is the benefit of `INCLUDE` columns in a nonclustered index?",
       [("They enforce uniqueness", False),
        ("They let the index cover a query without key lookups, without making the index key wider", True),
        ("They sort the table", False), ("They compress the index", False)],
       "Covering queries cheaply.",
       "Included columns are stored only at the leaf level: queries selecting them avoid Key Lookups into the clustered index, while the "
       "B-tree key stays small. Example: `CREATE INDEX IX_Orders_Customer ON Orders(CustomerId) INCLUDE (OrderDate, Total);`",
       tags=["indexes", "covering-index"]),

    mc("sq-014", T, I, "Advanced", "SARGable predicates", "Why is `WHERE YEAR(OrderDate) = 2024` usually slower than the alternative?",
       [("YEAR() is not supported", False),
        ("Wrapping the column in a function prevents an index seek; use a range: OrderDate >= '2024-01-01' AND OrderDate < '2025-01-01'", True),
        ("Because 2024 is an integer", False), ("It is equally fast", False)],
       "Functions on indexed columns make predicates non-SARGable.",
       "A Search ARGument-able predicate compares the bare column with a value, allowing an index seek. Similar traps: "
       "`ISNULL(col, 0) = 5`, `col + 1 = 10`, implicit conversions from mismatched data types (e.g. NVARCHAR vs VARCHAR).",
       tags=["sargability", "performance"], seconds=60),

    mc("sq-015", T, I, "Intermediate", "Key Lookup operator", "What does a **Key Lookup** in an execution plan indicate?",
       [("A primary key violation", False),
        ("A nonclustered index was used to find rows, but other columns had to be fetched from the clustered index row by row", True),
        ("The statistics are missing", False), ("A table scan", False)],
       "Missing columns fetched from the clustered index per row.",
       "A few lookups are fine; thousands are expensive. Add the missing columns as INCLUDE columns to make the index covering, or let "
       "the optimiser choose a scan when a large fraction of rows is needed.", tags=["execution-plans"], seconds=50),

    mc("sq-016", T, TX, "Intermediate", "READ COMMITTED", "Which isolation level prevents dirty reads but still allows non-repeatable reads?",
       [("READ UNCOMMITTED", False), ("READ COMMITTED", True), ("REPEATABLE READ", False), ("SERIALIZABLE", False)],
       "READ COMMITTED (SQL Server's default).",
       "READ UNCOMMITTED allows dirty reads; READ COMMITTED prevents them; REPEATABLE READ also prevents non-repeatable reads; "
       "SERIALIZABLE also prevents phantoms. SNAPSHOT gives a consistent view using row versions.", tags=["isolation-levels"], seconds=45),

    mc("sq-017", T, TX, "Advanced", "READ_COMMITTED_SNAPSHOT", "What does enabling `READ_COMMITTED_SNAPSHOT` on a database change?",
       [("It makes every transaction SERIALIZABLE", False),
        ("Statements under READ COMMITTED read the last committed row versions from the version store instead of taking shared locks, so readers do not block writers", True),
        ("It disables the transaction log", False), ("It allows dirty reads", False)],
       "READ COMMITTED becomes versioned: readers no longer block or get blocked by writers.",
       "Row versions live in tempdb (or the persistent version store with ADR), adding some overhead. It is statement-level consistency; "
       "SNAPSHOT isolation provides transaction-level consistency and can raise update conflicts.", tags=["isolation-levels", "rcsi"], seconds=75),

    mc("sq-018", T, TX, "Intermediate", "Deadlock handling", "What does SQL Server do when it detects a deadlock?",
       [("Waits forever", False), ("Chooses a victim, rolls back its transaction and raises error 1205", True),
        ("Restarts the server", False), ("Commits both transactions", False)],
       "Kills a victim with error 1205.",
       "Applications should retry the victim transaction. Reduce deadlocks by accessing objects in a consistent order, keeping "
       "transactions short, using appropriate indexes, and considering RCSI.", tags=["deadlocks", "transactions"], seconds=45),

    mc("sq-019", T, QO, "Advanced", "Parameter sniffing", "What is parameter sniffing?",
       [("A security attack on stored procedures", False),
        ("The optimiser builds and caches a plan for the first parameter values, which may be poor for very different values later", True),
        ("Reading parameters from the network", False), ("Logging parameters", False)],
       "A cached plan tuned for one set of parameter values.",
       "Skewed data (one customer with millions of rows) makes it visible. Mitigations: `OPTION (RECOMPILE)` for infrequent queries, "
       "`OPTIMIZE FOR`, Query Store plan forcing, or SQL Server 2022 Parameter Sensitive Plan optimisation.", tags=["query-plans", "performance"]),

    mc("sq-020", T, DD, "Beginner", "Third normal form", "A table is in 3NF when it is in 2NF and…",
       [("it has no indexes", False), ("no non-key column depends on another non-key column (no transitive dependencies)", True),
        ("all columns are strings", False), ("it has a composite key", False)],
       "No transitive dependencies on the key.",
       "Example violation: Orders(OrderId, CustomerId, CustomerCity) — CustomerCity depends on CustomerId, not OrderId. Move it to "
       "Customers. Denormalise deliberately for read-heavy reporting.", tags=["normalization"], seconds=50),

    mc("sq-021", T, DD, "Intermediate", "Random GUID clustered keys", "Why can a clustered primary key on `NEWID()` GUIDs hurt insert performance?",
       [("GUIDs cannot be indexed", False), ("Random values insert into random pages, causing page splits and fragmentation", True),
        ("GUIDs are 4 bytes", False), ("SQL Server sorts GUIDs alphabetically only on Sundays", False)],
       "Random insertion points cause page splits and fragmentation.",
       "Ever-increasing keys append to the end of the index. Options: INT/BIGINT IDENTITY clustered key with a GUID as a unique "
       "nonclustered public id, `NEWSEQUENTIALID()`, or sequential GUIDs generated by the application.", tags=["keys", "design"], seconds=60),

    q("sq-022", T, S, "CodeBased", "Intermediate", "Second highest salary",
      "Write a T-SQL query that returns the **second highest distinct salary** from `Employees(Id, Name, Salary)`. "
      "Return NULL if it does not exist.",
      answer="SELECT MAX(Salary) FROM Employees WHERE Salary < (SELECT MAX(Salary) FROM Employees); — or use DENSE_RANK() = 2.",
      explanation="Option 1 (returns NULL automatically when there is no second value):\n```sql\nSELECT MAX(Salary) AS SecondHighest\n"
                  "FROM Employees\nWHERE Salary < (SELECT MAX(Salary) FROM Employees);\n```\nOption 2 generalises to the Nth value:\n"
                  "```sql\nSELECT MAX(Salary) AS NthHighest\nFROM (SELECT Salary, DENSE_RANK() OVER (ORDER BY Salary DESC) AS rnk FROM Employees) r\n"
                  "WHERE rnk = 2;\n```\nDENSE_RANK handles duplicate salaries correctly; wrapping in MAX returns NULL when no row matches.",
      reference="Use MAX over salaries below the maximum, or DENSE_RANK() OVER (ORDER BY Salary DESC) = 2 wrapped in MAX to return NULL "
                "when absent; handle duplicates with DISTINCT/DENSE_RANK.", tags=["window-functions", "classic"], seconds=240),

    q("sq-023", T, QO, "ScenarioBased", "Advanced", "Tuning a slow reporting query",
      "A dashboard query that joins Orders (50M rows) and Customers became slow after data growth. How do you approach tuning it?",
      answer="Capture the actual execution plan and wait stats, check for scans, key lookups, spills and bad estimates; fix SARGability, "
             "add/adjust covering indexes, update statistics, and consider pre-aggregation.",
      explanation="1. Get the *actual* plan (or Query Store history) and compare estimated vs actual rows.\n2. Look for scans on large "
                  "tables, key lookups, sort/hash spills to tempdb, implicit conversions and non-SARGable predicates.\n3. Update "
                  "statistics; add covering indexes aligned with filters and joins; avoid SELECT *.\n4. Check parameter sniffing.\n5. For "
                  "dashboards, pre-aggregate into summary tables or indexed views, or use columnstore indexes for analytics.\n6. Re-measure "
                  "with SET STATISTICS IO, TIME ON.",
      reference="Diagnose with actual plans, Query Store and wait statistics; fix estimate errors (statistics), non-SARGable predicates "
                "and implicit conversions; add covering or columnstore indexes; reduce returned columns; address parameter sniffing; "
                "pre-aggregate for dashboards; verify with IO/TIME statistics.", tags=["performance", "tuning"], seconds=300, premium=True),
]

T2 = "databases"
DB = [
    mc("db-001", T2, "Relational Databases", "Intermediate", "PostgreSQL VACUUM", "Why does PostgreSQL need `VACUUM`?",
       [("To rebuild all indexes nightly", False),
        ("Its MVCC keeps old row versions (dead tuples) after UPDATE/DELETE; VACUUM reclaims them and prevents transaction-ID wraparound", True),
        ("To compress backups", False), ("To refresh materialized views", False)],
       "MVCC leaves dead tuples that must be cleaned up.",
       "Autovacuum normally handles this; long-running transactions prevent cleanup and cause bloat. VACUUM also updates the visibility "
       "map used by index-only scans.", tags=["postgresql", "mvcc"], seconds=60),

    mc("db-002", T2, "Relational Databases", "Intermediate", "InnoDB storage", "How does MySQL's InnoDB engine physically organise table rows?",
       [("As a heap with no order", False), ("In a clustered index on the primary key; secondary indexes store the primary key value", True),
        ("In separate files per row", False), ("Only in memory", False)],
       "Clustered by primary key.",
       "Because secondary indexes reference the primary key, a wide primary key makes every secondary index larger. Random UUID primary "
       "keys cause page splits, like random GUIDs in SQL Server.", tags=["mysql", "innodb"], seconds=50),

    mc("db-003", T2, "Relational Databases", "Beginner", "Limiting rows", "Which clause limits the number of rows returned in PostgreSQL and MySQL?",
       [("TOP", False, "TOP is SQL Server syntax."), ("LIMIT", True), ("ROWNUM", False, "ROWNUM is Oracle-specific."), ("FIRST", False)],
       "LIMIT (with OFFSET for paging).",
       "The ANSI form `OFFSET n ROWS FETCH NEXT m ROWS ONLY` works in PostgreSQL and SQL Server. Large OFFSETs are slow; prefer keyset "
       "pagination (`WHERE id > @lastId ORDER BY id LIMIT 20`).", tags=["pagination"], seconds=30),

    mc("db-004", T2, "Relational Databases", "Intermediate", "Indexing JSONB", "Which index type is typically used to speed up containment queries (`@>`) on a PostgreSQL JSONB column?",
       [("B-tree", False), ("GIN", True), ("Hash", False), ("BRIN", False)],
       "GIN.",
       "Generalized Inverted Indexes index the keys/values inside JSONB documents (also used for arrays and full-text search). "
       "B-tree indexes on expressions such as `(data->>'email')` suit equality lookups on a single path.", tags=["postgresql", "jsonb"], seconds=40),
]

write("questions/sqlserver.json", SQL)
write("questions/databases.json", DB)
