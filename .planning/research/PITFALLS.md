# Domain Pitfalls — Date Filters & Pagination

**Domain:** Finance Tracker API — transaction date-range filtering & pagination (EF Core / SQL Server)  
**Researched:** 2026-03-31

This document focuses on pitfalls that can break correctness, stability, or performance when adding `from`/`to` transaction date filters and 1-based pagination to the existing transactions list endpoint.

## Critical Pitfalls

### Pitfall 1: Off-by-one and inclusive range bugs for date-only filters
**What goes wrong:** Users pick a date range (e.g. 2026‑03‑01 to 2026‑03‑31) but the API silently drops transactions on the end date, or includes transactions outside the intended range. This usually happens when comparing `DateTime` values with time components against date-only query parameters.

**Why it happens:**
- Applying `>= from && <= to` directly to `DateTime` columns whose time-of-day is not midnight.
- Using end-of-day sentinels (e.g. `23:59:59`) and getting the precision wrong (milliseconds/ticks).
- Mixing `Date` semantics in the client with `DateTime` storage in SQL Server.

**Consequences:**
- Reports and UI views look “wrong” at the boundaries (e.g. last day of month missing).
- Users lose trust in the system when “known” transactions don’t appear.
- Subtle to detect in tests unless boundaries are explicitly covered.

**Prevention (for this project):**
- Treat the API’s `from`/`to` as **date-only** and convert them into a half-open server-side range:
  - `fromDateInclusive` → `fromDateInclusive.Date` (start of day).
  - `toDateInclusive` → `toDateInclusive.Date.AddDays(1)` and use `< nextDay` instead of `<= to`.
- Use a single normalized expression in queries, e.g.:
  - `where t.TransactionDate >= from && t.TransactionDate < toExclusive`.
- Add tests that explicitly cover:
  - Single-day ranges.
  - End-of-month and end-of-year ranges.
  - Ranges that have no matching data (should return empty but not error).

**Detection:**
- Unit/integration tests around boundary dates.
- Compare database contents vs API results for known ranges (seeded data).

### Pitfall 2: Timezone confusion between client, API, and SQL Server
**What goes wrong:** Date filters behave differently depending on where the client is located or how `DateTime` is serialized; some transactions appear on the “wrong” day for certain users.

**Why it happens:**
- Storing `DateTime` with `Kind=Unspecified` and then treating it inconsistently as local vs UTC.
- Clients sending local dates or datetimes that get implicitly converted by ASP.NET Core model binding.
- Server and database using different timezones or daylight saving rules.

**Consequences:**
- Hard-to-reproduce bugs where the same request returns different data over time or between environments.
- Date-range filters that “shift” when deployed on servers in a different timezone.

**Prevention (for this project, given the “API stays dumb about timezone” decision):**
- Be explicit about the contract:
  - Treat `TransactionDate` as a **logical business date** (e.g. posting date) rather than a moment-in-time.
  - Treat `from`/`to` as logical dates **in the same calendar system as `TransactionDate`**, not as local/UTC instants.
- Prefer `DateOnly` for new properties and filters in the domain and DTOs when possible; when using `DateTime`:
  - Normalize all comparisons to `.Date` or the half-open range pattern for dates.
  - Avoid server-side timezone conversions in queries for this endpoint (per project decision).
- Document the behavior clearly in API docs so clients don’t send timezone-dependent instants.

**Detection:**
- Integration tests that verify behavior is independent of server timezone.
- Environment checks where server and database are configured with different timezones.

### Pitfall 3: Applying functions to columns in WHERE clause (killing indexes)
**What goes wrong:** Date-filtered transaction queries are unexpectedly slow or time out as data grows, even with an index on `TransactionDate`.

**Why it happens:**
- Using patterns like `where t.TransactionDate.Date >= fromDate` or `CAST(TransactionDate AS DATE)` in SQL.
- Applying other non-sargable functions (e.g. `Convert`, `DateAdd` on the column) that prevent SQL Server from using the index efficiently.
- Doing complex client-side conversion logic that EF Core translates poorly.

**Consequences:**
- Full table scans on the transactions table for every filtered query.
- Severe performance degradation under load or with large historical datasets.

**Prevention:**
- Keep the column side “clean” and apply transformations to parameters, not to the column:
  - Precompute `fromUtc`, `toExclusiveUtc`, or normalized `DateTime` values in .NET.
  - Use comparisons like `t.TransactionDate >= fromNormalized && t.TransactionDate < toExclusiveNormalized`.
