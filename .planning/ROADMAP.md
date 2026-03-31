# Roadmap — Finance Tracker API

**Milestone focus:** Transactions list filters + pagination  
**Granularity:** standard  
**Last updated:** 2026-03-31

## Phases

- [ ] **Phase 1: Transactions list — filters + pagination** - Extend `GET /transactions` to support date range, multi-category filters, and optional deterministic pagination (with total count and guardrails).

## Phase Details

### Phase 1: Transactions list — filters + pagination
**Goal**: Clients can request transactions with reliable filtering and optional pagination without breaking existing callers.
**Depends on**: Nothing (baseline exists)
**Requirements**: TRX-01, TRX-02, TRX-03, TRX-04, TRX-05, TRX-06, TRX-07, TRX-08, TRX-09
**Success Criteria** (what must be TRUE):
  1. Calling `GET /api/v1/transactions?from=YYYY-MM-DD&to=YYYY-MM-DD` returns only transactions with `TransactionDate` within the inclusive bounds.
  2. Calling `GET /api/v1/transactions?categoryIds=1&categoryIds=2` returns only transactions in those categories; if `categoryIds` is omitted, `categoryType` filtering still behaves as before.
  3. Calling `GET /api/v1/transactions?page=1&pageSize=10` returns an envelope with `items` and `totalCount`, and `items` are ordered by `TransactionDate` desc then `Id` desc for stable paging.
  4. For paginated requests, `pageSize` never exceeds 20 (requests above cap are rejected or coerced per requirement interpretation) and `page` is treated as 1-based.
  5. Calling `GET /api/v1/transactions` with no pagination parameters preserves current behavior by returning the full list (no paging envelope requirement beyond existing conventions).
  6. Calling `GET /api/v1/transactions?categoryIds=` (present but empty array semantics) is rejected with a clear 400 error.
**Plans**: TBD

## Progress

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Transactions list — filters + pagination | 0/1 | Not started | - |
