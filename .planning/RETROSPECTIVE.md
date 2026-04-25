# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

## Milestone: v1.0 — Transactions List Filters + Pagination

**Shipped:** 2026-04-01  
**Phases:** 1 | **Plans:** 2 | **Duration:** ~40 min execution

### What Was Built

- `GET /api/v1/transactions` extended with date-range filters (`from`/`to`), multi-category Guid filters (`categoryIds`), and optional 1-based pagination with `items`/`totalCount` envelope.
- `GetTransactionsListQuery` handler using an EF Core `IQueryable` pipeline for all server-side filtering.
- Eight integration tests covering every TRX-01..TRX-09 requirement via `WebApplicationFactory` on an in-memory database.

### What Worked

- Splitting implementation (Plan 01) from integration tests (Plan 02) kept each plan focused and fast (~25 min and ~15 min respectively).
- `IQueryable` composition allowed filters to stack cleanly with no N+1 risk.
- Backward-compatibility decision made upfront avoided any ambiguity during implementation — unpaginated callers remain untouched.
- Strict controller-level validation (pageSize cap, partial paging, empty categoryIds) caught edge cases before they reached the handler.

### What Was Inefficient

- No audit step run before milestone completion (minor — all requirements were clearly complete).
- v2 requirements (TRX-10..TRX-12: sorting, CSV export, text search) identified early but only informally noted in REQUIREMENTS.md; next milestone should formalize them into active scope or explicitly defer.

### Patterns Established

- List endpoints use `GetTransactionsListResult` + `IActionResult` branching for flat-list vs paged-envelope responses — reuse for future list endpoints.
- Controller validates business constraints (pageSize, partial paging, empty arrays) before dispatching to MediatR handler.
- Integration tests use `HttpJsonOptions.ForApi` deserialization to handle list vs envelope shapes.

### Key Lessons

1. Two-plan split (implementation + tests) is the right granularity for a medium-sized feature — fast enough to finish in one session, clean enough to review independently.
2. Defining the backward-compatibility contract before writing a single line of code prevented scope ambiguity entirely.
3. The yolo config mode with `always_confirm_destructive: true` provides a good balance — no friction on planning steps, still protected on irreversible actions.

### Cost Observations

- Sessions: ~2 (research/planning + execution)
- Notable: Small, focused milestone with a single well-defined phase completed efficiently with minimal rework.

---

## Cross-Milestone Trends

### Process Evolution

| Milestone | Phases | Plans | Key Change |
|-----------|--------|-------|------------|
| v1.0 | 1 | 2 | Initial project — established IQueryable filter pattern and two-plan split convention |

### Cumulative Quality

| Milestone | Tests Added | Requirements Covered |
|-----------|-------------|---------------------|
| v1.0 | 8 integration | 9/9 (TRX-01..TRX-09) |

### Top Lessons (Verified Across Milestones)

1. Define backward-compatibility contracts explicitly before any implementation begins.
2. Separate implementation plans from test plans for cleaner review and faster parallel execution.
