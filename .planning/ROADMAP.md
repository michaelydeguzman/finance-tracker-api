# Roadmap — Finance Tracker API

**Last updated:** 2026-04-25 (v1.0 archived)

## Milestones

- ✅ **v1.0 Transactions List Filters + Pagination** — Phase 1 (shipped 2026-04-01)

## Phases

<details>
<summary>✅ v1.0 Transactions List Filters + Pagination (Phase 1) — SHIPPED 2026-04-01</summary>

- [x] Phase 1: Transactions list — filters + pagination (2/2 plans) — completed 2026-04-01

</details>

## Progress

| Phase | Milestone | Plans Complete | Status   | Completed  |
|-------|-----------|----------------|----------|------------|
| 1. Transactions list — filters + pagination | v1.0 | 1/1 | Complete   | 2026-04-26 |

### Phase 1: Clean up recurring transaction dead code and reconcile domain projects

**Goal:** Delete the dead `FinanceTracker.Domain/` draft project and rename the live `Finance.Tracker.Domain/` folder to `FinanceTracker.Domain/` so all projects follow the `FinanceTracker.*` naming convention; solution builds with 0 errors and 0 warnings.
**Requirements**: none (structural cleanup — no formal requirement IDs)
**Depends on:** Phase 0
**Plans:** 1/1 plans complete

Plans:
- [x] 01-01-PLAN.md — Delete dead domain project, rename live domain folder, update .sln and .csproj references, verify clean build

### Phase 2: Redesign recurring transaction domain model with template and instance separation

**Goal:** Introduce `RecurringTransaction` as a template entity (master definition) with its own `RecurringTransactions` table, wire individual `Transaction` instances back to it via a nullable `RecurringTransactionId` FK, remove `Transaction.FrequencyId`, and generate a schema migration that safely nulls existing FrequencyId data — giving Phases 3–5 a clean domain foundation to build on.
**Requirements**: none (structural domain model work — no formal requirement IDs; see decision IDs D-01..D-10 in CONTEXT.md)
**Depends on:** Phase 1
**Plans:** 2 plans

Plans:
- [x] 02-01-PLAN.md — Create RecurringTransaction entity + enum, restructure Transaction/Frequency/Category entities, remove all FrequencyId references from app/infra/test layers (clean build)
- [ ] 02-02-PLAN.md — Create RecurringTransactionConfiguration, update TransactionConfiguration + DbContext, generate migration with data-nulling SQL, add RecurringTransaction domain model tests (22+ tests pass)

### Phase 3: Fix calendar-based frequency interval logic for monthly, quarterly, and annual recurrences

**Goal:** [To be planned]
**Requirements**: TBD
**Depends on:** Phase 2
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd-plan-phase 3 to break down)

### Phase 4: Add background service to generate transaction instances from recurring templates

**Goal:** [To be planned]
**Requirements**: TBD
**Depends on:** Phase 3
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd-plan-phase 4 to break down)

### Phase 5: Add pause, cancel, and skip capabilities for recurring transactions

**Goal:** [To be planned]
**Requirements**: TBD
**Depends on:** Phase 4
**Plans:** 0 plans

Plans:
- [ ] TBD (run /gsd-plan-phase 5 to break down)

---
*Full milestone details: `.planning/milestones/v1.0-ROADMAP.md`*
