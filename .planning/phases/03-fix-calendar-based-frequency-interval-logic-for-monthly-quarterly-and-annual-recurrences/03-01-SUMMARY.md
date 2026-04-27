---
phase: 03-fix-calendar-based-frequency-interval-logic-for-monthly-quarterly-and-annual-recurrences
plan: 01
subsystem: domain
tags: [tdd, recurrence, calendar, domain-service]
dependency_graph:
  requires: []
  provides: [RecurrenceCalculator.NextOccurrence]
  affects: [Phase 04 background service]
tech_stack:
  added: []
  patterns: [TDD red-green, static pure domain service, snap-back anchoring]
key_files:
  created:
    - FinanceTracker.Domain/Services/RecurrenceCalculator.cs
    - FinanceTracker.Tests/Domain/RecurrenceCalculatorTests.cs
  modified: []
decisions:
  - "targetDay = startDate.Day (not currentDate.Day) is the snap-back anchor — prevents permanent drift after short-month clamping (D-03)"
  - "7-argument DateTime constructor preserves DateTimeKind through snap-back calculation"
  - "Default switch arm throws ArgumentOutOfRangeException — ensures future FrequencyType additions fail fast"
metrics:
  duration_minutes: 2
  completed_date: "2026-04-27"
  tasks_completed: 2
  files_created: 2
  files_modified: 0
---

# Phase 03 Plan 01: RecurrenceCalculator TDD Implementation Summary

**One-liner:** Pure static domain service with snap-back anchoring prevents month-end drift across all 8 FrequencyType values via startDate.Day anchor.

## What Was Built

`RecurrenceCalculator` — a pure static class in `FinanceTracker.Domain.Services` — implementing calendar-based next-occurrence date calculation for all 8 `FrequencyType` values. The snap-back design ensures that a recurring transaction starting on the 31st always returns to the 31st (or the last day of the month if shorter), never permanently drifting to a smaller day.

## Tasks Executed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | RED: Tests + skeleton | b92888d | RecurrenceCalculatorTests.cs, RecurrenceCalculator.cs (skeleton) |
| 2 | GREEN: Full implementation | 7792f47 | RecurrenceCalculator.cs (implementation) |

## Decisions Made

1. **`targetDay = startDate.Day` as snap-back anchor** — The target day is always derived from `startDate.Day`, never `currentDate.Day`. After a month-end clamp (e.g., Jan 31 → Feb 28), the March occurrence still returns to the 31st because the anchor is the original start date. This is the core behavior that fixes D-02/D-03.

2. **7-argument `DateTime` constructor** — `new DateTime(y, m, d, hour, minute, second, kind)` preserves `DateTimeKind` through the snap-back calculation. The 3-argument overload would silently produce `DateTimeKind.Unspecified` and break UTC-aware consumers.

3. **Exhaustive switch with default throw** — The `_ => throw new ArgumentOutOfRangeException(...)` default arm ensures adding a new `FrequencyType` value without updating the switch immediately surfaces as a runtime error rather than silent wrong output.

## Verification Results

- `dotnet test FinanceTracker.Tests --filter "FullyQualifiedName~RecurrenceCalculator"` → **12/12 passed**
- `dotnet test FinanceTracker.Tests` → **37/37 passed** (25 existing + 12 new)
- Snap-back regression: `NextOccurrence_Monthly_SnapBackAfterFebruary_ReturnsMarch31` passes ✓
- No files outside `FinanceTracker.Domain/Services/` and `FinanceTracker.Tests/Domain/` were modified ✓
- No new NuGet packages added ✓

## Deviations from Plan

None — plan executed exactly as written.

## Known Stubs

None — all logic is fully wired. No placeholder values, hardcoded returns, or TODO markers in the produced files.

## Self-Check: PASSED

- `FinanceTracker.Domain/Services/RecurrenceCalculator.cs` — FOUND
- `FinanceTracker.Tests/Domain/RecurrenceCalculatorTests.cs` — FOUND
- Commit `b92888d` — FOUND (test(03-01): add failing RecurrenceCalculator tests + skeleton)
- Commit `7792f47` — FOUND (feat(03-01): implement RecurrenceCalculator with snap-back anchoring)
