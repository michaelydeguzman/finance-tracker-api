# Finance Tracker API

## What This Is

An ASP.NET Core (.NET 8) REST API for tracking personal finances. It exposes versioned endpoints to manage finance data (e.g. transactions, categories, frequencies) backed by SQL Server via EF Core. Multi-user, with each user seeing only their own data.

## Current Milestone: v1.1 — Authentication & Authorization

**Goal:** Add multi-user authentication with JWT bearer tokens, Google SSO, and per-user data isolation to the Finance Tracker API.

**Target features:**
- User registration with email/password (auto-verified, no email sending this milestone)
- User login returning a JWT bearer token
- Google SSO — register or log in via Google OAuth2
- JWT middleware protecting all existing API endpoints
- Per-user data isolation — transactions scoped to the authenticated user
- Seed admin user for local/test login
- Existing transactions migrated to seed admin user

## Core Value

Users can reliably record and retrieve transactions with flexible filtering for reporting and UI views.

## Requirements

### Validated

- ✓ Versioned `GET /api/v{version}/transactions` exists and returns transactions — existing
- ✓ Transactions have `TransactionDate` and a `Category` relationship — existing
- ✓ Category-type filtering exists on transactions list (`categoryType`) — existing
- ✓ **Phase 1:** Date range (`from` / `to`), multi-category `categoryIds` (Guids), optional paging (`page` / `pageSize`) with `items` + `totalCount`, backward-compatible unpaginated list, `pageSize` ≤ 20, empty `categoryIds` rejected — TRX-01..TRX-09

### Active

- [ ] User registration (email/password, auto-verified)
- [ ] User login with JWT bearer token
- [ ] Google OAuth2 login/registration (SSO)
- [ ] JWT middleware protecting all API endpoints
- [ ] Per-user transaction data isolation
- [ ] Seed admin user for testing
- [ ] Existing transactions assigned to seed admin user

### Out of Scope

- Dashboard/aggregates endpoints (totals, charts, rollups) — not needed for current "list with filters" use case
- Timezone normalization/UTC policy changes — keep API "dumb"; client supplies intended bounds
- Email verification / password reset emails — deferred to future milestone
- Household/shared access — multiple users viewing same transactions — future milestone
- Role-based authorization — flat "authenticated = can do everything" for now

## Context

- Current stack: .NET 8, ASP.NET Core, MediatR, EF Core (SQL Server), API versioning, Swagger.
- Transactions list endpoint: `FinanceTracker/Controllers/TransactionsV1Controller.cs` dispatches `GetTransactionsListQuery` (MediatR).
- List supports `categoryType`, optional `from`/`to`, `categoryIds`, and optional paging with distinct ordering for paged vs unpaged responses.
- **v1.0 shipped:** ~2,850 LOC C# across the solution; 8 integration tests cover all TRX requirements.
- **v1.1 adding:** ASP.NET Core Identity, JWT bearer auth, Google OAuth2, per-user data scoping.
- **Phase 01 complete:** Dead `FinanceTracker.Domain/` draft project purged; live `Finance.Tracker.Domain/` renamed to `FinanceTracker.Domain/` — all projects now follow `FinanceTracker.*` naming. Solution builds 0 errors/warnings, 21 tests pass.
- **Phase 02 complete:** `RecurringTransaction` template entity introduced with its own `RecurringTransactions` table; `Transaction.FrequencyId` removed; nullable `RecurringTransactionId` FK wired; EF Core migration safely nulls existing FrequencyId data. Build: 0 errors/warnings, 25 tests pass.
- **Phase 03 complete:** `RecurrenceCalculator` pure static class added to `FinanceTracker.Domain/Services/`; snap-back anchoring (`targetDay = startDate.Day`) prevents month-end drift; all 8 `FrequencyType` values handled; 12 new tests + 25 prior = 37 total pass.
- **Phase 04 complete:** `FinanceTracker.Worker` console app scaffolded with `IRecurringTransactionRepository` + `RecurringTransactionRepository` (EF Core eager-loading, no `AsNoTracking`); full `TransactionGenerationService` implements catch-up loop (D-04), EndDate boundary (D-13), NextOccurrenceDate advancement via `RecurrenceCalculator` (D-14), per-template error isolation with EF context cleanup (D-15), and all field-mapping decisions (D-07..D-12); 10 new TDD tests + 37 prior = 47 total pass.
- Known technical debt: none recorded from v1.0.

## Constraints

- **Backward compatibility**: existing callers of `GET /transactions` without pagination params must still receive full list
- **Performance**: paging must have stable ordering; paged responses include `totalCount`
- **API contract**: pagination is 1-based; `pageSize` must not exceed 20
- **Security**: all endpoints must require authentication; unauthenticated requests return 401

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Add filters to existing `GET /transactions` | Same resource & representation; avoid duplicated list endpoints | ✓ Phase 1 |
| `categoryIds` is an array filter; fallback to `categoryType` if `categoryIds` absent | Supports multi-select UI while keeping existing query param useful | ✓ Phase 1 |
| Date filtering uses optional `from`/`to` | Supports presets + custom range consistently | ✓ Phase 1 |
| API stays "dumb" about timezone | Keep contract simple; FE sends explicit bounds | ✓ Phase 1 |
| Pagination is optional and 1-based; paged order `TransactionDate` desc, then `Id`; unpaged keeps `CreatedAt` desc | Predictable UX and stable paging; backward compatibility | ✓ Phase 1 |
| Return envelope with `totalCount` for paged responses | Enables FE pagination UI | ✓ Phase 1 |
| Cap `pageSize` at 20 | Prevent heavy queries from large limits | ✓ Phase 1 |
| JWT bearer tokens (not cookies) | API-first; stateless; easier for future mobile/web clients | — Pending |
| Google OAuth2 via ASP.NET Core Identity external login | Standard integration path; avoids custom OAuth2 plumbing | — Pending |
| Auto-verify registration (no email step) | Simplify v1.1; email flows deferred to future milestone | — Pending |

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
*Last updated: 2026-04-27 after Phase 04 completion*
