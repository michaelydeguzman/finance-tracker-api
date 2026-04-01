# Requirements: Finance Tracker API

**Defined:** 2026-03-31  
**Core Value:** Users can reliably record and retrieve transactions with flexible filtering for reporting and UI views.

## v1 Requirements

### Transactions — Listing, filtering, pagination

- [x] **TRX-01**: User can filter transactions by `TransactionDate` using optional `from` and `to` query params (inclusive bounds)
- [x] **TRX-02**: User can filter transactions by multiple categories using `categoryIds[]` (array of category IDs)
- [x] **TRX-03**: If `categoryIds` is omitted/null, the API applies `categoryType` filtering when provided (backward-compatible)
- [x] **TRX-04**: User can request optional pagination using 1-based `page` and `pageSize`
- [x] **TRX-05**: When pagination params are provided, the API returns an envelope containing `items` and `totalCount`
- [x] **TRX-06**: Paginated results are deterministically ordered by default (`TransactionDate` desc, then `Id` tie-break)
- [x] **TRX-07**: For paginated requests, `pageSize` is capped at 20
- [x] **TRX-08**: When pagination params are omitted, the endpoint preserves current behavior and returns the full list (no paging)
- [x] **TRX-09**: If `categoryIds` is present but empty (`[]`), the API rejects the request with a clear 400 error (client bug guardrail)

## v2 Requirements

### Transactions — Power user features

- **TRX-10**: User can sort transactions by additional fields (e.g. amount) while preserving stable paging semantics
- **TRX-11**: User can export filtered transactions to CSV
- **TRX-12**: User can search transactions by text (merchant/name/description)

## Out of Scope

| Feature | Reason |
|---------|--------|
| Dashboard aggregate/report endpoints | Not needed for current “list with filters” use case |
| Timezone normalization / UTC policy changes | Contract remains “client supplies intended bounds” |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| TRX-01 | Phase 1 | Complete |
| TRX-02 | Phase 1 | Complete |
| TRX-03 | Phase 1 | Complete |
| TRX-04 | Phase 1 | Complete |
| TRX-05 | Phase 1 | Complete |
| TRX-06 | Phase 1 | Complete |
| TRX-07 | Phase 1 | Complete |
| TRX-08 | Phase 1 | Complete |
| TRX-09 | Phase 1 | Complete |

**Coverage:**
- v1 requirements: 9 total
- Mapped to phases: 9
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-31*  
*Last updated: 2026-03-31 after initial definition*

