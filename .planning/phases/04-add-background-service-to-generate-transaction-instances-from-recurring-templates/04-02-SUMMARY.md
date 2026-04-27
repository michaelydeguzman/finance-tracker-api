---
phase: 04-add-background-service-to-generate-transaction-instances-from-recurring-templates
plan: "02"
subsystem: worker-testing
tags: [tdd, xunit, moq, fluentassertions, efcore-inmemory, worker, recurring-transactions]
dependency_graph:
  requires:
    - phase: 04-01
      provides: IRecurringTransactionRepository stub, TransactionGenerationService skeleton, Worker project scaffold
    - phase: 03
      provides: RecurrenceCalculator.NextOccurrence with snap-back anchoring
  provides:
    - Full TransactionGenerationService.RunAsync + GenerateForTemplateAsync implementation (D-04..D-15)
    - 10 unit tests covering all locked decisions
  affects:
    - Phase 05 (pause/cancel/skip): builds on TransactionGenerationService and RecurringTransactionStatus
tech-stack:
  added: []
  patterns:
    - TDD RED→GREEN cycle with xUnit + InMemory EF Core
    - Mock<IRecurringTransactionRepository> via Moq to isolate query layer from generation logic
    - Per-template SaveChangesAsync for D-15 isolation (exception in one template does not abort batch)
    - EntityState.Detached cleanup on exception to prevent context pollution across templates
key-files:
  created:
    - FinanceTracker.Tests/Worker/TransactionGenerationServiceTests.cs
  modified:
    - FinanceTracker.Worker/Services/TransactionGenerationService.cs
key-decisions:
  - "AddMinutes(5) buffer on test dates: DateTime.UtcNow.AddDays(-N).AddMinutes(5) prevents loop over-counting from sub-millisecond clock drift between test setup and service execution (after N daily advances, NextOccurrenceDate lands 5 minutes in the future)"
  - "EntityState.Detached cleanup in RunAsync catch block: Added-but-unsaved Transaction entities are detached on exception to implement true D-15 per-template isolation in EF Core InMemory tests"
  - "Category = null! on new Transaction: C# required keyword satisfied with null-forgiveness; EF Core InMemory does not validate required nav properties at Add() time"
  - "D-13 EndDate check at TOP of while loop body: before generation AND before advancement — NextOccurrenceDate is never advanced when EndDate is exceeded"
patterns-established:
  - "Per-template error isolation pattern: try/catch around GenerateForTemplateAsync + detach Added entities on exception"
  - "TDD timing buffer: use DateTime.UtcNow.AddDays(-N).AddMinutes(M) for test dates to prevent clock-drift flakiness in catch-up loop tests"
requirements-completed: []
duration: ~15min
completed: 2026-04-27
---

# Phase 04 Plan 02: TDD — TransactionGenerationService Implementation Summary

**Core generation algorithm with catch-up loop (D-04..D-15): while loop, EndDate guard, field mapping, NextOccurrenceDate advancement via RecurrenceCalculator, and per-template exception isolation — verified by 10 unit tests (RED → GREEN).**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-04-27T07:35:00Z
- **Completed:** 2026-04-27T07:50:00Z
- **Tasks:** 2 (RED, GREEN)
- **Files modified:** 2

## Accomplishments

- 10 unit tests written first (RED) covering all locked decisions D-04 through D-15
- `TransactionGenerationService.RunAsync` + `GenerateForTemplateAsync` implemented — replaces Plan 01 stub
- Full TDD cycle complete: RED committed at `f9b7283`, GREEN committed at `0dfd78d`
- 47/47 tests pass: 10 new (all green) + 37 pre-existing (no regressions)

## Task Commits

Each task was committed atomically:

1. **Task 1 (RED): Write 10 failing tests** — `f9b7283` (test)
2. **Task 2 (GREEN): Implement TransactionGenerationService** — `0dfd78d` (feat)

## Files Created/Modified

- `FinanceTracker.Tests/Worker/TransactionGenerationServiceTests.cs` — 10 unit tests covering D-04..D-15; uses InMemory EF Core context + Moq repo; `CreateTemplate` factory helper
- `FinanceTracker.Worker/Services/TransactionGenerationService.cs` — full `RunAsync` + `GenerateForTemplateAsync`; D-13 EndDate guard; D-14 advancement via `RecurrenceCalculator.NextOccurrence`; D-15 isolation via try/catch + EntityState.Detached cleanup

## Decisions Made

