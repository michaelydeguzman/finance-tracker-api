# STATE — Finance Tracker API

**Last updated:** 2026-03-31

## Project Reference

- **Core value**: Users can reliably record and retrieve transactions with flexible filtering for reporting and UI views.
- **Current milestone focus**: Transactions list filters + pagination
- **Primary artifact**: `.planning/ROADMAP.md`

## Current Position

- **Current phase**: Phase 1 — Transactions list — filters + pagination
- **Status**: Not started
- **Progress**: 0%

## Phase Tracking

| Phase | Status | Notes |
|------:|--------|------|
| 1 | Not started | Implement filters (`from`/`to`, `categoryIds[]` + fallback), optional paging with stable ordering, envelope + `totalCount`, and request guardrails |

## Decisions (sticky)

- Keep `GET /transactions` backward-compatible: omit paging params → full list (current behavior).
- Pagination is optional and **1-based** with deterministic ordering: `TransactionDate` desc, then `Id` as tie-break.
- API remains “timezone dumb”: client supplies intended bounds.

## Blockers / Risks

- None recorded.

## Notes / Context

- v1 requirements tracked in `.planning/REQUIREMENTS.md` (TRX-01..TRX-09).
