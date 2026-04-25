# Milestones

## v1.0 Transactions List Filters + Pagination (Shipped: 2026-04-25)

**Phases completed:** 1 phase, 2 plans, 6 tasks  
**Timeline:** 2026-03-31 → 2026-04-01  
**Codebase:** ~2,850 LOC C# | 29 files changed (+2,064 / -44)

**Key accomplishments:**

1. Extended `GET /api/v1/transactions` with inclusive date-range filters (`from`/`to`), multi-category Guid filters (`categoryIds`), and optional 1-based pagination (`page`/`pageSize`) — all backward-compatible.
2. Replaced `GetAllTransactionsQuery` with `GetTransactionsListQuery` backed by an `IQueryable` pipeline executing all filters server-side via EF Core.
3. Paged responses return a `PagedTransactionsResponseDto` envelope (`items` + `totalCount`); unpaged requests preserve the original flat-list contract ordered by `CreatedAt` desc.
4. Deterministic paging order (`TransactionDate` desc, `Id` desc) prevents duplicate/skipped rows across pages.
5. Controller-level guardrails: `pageSize` capped at 20 (400 on violation), partial paging params rejected (400), empty `categoryIds` key rejected (400).
6. Eight integration tests (`TransactionsList_*`) assert every TRX-01..TRX-09 requirement on the real HTTP pipeline with an in-memory database — all green.

**Git range:** `docs: initialize project` → `feat(transactions): filters, pagination, and phase 1 completion`

---
