# Finance Tracker API — Query Filtering + Pagination (CQRS/MediatR + EF Core)

**Researched:** 2026-03-31  
**Scope:** Architecture patterns for list queries (filters + pagination) in a layered API: Controller → MediatR → services → EF Core repos.

## Executive recommendation (opinionated)

Use **Application-layer query parameter objects** plus **composable `IQueryable` extensions** (or a light “specification” abstraction) for filtering/sorting/paging. Keep EF Core execution in Infrastructure, but keep *query shape* decisions (what filters exist, allowed sorts, default ordering, paging rules) in Application.

If you’re already using repositories that return `IQueryable<T>` (or accept an `IQueryable`-shaping input), the simplest scalable pattern is:

- **`TransactionsQueryParams` (Application DTO)**: owns *what the API supports* (date range, categoryIds, categoryType fallback, paging/sort).
- **`ApplyFilters/ApplySorting/ApplyPaging` (Application extensions)**: pure LINQ over `IQueryable<Transaction>` using expression trees (EF-translatable).
- **Handler**: orchestrates `totalCount` + paging and returns either full list or an envelope depending on whether paging is requested.

If you prefer strong encapsulation/reuse across many queries, introduce the **Specification pattern** (or similar) *in Application* and have Infrastructure apply it to EF Core queries (see sources).

## Where filter objects belong

### Controller / Presentation

- **Purpose:** parse HTTP query params into a request model and apply validation defaults.
- **Keep here:** HTTP-specific concerns only (query string naming, binding quirks).
- **Avoid here:** business semantics (e.g., “if categoryIds omitted, fallback to categoryType”) — that should live in Application so it’s testable and reusable outside HTTP.

### Application (recommended home)

- **`TransactionsQueryParams` (or `GetTransactionsFilter`)**: a stable contract used by MediatR query + handler.
- **Validation rules:** page is 1-based, `pageSize <= 20`, allowed sort fields, “categoryIds overrides categoryType”, inclusive from/to semantics.
- **Reusable query composition:** `IQueryable` extension methods or specifications.

Why: Application is where you define use-case behavior. The presence/meaning of filters is use-case logic; EF Core is just the executor.

### Infrastructure

- **Executes** the composed query (`ToListAsync`, `CountAsync`) and maps persistence config.
- **Avoid:** embedding application filter semantics inside repositories as many bespoke methods (that explodes quickly).

## Pattern 1 — Composable `IQueryable` pipeline (pragmatic default)

### Shape

- **MediatR query:** `GetTransactionsQuery(TransactionsQueryParams Params, bool UsePaginationEnvelope)`
- **Handler:** obtains base `IQueryable<Transaction>` from repo/dbcontext, applies:
  - `ApplyFilters(params)`
  - `ApplyDefaultOrdering()` (deterministic)
  - If paging requested:
    - `totalCount = await query.CountAsync(ct)`
    - `items = await query.ApplyPaging(params).ToListAsync(ct)`
    - return `PagedResult<T>` (envelope)
  - Else:
    - return full list (current behavior)

### Why it works well

- **Testability:** filter rules are pure functions over `IQueryable`.
- **EF Core-friendly:** expression trees translate to SQL when you keep logic translatable.
- **Extensible:** adding a filter is “one more `.Where(...)`” in `ApplyFilters`.

### Rules of thumb (EF Core translation safety)

- Use `Expression<Func<T,bool>>` and `Queryable.Where`.
- Avoid arbitrary .NET methods inside predicates unless EF can translate them.
- Prefer **range predicates** (`>=`, `<=`) and `Contains` over lists (`categoryIds.Contains(t.CategoryId)`).
- Keep projection (`Select`) near the end unless you need it earlier for performance.

## Pattern 2 — Specification pattern (when reuse is high)

### When to prefer it

- Many list queries with overlapping filters (date range appears everywhere).
- Desire to name/query as a first-class concept (“TransactionsForReportSpec”).
- Want a single repository method rather than many bespoke methods.

