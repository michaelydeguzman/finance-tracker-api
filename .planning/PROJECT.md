# Finance Tracker API

## What This Is

An ASP.NET Core (.NET 8) REST API for tracking personal finances. It exposes versioned endpoints to manage finance data (e.g. transactions, categories, frequencies) backed by SQL Server via EF Core.

## Core Value

Users can reliably record and retrieve transactions with flexible filtering for reporting and UI views.

## Requirements

### Validated

- ✓ Versioned `GET /api/v{version}/transactions` exists and returns transactions — existing
- ✓ Transactions have `TransactionDate` and a `Category` relationship — existing
- ✓ Category-type filtering exists on transactions list (`categoryType`) — existing

### Active

- [ ] Extend transactions list endpoint to support filtering by `TransactionDate` (optional `from`/`to`, inclusive bounds)
- [ ] Extend transactions list endpoint to support filtering by multiple categories (`categoryIds[]`)
- [ ] Preserve existing behavior: if `categoryIds` is omitted/null, apply `categoryType` when provided
- [ ] Add optional pagination (1-based `page`, `pageSize`) with deterministic ordering (default: `TransactionDate` desc, then `Id` for tie-break)
- [ ] Preserve backward compatibility: when pagination params are omitted, return full list (current behavior)
- [ ] Add paginated response envelope including `totalCount` (when pagination is used)
- [ ] Enforce `pageSize` cap of 20 for paginated requests

### Out of Scope

- Dashboard/aggregates endpoints (totals, charts, rollups) — not needed for current “list with filters” use case
- Timezone normalization/UTC policy changes — keep API “dumb”; client supplies intended bounds

## Context

- Current stack: .NET 8, ASP.NET Core, MediatR, EF Core (SQL Server), API versioning, Swagger.
- Current transactions list endpoint lives in `FinanceTracker/Controllers/TransactionsV1Controller.cs` and dispatches `GetAllTransactionsQuery`.
- Current list supports `categoryType` filter; we’re extending it with date range, multi-category IDs, and pagination.

## Constraints

- **Backward compatibility**: existing callers of `GET /transactions` without pagination params must still receive full list
- **Performance**: paging must have stable ordering; paged responses include `totalCount`
- **API contract**: pagination is 1-based; `pageSize` must not exceed 20

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Add filters to existing `GET /transactions` | Same resource & representation; avoid duplicated list endpoints | — Pending |
| `categoryIds` is an array filter; fallback to `categoryType` if `categoryIds` absent | Supports multi-select UI while keeping existing query param useful | — Pending |
| Date filtering uses optional `from`/`to` | Supports presets + custom range consistently | — Pending |
| API stays “dumb” about timezone | Keep contract simple; FE sends explicit bounds | — Pending |
| Pagination is optional and 1-based; default order `TransactionDate` desc | Predictable UX and stable paging | — Pending |
| Return envelope with `totalCount` for paged responses | Enables FE pagination UI | — Pending |
| Cap `pageSize` at 20 | Prevent heavy queries from large limits | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-03-31 after initialization*

