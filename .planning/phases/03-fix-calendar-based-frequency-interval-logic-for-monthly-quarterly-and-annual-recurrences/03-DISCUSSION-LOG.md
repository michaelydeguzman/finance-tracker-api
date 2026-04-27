# Phase 3: Fix calendar-based frequency interval logic - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-26
**Phase:** 03-fix-calendar-based-frequency-interval-logic-for-monthly-quarterly-and-annual-recurrences
**Areas discussed:** End-of-month anchoring, Scope of FrequencyTypes, Where the logic lives, Testing expectations

---

## End-of-month anchoring

| Option | Description | Selected |
|--------|-------------|----------|
| Snap-back (anchor-based) | Honor original day-of-month from `StartDate`; Jan 31 → Feb 28 → Mar 31 → Apr 30 → May 31 | |
| Clamp-and-drift | Use `AddMonths()` directly on current date; Jan 31 → Feb 28 → Mar 28 → Apr 28 | |
| You decide | Leave to Claude's discretion | ✓ |

**User's choice:** Claude's discretion
**Notes:** Claude elected snap-back anchoring — most user-friendly for personal finance (end-of-month bills should stay near month-end). `StartDate.Day` used as the anchor.

---

## Scope of FrequencyTypes

| Option | Description | Selected |
|--------|-------------|----------|
| All calendar-based | Monthly, Quarterly, SemiAnnually, Annually get calendar math; Daily/Weekly/BiWeekly/Custom use AddDays | ✓ |
| Title only | Only Monthly, Quarterly, Annually; SemiAnnually and Custom left for later | |
| You decide | Leave to Claude's discretion | |

**User's choice:** All calendar-based types in one calculator
**Notes:** Avoids an awkward split where Phase 4 would need to handle leftover FrequencyTypes.

---

## Where the logic lives

| Option | Description | Selected |
|--------|-------------|----------|
| Static domain service | `RecurrenceCalculator` static class in `FinanceTracker.Domain` | ✓ |
| Interface + implementation | `IRecurrenceCalculator` in Application, implementation in Domain | |
| You decide | Leave to Claude's discretion | |

**User's choice:** Static domain service
**Notes:** Pure math with no dependencies; no reason for an abstraction over deterministic date arithmetic.

---

## Testing expectations

| Option | Description | Selected |
|--------|-------------|----------|
| Happy path + key edge cases | One test per FrequencyType, plus Feb 29, month-end snap-back, year-end rollover | ✓ |
| Exhaustive edge cases | All of the above plus Jan 29/30/31, non-leap Feb, all quarter boundaries, EndDate boundary | |
| You decide | Leave to Claude's discretion | |

**User's choice:** Happy path + key edge cases
**Notes:** User explicitly noted "we'll add more unit tests later" — exhaustive coverage deferred.

---

## Claude's Discretion

- End-of-month snap-back implementation detail (elected by Claude: `targetDay = startDate.Day`, clamp with `DateTime.DaysInMonth`)
- Handling of `Custom` type when `IntervalDays` is null
- Placement of static class within Domain project (suggested: `FinanceTracker.Domain/Services/`)

## Deferred Ideas

- Additional edge-case unit tests (Jan 29/30/31, non-leap Feb 29, full quarter boundary matrix) — noted for later
