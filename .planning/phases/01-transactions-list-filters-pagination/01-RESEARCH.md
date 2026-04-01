# Phase 1 Research — Transactions list: filters + pagination

**Phase:** 01 — Transactions list — filters + pagination  
**Researched:** 2026-03-31  
**Question:** What do we need to know to plan implementation well?

## Executive summary

Extend `GET /api/v1/transactions` using the existing MediatR + thin controller pattern. Add optional query parameters (`from`, `to`, `categoryIds`, `page`, `pageSize`) while preserving today’s unpaginated response shape when paging is not requested. Persistence should apply filters and sorting in the database via `IQueryable` (EF Core), not by loading all rows into memory—today `GetByCategoryType` loads all transactions then filters in memory; the new work should not worsen that pattern for the new filters.

**ID type note:** `Category.Id` and `Transaction.Id` are **Guids**. Roadmap examples show numeric `categoryIds`; implementation and tests must use **Guid** query parameters (`categoryIds={guid}&categoryIds={guid}`).

## Current implementation (baseline)

| Area | Behavior |
|------|----------|
| `TransactionsV1Controller.GetTransactions` | Single `categoryType` query param; sends `GetAll_transactionsQuery` |
| `GetAllTransactionsQueryHandler` | If `CategoryType` set → `GetByCategoryType` (in-memory filter after full load); else `GetAllAsync()` |
| `TransactionRepository.GetAllAsync` | `OrderByDescending(CreatedAt)`, includes `Category` and `Frequency` |
| Response (no paging) | `ApiResponseDto<List<TransactionResponseDto>>` |

## Ordering semantics (requirements)

- **TRX-08 / unpaginated:** Preserve **current** list behavior: same ordering as today (`CreatedAt` desc from repository), full list, same envelope (`ApiResponseDto<List<...>>`).
- **TRX-06 / paginated:** Order by **`TransactionDate` desc**, then **`Id` desc** (stable tie-break).

## Suggested architecture

1. **Replace or supersede** `GetAllTransactionsQuery` with a single list query (e.g. `GetTransactionsListQuery`) that carries:
   - `CategoryType? categoryType`
   - `DateTime? fromUtc`, `DateTime? toUtc` (or parsed `DateOnly?` converted to inclusive UTC bounds in the handler)
   - `IReadOnlyList<Guid>? categoryIds` + a flag or separate API indicating “parameter absent” vs “present empty” for TRX-09
   - `int? page`, `int? pageSize`
2. **Repository** exposes a **composable** entry point, e.g. `IQueryable<Transaction> GetTransactionsQueryable()` with `AsNoTracking()` and `.Include(t => t.Category).Include(t => t.Frequency)`, or a dedicated method that returns filtered/paged results without leaking EF types to the handler—*preferred for this codebase:* `IQueryable` from repository so the handler applies filters and ordering expressions that EF can translate.
3. **Handler pipeline** (pseudocode):
   - Validate: if `categoryIds` was explicitly sent empty → **400** (TRX-09). If `pageSize > 20` → **400** (TRX-07; choose reject over silent coerce unless product decides otherwise).
   - Determine **paging requested** only when both `page` and `pageSize` are present (or document “partial params” as 400—pick one rule and apply consistently).
   - Build `IQueryable`: optional date range on `TransactionDate`; optional `categoryIds.Contains(t.CategoryId)`; **`categoryType`** when `categoryIds` is null/absent per TRX-03; if both `categoryType` and `categoryIds` are set, apply **both** (intersection): categories must be in the ID set **and** match type when type is specified (defensive, matches “backward compatible” spirit).
   - Unpaginated: `OrderByDescending(t => t.CreatedAt)` to match current repo behavior; materialize list; map DTOs.
   - Paginated: `OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id)`; `CountAsync`; `Skip`/`Take`; return envelope type with `Items` + `TotalCount`.
4. **Controller** returns:
   - Unpaginated: existing `Ok(ApiResponseDto<List<TransactionResponseDto>>.Ok(...))`.
   - Paginated: `Ok(ApiResponseDto<PagedTransactionsResponseDto>.Ok(...))` (new DTO with `Items` and `TotalCount`).

## EF Core translation notes

- Prefer `Queryable.Where` with translatable predicates (`>=`, `<=`, `Contains` on lists).
- Avoid non-translatable helpers in `Where` unless backed by `EF.Functions` or mapped translations.

## Testing strategy

- Extend `FinanceTracker.Tests.Integration.TransactionsApiIntegrationTests` (or add a dedicated test class) using the existing `FinanceTrackerWebApplicationFactory` + in-memory DB.
- Seed multiple transactions and categories via API (reuse `CreateCategoryViaApiAsync` patterns).
- Assert HTTP status, envelope shape, ordering (paginated), inclusion/exclusion for filters, and 400 cases for TRX-07/TRX-09.

## Open items (no CONTEXT.md)

- **Partial pagination params:** If only `page` or only `pageSize` is sent, prefer **400** with a clear message (explicit contract) unless you standardize on “ignore paging”—document the chosen rule in code comments and tests.
- **Date bounds:** Parse `from`/`to` as ISO dates; document UTC boundary behavior in plan tasks.

---

## Validation Architecture

This phase is verified primarily by **integration tests** against the real API host with in-memory EF Core. No new test framework is required.

| Dimension | Approach |
|-----------|----------|
| Automated | `dotnet test` on `FinanceTracker.Tests` |
| Scope | New tests for list filters, pagination envelope, ordering, cap, empty `categoryIds`, backward-compatible unpaginated response |
| CI | Same as existing solution—full test project |
| Manual | Optional Swagger spot-check in Development only |

Validation artifacts: see `01-VALIDATION.md` for commands and task-to-test mapping.

---

## RESEARCH COMPLETE