- For purely date-based filters, consider:
  - Storing a separate `TransactionDateOnly` (e.g. `date` in SQL Server / `DateOnly` in C#) if the time component is not needed.
- Ensure proper indexes exist for the query pattern (e.g. index on `TransactionDate` and potentially composite with `CategoryId`).

**Detection:**
- Inspect generated SQL (e.g. via `ToQueryString()` or logging).
- Use SQL Server execution plans to verify index seeks vs scans.

### Pitfall 4: Pagination without deterministic, unique ordering
**What goes wrong:** Users see duplicate or missing transactions when paging through results; `page=2` sometimes overlaps with `page=1` or skips items after inserts.

**Why it happens:**
- Relying on `OrderBy(t.TransactionDate)` only, when many rows share the same timestamp.
- Leaving ordering entirely unspecified (database default ordering is undefined).
- Using `Skip(pageSize * (page - 1)).Take(pageSize)` on a non-unique sort key.

**Consequences:**
- Unstable pagination where the same page returns different results on successive requests.
- Very hard-to-debug issues when concurrent writes happen.

**Prevention (aligned with project requirements):**
- Always specify a **stable, unique ordering**, e.g.:
  - `OrderByDescending(t.TransactionDate).ThenByDescending(t.Id)` (or ascending, but consistently).
- Use the same ordering for both:
  - The paginated result query.
  - Any count or non-paginated variants that should match.
- Add integration tests that:
  - Seed transactions sharing the same `TransactionDate` but different IDs.
  - Assert that paging through the list never loses or duplicates any seeded transaction.

**Detection:**
- Manually create data with identical timestamps and exercise pagination.
- Log the ordered keys (date + id) for successive pages to ensure continuity.

### Pitfall 5: Counting and paging against different predicates
**What goes wrong:** The reported `totalCount` does not match the actual number of items that can be paged through, or some filters are only applied to the result query but not the count (or vice versa).

**Why it happens:**
- Building two separate queries by hand and accidentally applying different filters or includes.
- Materializing into memory before counting or paging, and then mutating the in-memory list.

**Consequences:**
- Pagination UI shows wrong number of pages.
- Edge cases where the “last page” is empty or partially full when it shouldn’t be.

**Prevention:**
- Build a single base `IQueryable<Transaction>` that applies **all** filters (date, categoryIds, categoryType) once.
- Derive:
  - `totalCount` using `baseQuery.CountAsync(cancellationToken)`.
  - Paged items using `baseQuery.OrderBy(...).Skip(...).Take(...).ToListAsync(cancellationToken)`.
- Avoid materializing the query before counting or paging.

**Detection:**
- Tests comparing `totalCount` against the actual number of rows returned when walking all pages.
- Assertions that filters applied in the request are reflected identically in both count and data queries.

### Pitfall 6: Fetching entire result sets before filtering or paging
**What goes wrong:** EF Core loads all transactions into memory (or a very large subset) and only then applies filters or paging, causing memory pressure and slow responses.

**Why it happens:**
- Calling `.ToList()` or `.AsEnumerable()` too early, before all `Where`/`OrderBy`/`Skip`/`Take` operators are applied.
- Doing in-memory projections that EF Core cannot translate, forcing client-side evaluation of filters.

**Consequences:**
- Excessive memory usage and slow endpoints as data grows.
- Increased GC pressure and timeouts under load.

**Prevention:**
- Keep queries as `IQueryable` end-to-end until the final `ToListAsync` / `SingleAsync` etc.
- Avoid client-only projections inside the core query; if necessary, project into DTOs with mappable expressions.
- Enable EF Core logging for client vs server evaluation if needed, and watch for warnings about client-side evaluation in logs.

**Detection:**
- Review code for early materialization.
- Use logging and profiling to confirm only the paged subset is pulled from the database.

## Moderate Pitfalls

### Pitfall 7: Ignoring page bounds and caps
**What goes wrong:** The API accepts arbitrarily large `pageSize` and out-of-range `page` values, allowing abusive or accidental heavy queries.

**Why it happens:**
- Passing user-provided `page` and `pageSize` directly into `Skip`/`Take` without validation.
- Not enforcing upper bounds at the API boundary.

**Consequences:**
- Requests that scan huge portions of the table (e.g. `pageSize=10000`), hurting performance.
- Confusing UX when `page` is far beyond the total number of pages.

**Prevention (aligned with project constraints):**
- Clamp `pageSize` to a reasonable maximum (e.g. 20, as specified).
- Normalize or reject invalid `page` values:
  - Treat `page <= 0` as `1` or return a validation error.
  - When `page` is too large, return an empty `items` array but a correct `totalCount`.
- Document these rules so clients know what to expect.

### Pitfall 8: Breaking backward compatibility when introducing optional pagination
**What goes wrong:** Existing clients that do not send pagination parameters suddenly start receiving paged results or a different response envelope.

**Why it happens:**
- Applying pagination unconditionally or changing the shape of the success response for all calls.

**Consequences:**
- Downstream consumers break silently (e.g. UI lists truncate, integrations misinterpret responses).

**Prevention (specific to this project):**
- Keep two pathways:
  - **Non-paginated path** when `page` and `pageSize` are both absent → preserve existing behavior and response shape.
  - **Paginated path** when both are present → return a well-defined envelope (`items`, `totalCount`, etc.).
- Add explicit tests:
  - “Legacy” call without pagination returns the full list and original payload shape.
  - Paginated call returns a new envelope but same item ordering semantics.

### Pitfall 9: Over-eager `Include`s causing heavy queries
**What goes wrong:** Pagination queries become slow because EF Core eagerly loads large related graphs (e.g. categories, frequencies) for every row.

**Why it happens:**
- Adding `.Include` navigation properties without considering the cost per page.
- Using `Include` in both the paged query and a separate summary query.

**Consequences:**
- Increased row and column counts per page.
- Higher memory footprint and slower serialization.

**Prevention:**
- Only `Include` what the list view truly needs.
- Consider projection into lightweight DTOs with just the required fields (e.g. category name instead of full category entity).
- Avoid `Include` on the `CountAsync` query; use the minimal base query.

## Minor Pitfalls

### Pitfall 10: Inconsistent filter precedence between `categoryIds` and `categoryType`
**What goes wrong:** The combined behavior of `categoryIds` and `categoryType` is unclear or inconsistent between pages or code paths.

**Why it happens:**
- Implementing filters in slightly different ways in different queries (e.g. base vs paged).
- Forgetting the rule that `categoryIds` takes precedence when provided, with `categoryType` as a fallback.

**Consequences:**
- Confusing results when slowly rolling out multi-category filtering.

**Prevention (per project decisions):**
- Implement a single, shared filter-building function that:
  - Applies `categoryIds` when present and non-empty.
  - Falls back to `categoryType` only when `categoryIds` is null/omitted.
- Reuse this logic for both count and data queries.
- Cover the precedence behavior in tests (with and without pagination).

### Pitfall 11: Missing or misleading API documentation for filters and pagination
**What goes wrong:** Clients misuse `from`/`to` or pagination parameters, causing unexpected queries and support load.

**Why it happens:**
- Swagger description fields and XML comments not updated to reflect date semantics and 1-based pagination.

**Consequences:**
- More trial-and-error on the client side.
- Harder to diagnose issues because expectations are not aligned.

**Prevention:**
- Document:
  - That `from`/`to` are inclusive date bounds interpreted as logical transaction dates.
  - That pagination is 1-based and `pageSize` is capped.
  - That `categoryIds` overrides `categoryType` when specified.
- Keep Swagger annotations and any external API docs in sync with implementation.

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|-----------|
| Add `from`/`to` date filters | Off-by-one end date and timezone ambiguity | Normalize to half-open ranges, treat values as logical dates, and add boundary tests. |
| Add optional pagination | Unstable ordering and backward compatibility breakage | Enforce deterministic ordering (`TransactionDate` + `Id`) and branch behavior based on presence of pagination params. |
| Combine filters + pagination | Mismatched predicates between count and data queries | Build filters once on a shared `IQueryable` and derive both count and items from it. |
| Scale to larger datasets | Non-sargable predicates and unbounded page sizes | Avoid functions on columns, index common filter keys, and cap `pageSize`. |

## Sources

- **EF Core pagination guidance (HIGH confidence):** Microsoft Learn — “Pagination - EF Core” (`https://learn.microsoft.com/en-us/ef/core/querying/pagination`).
- **Performance pitfalls with EF Core queries (MEDIUM confidence):** Community post “Our EF Core Queries Were 100× Slower Than They Should Be — Here’s Every Pitfall We Hit” on Medium (Feb 2026) — highlights N+1, over-fetching, and non-sargable predicates.
- **Date range and timezone filtering pitfalls (MEDIUM confidence):** Stack Overflow discussions on EF Core date range performance and timezone conversion behavior in SQL Server and PostgreSQL, cross-checked against EF Core docs.
