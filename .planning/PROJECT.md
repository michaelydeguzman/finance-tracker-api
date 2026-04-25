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
- ✓ **Phase 1:** Date range (`from` / `to`), multi-category `categoryIds` (Guids), optional paging (`page` / `pageSize`) with `items` + `totalCount`, backward-compatible unpaginated list, `pageSize` ≤ 20, empty `categoryIds` rejected — TRX-01..TRX-09

### Active

_(None — milestone v1.0 list/pagination scope delivered.)_

### Out of Scope

- Dashboard/aggregates endpoints (totals, charts, rollups) — not needed for current “list with filters” use case
- Timezone normalization/UTC policy changes — keep API “dumb”; client supplies intended bounds

## Context

- Current stack: .NET 8, ASP.NET Core, MediatR, EF Core (SQL Server), API versioning, Swagger.
- Transactions list endpoint: `FinanceTracker/Controllers/TransactionsV1Controller.cs` dispatches `GetTransactionsListQuery` (MediatR).
- List supports `categoryType`, optional `from`/`to`, `categoryIds`, and optional paging with distinct ordering for paged vs unpaged responses.
- **v1.0 shipped:** ~2,850 LOC C# across the solution; 8 integration tests cover all TRX requirements.
- Known technical debt: none recorded from v1.0.

## Constraints

- **Backward compatibility**: existing callers of `GET /transactions` without pagination params must still receive full list
- **Performance**: paging must have stable ordering; paged responses include `totalCount`
- **API contract**: pagination is 1-based; `pageSize` must not exceed 20

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Add filters to existing `GET /transactions` | Same resource & representation; avoid duplicated list endpoints | ✓ Phase 1 |
| `categoryIds` is an array filter; fallback to `categoryType` if `categoryIds` absent | Supports multi-select UI while keeping existing query param useful | ✓ Phase 1 |
| Date filtering uses optional `from`/`to` | Supports presets + custom range consistently | ✓ Phase 1 |
| API stays “dumb” about timezone | Keep contract simple; FE sends explicit bounds | ✓ Phase 1 |
| Pagination is optional and 1-based; paged order `TransactionDate` desc, then `Id`; unpaged keeps `CreatedAt` desc | Predictable UX and stable paging; backward compatibility | ✓ Phase 1 |
| Return envelope with `totalCount` for paged responses | Enables FE pagination UI | ✓ Phase 1 |
| Cap `pageSize` at 20 | Prevent heavy queries from large limits | ✓ Phase 1 |

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
*Last updated: 2026-04-25 after v1.0 milestone completion*

