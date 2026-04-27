# Phase 3: Fix calendar-based frequency interval logic for monthly, quarterly, and annual recurrences - Context

**Gathered:** 2026-04-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Implement a `RecurrenceCalculator` that computes the next `NextOccurrenceDate` for a `RecurringTransaction` given its `Frequency` type and current date. This is the pure calculation layer — it does not read from or write to the database, does not generate `Transaction` instances, and does not advance `NextOccurrenceDate` on the template. The background service in Phase 4 will call this calculator.

All eight `FrequencyType` values must be handled. This phase does NOT implement the background generation service, the API endpoints for recurring transactions, or the pause/cancel/skip lifecycle — those belong in Phases 4 and 5.

</domain>

<decisions>
## Implementation Decisions

### FrequencyType Coverage
- **D-01:** All eight `FrequencyType` values are handled in one calculator:
  - Calendar-based (add months): `Monthly`, `Quarterly`, `SemiAnnually`, `Annually`
  - Fixed-offset (add days): `Daily`, `Weekly`, `BiWeekly`, `Custom` (uses `Frequency.IntervalDays`)

### End-of-Month Anchoring
- **D-02:** Calendar-based types use **snap-back anchoring**: the intended day-of-month is derived from `StartDate`. Each call tries to land on that day, clamping to the last day of the shorter month when necessary. This means Jan 31 → Feb 28 → **Mar 31** → Apr 30 → **May 31** (not a permanent drift to the 28th after February).
- **D-03:** The snap-back anchor is always the day-of-month of `StartDate`, not the day-of-month of `currentDate`. The calculator signature must therefore accept `startDate` as a parameter.

### Calculator Architecture
- **D-04:** Implemented as a static class `RecurrenceCalculator` in `FinanceTracker.Domain` (e.g., `FinanceTracker.Domain/Services/RecurrenceCalculator.cs`). Pure functions, no dependencies, no interface needed — this is deterministic math with no reason for alternative implementations.
- **D-05:** Primary method signature: `RecurrenceCalculator.NextOccurrence(FrequencyType type, int? intervalDays, DateTime currentDate, DateTime startDate) → DateTime`

### Testing
- **D-06:** Unit tests cover happy path (one test per `FrequencyType`) plus key edge cases: Feb 29 leap year, month-end snap-back (Jan 31 → Feb 28 → Mar 31), and year-end rollover (Dec 31 → Jan 31). More edge case tests can be added in later phases.

### Claude's Discretion
- Exact snap-back implementation detail (whether to compute `targetDay = startDate.Day` and then call a helper, or use a known pattern like `new DateTime(year, month, Math.Min(targetDay, daysInMonth))`)
- Handling of `Custom` type when `IntervalDays` is null (defensive throw vs. return unchanged date)
- Placement of the static class within the Domain project (suggested: `FinanceTracker.Domain/Services/`)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Domain Entities
- `FinanceTracker.Domain/Entities/Frequency.cs` — `FrequencyType` enum (all 8 values) and `IntervalDays` for `Custom` type
- `FinanceTracker.Domain/Entities/RecurringTransaction.cs` — `StartDate`, `NextOccurrenceDate` fields that the calculator operates on

### Prior Phase Context
- `.planning/phases/02-redesign-recurring-transaction-domain-model-with-template-and-instance-separation/02-CONTEXT.md` — D-06 documents `NextOccurrenceDate` ownership on template; D-03 documents template fields including `StartDate`

### Infrastructure (for reference only — Phase 3 does not modify these)
- `FinanceTracker.Infrastructure/Persistence/Configurations/RecurringTransactionConfiguration.cs` — EF Core config for the template entity
- `FinanceTracker.Tests/Domain/RecurringTransactionDomainModelTests.cs` — existing domain test file; new calculator tests should follow the same xUnit + FluentAssertions pattern

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `FrequencyType` enum in `Frequency.cs` — all 8 values already defined; calculator switches on this
- `RecurringTransaction.StartDate` + `NextOccurrenceDate` — the two date fields the calculator reads

### Established Patterns
- Domain entities live in `FinanceTracker.Domain/Entities/`; a new `Services/` subfolder is the natural home for `RecurrenceCalculator`
- Tests use xUnit `[Fact]` / `[Theory]` with FluentAssertions — follow `RecurringTransactionDomainModelTests.cs` as the model
- All existing domain logic is side-effect-free — `RecurrenceCalculator` fits this pattern as a pure static class

### Integration Points
- Phase 4 background service will call `RecurrenceCalculator.NextOccurrence(...)` to advance `NextOccurrenceDate` after generating a `Transaction` instance
- No changes to existing EF Core configuration, migrations, or application handlers in this phase

</code_context>

<specifics>
## Specific Ideas

- Snap-back behavior: Jan 31 → Feb 28 → Mar 31 → Apr 30 → May 31 (honors original day-of-month from `StartDate` whenever the month allows it)
- More exhaustive edge-case unit tests (Jan 29/30/31 across all calendar types, non-leap Feb, quarter boundaries) deferred to a future phase or alongside Phase 4

</specifics>

<deferred>
## Deferred Ideas

- Additional edge-case unit tests (Jan 29/30/31, non-leap Feb 29, full quarter boundary matrix) — user noted "we'll add more unit tests later"
- Background service that calls the calculator — Phase 4
- Pause/cancel/skip lifecycle — Phase 5

</deferred>

---

*Phase: 03-fix-calendar-based-frequency-interval-logic-for-monthly-quarterly-and-annual-recurrences*
*Context gathered: 2026-04-26*