### Placement

- **Specs live in Application** (or Domain if you’re strict) because they describe the *business query intent*.
- **EF evaluation lives in Infrastructure** (adapter) because it knows EF Core.

### Notes

Ardalis.Specification is a common implementation: it encapsulates query logic (Where/Include/OrderBy/Pagination) in a class and lets a repository evaluate it against EF Core via an evaluator. It’s specifically positioned as a way to prevent repository-method explosion.

## Pattern 3 — Dynamic predicate builder (advanced, for optional filters)

### When it helps

- Lots of optional filters and you want to avoid nested `if` blocks.

### How to do it safely

- Build expression trees and combine with `AndAlso` / `OrElse`.
- Keep the builder in Application; only pass the final expression(s) to Infrastructure for execution.

### Caution

- Debuggability can suffer if expressions get too dynamic.
- Watch out for EF translation edge cases; unit tests won’t catch translation failures unless you run against a relational provider.

## Pagination: offset vs keyset (and “totalCount” reality)

### Offset pagination (Skip/Take) — good for your current requirements

You explicitly need:

- Optional paging
- Deterministic ordering: `TransactionDate` desc then `Id` tie-break
- `totalCount` in the envelope

That maps naturally to **offset pagination**:

- Always apply a deterministic `OrderBy/ThenBy` before `Skip/Take`.
- Compute `totalCount` from the **filtered** query **before** applying `Skip/Take`.

### Keyset pagination — consider later for scale

Keyset (“seek”) pagination avoids large `Skip` costs on big datasets and is generally better for deep paging, but it complicates:

- Client contract (needs cursor tokens)
- Total count semantics (still possible, but often expensive/less useful)

Given your current constraints (page/pageSize + totalCount + cap 20), offset is the right starting point.

## Recommended building blocks (concrete)

### 1) Application DTOs

- `TransactionsQueryParams`
  - `DateOnly? From`, `DateOnly? To` (or `DateTime?` if you already use that)
  - `IReadOnlyList<int>? CategoryIds`
  - `CategoryType? CategoryType`
  - `int? Page`, `int? PageSize`
  - (Optional later) `string? SortBy`, `SortDirection? SortDir`

### 2) Result envelopes

- `PagedResult<T>`
  - `IReadOnlyList<T> Items`
  - `int TotalCount`
  - `int Page`
  - `int PageSize`

And keep backward compatibility by returning the old “full list” shape when `Page/PageSize` are omitted.

### 3) Query composition functions (Application)

- `ApplyFilters(IQueryable<Transaction> query, TransactionsQueryParams p)`
  - Date range (inclusive)
  - CategoryIds overrides CategoryType
- `ApplyOrdering(IQueryable<Transaction> query)`
  - default: `OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id)` (or `ThenBy(t => t.Id)`; pick one and keep stable)
- `ApplyPaging(IQueryable<Transaction> query, int page, int pageSize)`
  - `Skip((page-1)*pageSize).Take(pageSize)`

### 4) Validation

Put validation in Application (preferably via pipeline behavior / validators):

- `page >= 1` when provided
- `1 <= pageSize <= 20` when provided
- Require `pageSize` if `page` is provided (or default it)
- Optionally clamp vs reject; for APIs, rejecting invalid paging is usually clearer than silently clamping.

## Phase-specific warning (for your repo’s current layering)

Your current architecture notes indicate **Application references Infrastructure** and repository interfaces live in Infrastructure. That’s workable, but it encourages “put all query logic in repos”.

Mitigation (without major refactor):

- Keep repository methods **generic-ish** (expose a base query or accept a spec/query options object) and keep the filter semantics in Application.
- If you do adopt Specification, you can still keep the evaluator in Infrastructure while the spec definitions stay in Application.

## Sources

- Ardalis Specification docs (overview): `https://specification.ardalis.com/`
- Ardalis Specification usage with Repository Pattern: `https://specification.ardalis.com/usage/use-specification-repository-pattern.html`