- `DateTime.UtcNow.AddDays(-N).AddMinutes(5)` used for test dates to avoid loop over-counting from clock drift (see Deviations)
- `Category = null!` on new Transaction: FK-only insert sufficient; EF InMemory does not validate required nav at Add() time
- D-15 isolation implemented by detaching `EntityState.Added` entries on exception — prevents uncommitted rows from leaking into subsequent templates' `SaveChangesAsync`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed loop over-counting caused by sub-millisecond clock drift in tests**
- **Found during:** Task 2 (first run of tests in GREEN state)
- **Issue:** Tests used `DateTime.UtcNow.AddDays(-N)` for template dates. After N daily advances, `NextOccurrenceDate = T_test_setup`, which is a few ms BEFORE `now = DateTime.UtcNow` captured at service start. The `while (NextOccurrenceDate <= now)` condition fired one extra time, generating N+1 transactions instead of N.
- **Fix:** Added `.AddMinutes(5)` to all affected test dates. After N advances, `NextOccurrenceDate = T_setup + 5 min`, which is strictly after `now`. Loop stops at exactly N iterations. Test 7's `BeAfter(DateTime.UtcNow)` assertion also passes (5 min in future > current time at assertion).
- **Files modified:** `FinanceTracker.Tests/Worker/TransactionGenerationServiceTests.cs` (tests 1, 2, 6, 7, 8, 9, 10)
- **Verification:** `dotnet test` — 47/47 pass
- **Committed in:** `0dfd78d` (Task 2 commit)

**2. [Rule 1 - Bug] Fixed D-15 isolation: Added-but-unsaved entities leaked across templates**
- **Found during:** Task 2 (Test 10 failed — expected 1 transaction, found 3)
- **Issue:** When `badTemplate` threw `NullReferenceException` at `template.Frequency.Type` (inside the while loop AFTER `_context.Transactions.Add(transaction)` but BEFORE `await _context.SaveChangesAsync()`), the partially-added transaction remained tracked in the EF context. The subsequent `goodTemplate`'s `SaveChangesAsync()` committed BOTH the good template's transaction AND the bad template's unsaved row.
- **Fix:** In `RunAsync` catch block, iterate `_context.ChangeTracker.Entries()` filtered to `EntityState.Added` and set each to `EntityState.Detached`. This discards the uncommitted rows for the failed template before the next template is processed.
- **Files modified:** `FinanceTracker.Worker/Services/TransactionGenerationService.cs`
- **Verification:** Test 10 `RunAsync_OneTemplateThrows_OtherTemplateStillProcessed` passes — 1 transaction from goodTemplate, bad template's row discarded
- **Committed in:** `0dfd78d` (Task 2 commit)

**3. [Rule 1 - Bug] Added `Category = null!` to new Transaction in service**
- **Found during:** Task 2 (code review before running tests)
- **Issue:** `Transaction.Category` is declared `public required Category Category { get; set; }` in C# 11. The plan's implementation code omitted this property, which would cause compiler error CS9035 (required member not set in object initializer).
- **Fix:** Added `Category = null!` to satisfy the C# `required` constraint while not setting the actual nav property (EF Core does not validate required nav at `Add()` time for InMemory or SqlServer providers).
- **Files modified:** `FinanceTracker.Worker/Services/TransactionGenerationService.cs`
- **Verification:** Build succeeds 0 errors/warnings; tests pass
- **Committed in:** `0dfd78d` (Task 2 commit)

---

**Total deviations:** 3 auto-fixed (3 × Rule 1 - Bug)
**Impact on plan:** All three fixes were necessary for correctness. No scope creep. The timing buffer fix is a test-quality improvement that makes the tests deterministic and non-flaky.

## Issues Encountered

None beyond the three auto-fixed bugs documented above.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- `TransactionGenerationService.RunAsync` is fully implemented and tested: call from `Program.cs` (wired in Plan 01) is now functional end-to-end
- Phase 04 is complete: both plans done, 47 tests pass, Worker console app ready for Windows Task Scheduler deployment
- Phase 05 (pause/cancel/skip recurring transactions) can begin; it depends on `RecurringTransactionStatus.Paused/Cancelled` which this phase exercises via D-05/D-06

## Self-Check

- `FinanceTracker.Tests/Worker/TransactionGenerationServiceTests.cs` — FOUND
- `FinanceTracker.Worker/Services/TransactionGenerationService.cs` — FOUND
- Commit `f9b7283` (RED) — FOUND
- Commit `0dfd78d` (GREEN) — FOUND

---
*Phase: 04-add-background-service-to-generate-transaction-instances-from-recurring-templates*
*Completed: 2026-04-27*
