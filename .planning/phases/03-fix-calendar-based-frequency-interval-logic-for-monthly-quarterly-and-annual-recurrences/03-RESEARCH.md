# Phase 3: Fix Calendar-Based Frequency Interval Logic - Research

**Researched:** 2026-04-26
**Domain:** .NET DateTime arithmetic, calendar-based recurrence calculation
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** All eight `FrequencyType` values handled in one calculator:
  - Calendar-based (add months): `Monthly`, `Quarterly`, `SemiAnnually`, `Annually`
  - Fixed-offset (add days): `Daily`, `Weekly`, `BiWeekly`, `Custom` (uses `Frequency.IntervalDays`)
- **D-02:** Calendar-based types use **snap-back anchoring**: intended day-of-month derived from `StartDate`. Each call tries to land on that day, clamping to last day of shorter month when necessary. Jan 31 → Feb 28 → **Mar 31** → Apr 30 → **May 31** (not permanent drift).
- **D-03:** Snap-back anchor is always `StartDate.Day`, not `currentDate.Day`. Calculator signature must accept `startDate`.
- **D-04:** Implemented as static class `RecurrenceCalculator` in `FinanceTracker.Domain/Services/RecurrenceCalculator.cs`. Pure functions, no dependencies, no interface needed.
- **D-05:** Primary method signature: `RecurrenceCalculator.NextOccurrence(FrequencyType type, int? intervalDays, DateTime currentDate, DateTime startDate) → DateTime`
- **D-06:** Unit tests cover happy path (one test per `FrequencyType`) plus key edge cases: Feb 29 leap year, month-end snap-back (Jan 31 → Feb 28 → Mar 31), and year-end rollover (Dec 31 → Jan 31).

### Claude's Discretion

- Exact snap-back implementation detail (whether to compute `targetDay = startDate.Day` and then call a helper, or use a known pattern like `new DateTime(year, month, Math.Min(targetDay, daysInMonth))`)
- Handling of `Custom` type when `IntervalDays` is null (defensive throw vs. return unchanged date)
- Placement of the static class within the Domain project (suggested: `FinanceTracker.Domain/Services/`)

### Deferred Ideas (OUT OF SCOPE)

- Additional edge-case unit tests (Jan 29/30/31, non-leap Feb 29, full quarter boundary matrix)
- Background service that calls the calculator — Phase 4
- Pause/cancel/skip lifecycle — Phase 5
</user_constraints>

---

## Summary

Phase 3 is purely algorithmic: implement a static class that maps a `FrequencyType` (plus optional `intervalDays`) to the next occurrence `DateTime` given the current date and the original start date. No EF Core, no DI, no migrations — just math.

The core problem is that `DateTime.AddMonths()` in .NET permanently drifts day-of-month after a short month. If a recurring transaction starts on Jan 31, calling `AddMonths(1)` lands on Feb 28 correctly, but the *next* call on Feb 28 yields Mar 28 — not Mar 31. Snap-back anchoring solves this by always re-reading the intended day from `StartDate.Day` and clamping to `DateTime.DaysInMonth(year, month)` at each step.

Fixed-offset types (Daily, Weekly, BiWeekly, Custom) have no such problem — `AddDays()` is exact and needs no anchoring.

The test suite for this phase is the most important deliverable alongside the class itself. The edge-case tests (D-06) serve as executable documentation of the snap-back contract.

**Primary recommendation:** Implement a private `AddMonthsWithSnapBack(DateTime current, int targetDay, int months) → DateTime` helper; the public `NextOccurrence` method dispatches to it for calendar types and to `AddDays` for fixed-offset types.

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `System` (BCL) | .NET 8 | `DateTime`, `DateTime.DaysInMonth`, `AddMonths`, `AddDays` | Built-in; no NuGet dependency |
| xUnit | 2.9.2 (already installed) | Test framework | Already in `FinanceTracker.Tests.csproj` |
| FluentAssertions | 6.12.2 (already installed) | Test assertions | Already in `FinanceTracker.Tests.csproj` |

### Supporting

None required. This phase adds zero new NuGet packages.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| BCL `DateTime` arithmetic | NodaTime | NodaTime is superior for calendar math but is a third-party dependency not present in the project; BCL is sufficient for the snap-back pattern we're implementing |
| `[Theory] + [InlineData]` | `[Theory] + [MemberData]` | InlineData is simpler for the discrete test matrix here |

---

## Architecture Patterns

### Recommended Project Structure

```
FinanceTracker.Domain/
├── Entities/
│   ├── Category.cs
│   ├── Frequency.cs          ← FrequencyType enum lives here
│   ├── RecurringTransaction.cs
│   └── Transaction.cs
└── Services/                 ← NEW folder
    └── RecurrenceCalculator.cs
```

```
FinanceTracker.Tests/
└── Domain/
    ├── RecurringTransactionDomainModelTests.cs   ← existing pattern to follow
    └── RecurrenceCalculatorTests.cs              ← NEW
```

### Pattern 1: Snap-Back Month Addition

**What:** Derive intended day from `startDate.Day`; after `AddMonths`, override the day with `Math.Min(targetDay, DateTime.DaysInMonth(year, month))`.

**When to use:** All calendar-based frequency types (Monthly, Quarterly, SemiAnnually, Annually).

**Why this works:**
- `AddMonths` correctly advances the year/month (including year rollover at December).
- The returned `.Day` from `AddMonths` may be clamped (Feb case) — we discard it.
- We substitute `Math.Min(targetDay, DaysInMonth)` to get the correct day for the target month.
- Because `targetDay` always comes from `startDate.Day` (not `currentDate.Day`), we never accumulate drift across multiple calls.

**Example:**
```csharp
// Source: BCL documentation + standard recurrence pattern
private static DateTime AddMonthsWithSnapBack(DateTime currentDate, int targetDay, int months)
{
    // AddMonths handles year rollover correctly (e.g., Dec + 1 → Jan next year)
    DateTime shifted = currentDate.AddMonths(months);
    int clampedDay = Math.Min(targetDay, DateTime.DaysInMonth(shifted.Year, shifted.Month));
    return new DateTime(shifted.Year, shifted.Month, clampedDay,
                        currentDate.Hour, currentDate.Minute, currentDate.Second,
                        currentDate.Kind);
}
```

**Public dispatch:**
```csharp
public static DateTime NextOccurrence(
    FrequencyType type,
    int? intervalDays,
    DateTime currentDate,
    DateTime startDate)
{
    int targetDay = startDate.Day;

    return type switch
    {
        FrequencyType.Daily        => currentDate.AddDays(1),
        FrequencyType.Weekly       => currentDate.AddDays(7),
        FrequencyType.BiWeekly     => currentDate.AddDays(14),
        FrequencyType.Monthly      => AddMonthsWithSnapBack(currentDate, targetDay, 1),
        FrequencyType.Quarterly    => AddMonthsWithSnapBack(currentDate, targetDay, 3),
        FrequencyType.SemiAnnually => AddMonthsWithSnapBack(currentDate, targetDay, 6),
        FrequencyType.Annually     => AddMonthsWithSnapBack(currentDate, targetDay, 12),
        FrequencyType.Custom       => currentDate.AddDays(
                                          intervalDays ?? throw new ArgumentException(
                                              "IntervalDays must be set for Custom frequency.", nameof(intervalDays))),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unhandled FrequencyType.")
    };
}
```

### Pattern 2: xUnit Theory for FrequencyType Coverage

**What:** One `[Theory]` with `[InlineData]` for each of the 8 `FrequencyType` values (happy path). Separate `[Fact]` tests for each named edge case.

**When to use:** When input is a discrete enum — `[Theory]` + `[InlineData]` produces clearer failure messages than a single `[Fact]` with a loop.

**Example structure:**
```csharp
// Happy path — one test case per FrequencyType
[Theory]
[InlineData(FrequencyType.Daily,        null,  "2026-01-15", "2026-01-01", "2026-01-16")]
[InlineData(FrequencyType.Weekly,       null,  "2026-01-15", "2026-01-01", "2026-01-22")]
[InlineData(FrequencyType.BiWeekly,     null,  "2026-01-15", "2026-01-01", "2026-01-29")]
[InlineData(FrequencyType.Monthly,      null,  "2026-01-15", "2026-01-15", "2026-02-15")]
[InlineData(FrequencyType.Quarterly,    null,  "2026-01-15", "2026-01-15", "2026-04-15")]
[InlineData(FrequencyType.SemiAnnually, null,  "2026-01-15", "2026-01-15", "2026-07-15")]
[InlineData(FrequencyType.Annually,     null,  "2026-01-15", "2026-01-15", "2027-01-15")]
[InlineData(FrequencyType.Custom,       30,    "2026-01-15", "2026-01-15", "2026-02-14")]
public void NextOccurrence_HappyPath_ReturnsExpectedDate(
    FrequencyType type, int? intervalDays,
    string currentStr, string startStr, string expectedStr)
{
    var current  = DateTime.Parse(currentStr,  null, System.Globalization.DateTimeStyles.RoundtripKind);
    var start    = DateTime.Parse(startStr,    null, System.Globalization.DateTimeStyles.RoundtripKind);
    var expected = DateTime.Parse(expectedStr, null, System.Globalization.DateTimeStyles.RoundtripKind);

    var result = RecurrenceCalculator.NextOccurrence(type, intervalDays, current, start);

    result.Should().Be(expected);
}

// Edge case — snap-back: Jan 31 → Feb 28 → Mar 31
[Fact]
public void NextOccurrence_Monthly_SnapBackAfterFebruary_ReturnsMarch31()
{
    var start   = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
    var current = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc); // after Feb clamp
    var result  = RecurrenceCalculator.NextOccurrence(FrequencyType.Monthly, null, current, start);
    result.Should().Be(new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc));
}

// Edge case — year-end rollover: Dec 31 → Jan 31
[Fact]
public void NextOccurrence_Monthly_YearEndRollover_ReturnsJan31()
{
    var start   = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
    var current = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);
    var result  = RecurrenceCalculator.NextOccurrence(FrequencyType.Monthly, null, current, start);
    result.Should().Be(new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc));
}

// Edge case — Feb 29 (leap year)
[Fact]
public void NextOccurrence_Monthly_StartFeb29LeapYear_ClampsOnNonLeapMonths()
{
    var start   = new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc);
    var current = new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Utc);
    var result  = RecurrenceCalculator.NextOccurrence(FrequencyType.Monthly, null, current, start);
    result.Should().Be(new DateTime(2024, 3, 29, 0, 0, 0, DateTimeKind.Utc));
}
```

### Anti-Patterns to Avoid

- **Using `DateTime.AddMonths` without snap-back:** `AddMonths` clamps day-of-month when the result month is shorter. Calling it on the clamped date the next time drifts permanently. Always override the day from `startDate.Day`.
- **Anchoring to `currentDate.Day` instead of `startDate.Day`:** If `currentDate` has already been clamped (e.g., Feb 28 due to a Jan 31 start), anchoring to `currentDate.Day` gives 28 instead of 31 — the drift is re-introduced on the next step.
- **Using `new DateTime(y, m, d)` without preserving `DateTimeKind`:** Losing `DateTimeKind.Utc` on the returned value can cause subtle bugs downstream. Use the `new DateTime(y, m, d, h, min, s, kind)` overload.
- **Missing `default` case in switch:** Without it, adding a new `FrequencyType` value compiles silently and returns garbage. Throw `ArgumentOutOfRangeException`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Month arithmetic | Custom month-counter loop | BCL `DateTime.AddMonths` + day override | AddMonths handles leap year, year rollover, days-per-month correctly for the year/month result — we only need to override the day |
| Days-in-month lookup | Manual lookup table | `DateTime.DaysInMonth(year, month)` | BCL accounts for leap years correctly |

**Key insight:** The BCL does all the hard calendar work for year/month advancement. The only gap is day-of-month clamping on multi-call sequences — that one line (`Math.Min(targetDay, DaysInMonth)`) is all the custom logic needed.

---

## Common Pitfalls

### Pitfall 1: The Drift Bug (Most Critical)

**What goes wrong:** Jan 31 → Feb 28 (correct) → Mar 28 (wrong, should be Mar 31).

**Why it happens:** After the first call produces Feb 28, the next call passes Feb 28 as `currentDate`. `Feb 28.AddMonths(1)` = Mar 28 — the CLR has no memory of the original 31.

**How to avoid:** Always derive `targetDay = startDate.Day` — not `currentDate.Day` — before adding months. The calculator's contract (D-03) makes this explicit.

**Warning signs:** Tests that only check one hop from a month-end date pass; tests checking two hops fail on the second.

### Pitfall 2: DateTimeKind Loss

**What goes wrong:** Calculator returns a `DateTime` with `Kind = Unspecified` when the inputs were `Kind = Utc`.

**Why it happens:** `new DateTime(year, month, day)` uses the 3-argument constructor which defaults to `DateTimeKind.Unspecified`. The `AddMonths`/`AddDays` overloads on an existing `DateTime` preserve `Kind`, but constructing a new `DateTime` from components does not.

**How to avoid:** Use `new DateTime(y, m, d, h, min, s, existingDate.Kind)`. Alternatively, if the project always uses date-only values (time components = 0), use `new DateTime(y, m, d, 0, 0, 0, currentDate.Kind)`.

**Warning signs:** Integration tests that compare UTC dates start failing; `DateTimeOffset` comparisons throw.

### Pitfall 3: Custom with Null IntervalDays

**What goes wrong:** `Custom` frequency with a null `IntervalDays` — calling `AddDays(null.Value)` throws a `NullReferenceException` with no diagnostic context.

**Why it happens:** The `Custom` enum value is meaningless without `IntervalDays`.

**How to avoid:** Explicitly guard: `intervalDays ?? throw new ArgumentException("IntervalDays must be set for Custom frequency.", nameof(intervalDays))`. This gives a clear error message to the Phase 4 caller.

### Pitfall 4: The Feb 29 Leap Year Edge Case

**What goes wrong:** `new DateTime(2025, 2, 29, ...)` throws `ArgumentOutOfRangeException` at runtime.

**Why it happens:** Constructing a date that does not exist in the calendar.

**How to avoid:** The snap-back pattern handles this automatically — `Math.Min(29, DateTime.DaysInMonth(2025, 2))` = `Math.Min(29, 28)` = 28. No special case needed; the clamping covers it.

---

## Code Examples

### Complete RecurrenceCalculator Implementation

```csharp
// FinanceTracker.Domain/Services/RecurrenceCalculator.cs
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Domain.Services;

public static class RecurrenceCalculator
{
    public static DateTime NextOccurrence(
        FrequencyType type,
        int? intervalDays,
        DateTime currentDate,
        DateTime startDate)
    {
        int targetDay = startDate.Day;

        return type switch
        {
            FrequencyType.Daily        => currentDate.AddDays(1),
            FrequencyType.Weekly       => currentDate.AddDays(7),
            FrequencyType.BiWeekly     => currentDate.AddDays(14),
            FrequencyType.Monthly      => AddMonthsWithSnapBack(currentDate, targetDay, 1),
            FrequencyType.Quarterly    => AddMonthsWithSnapBack(currentDate, targetDay, 3),
            FrequencyType.SemiAnnually => AddMonthsWithSnapBack(currentDate, targetDay, 6),
            FrequencyType.Annually     => AddMonthsWithSnapBack(currentDate, targetDay, 12),
            FrequencyType.Custom       => currentDate.AddDays(
                                              intervalDays ?? throw new ArgumentException(
                                                  "IntervalDays must be set for Custom frequency.",
                                                  nameof(intervalDays))),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unhandled FrequencyType.")
        };
    }

    private static DateTime AddMonthsWithSnapBack(DateTime currentDate, int targetDay, int months)
    {
        // AddMonths correctly advances year/month (handles Dec → Jan rollover, leap year month count)
        // We only override the day component to prevent drift after short-month clamping
        DateTime shifted = currentDate.AddMonths(months);
        int clampedDay = Math.Min(targetDay, DateTime.DaysInMonth(shifted.Year, shifted.Month));
        return new DateTime(shifted.Year, shifted.Month, clampedDay,
                            currentDate.Hour, currentDate.Minute, currentDate.Second,
                            currentDate.Kind);
    }
}
```

### Key Snap-Back Behavior Verified

| StartDate | CurrentDate (input) | FrequencyType | Expected Output | AddMonths alone gives |
|-----------|---------------------|---------------|-----------------|-----------------------|
| Jan 31    | Jan 31              | Monthly       | Feb 28          | Feb 28 ✓              |
| Jan 31    | Feb 28              | Monthly       | Mar 31          | Mar 28 ✗ (drift!)     |
| Jan 31    | Mar 31              | Monthly       | Apr 30          | Apr 30 ✓              |
| Jan 31    | Apr 30              | Monthly       | May 31          | May 30 ✗ (drift!)     |
| Dec 31    | Dec 31              | Monthly       | Jan 31          | Jan 31 ✓              |
| Feb 29    | Feb 29 (2024)       | Monthly       | Mar 29          | Mar 29 ✓              |
| Jan 31    | Jan 31              | Quarterly     | Apr 30          | Apr 30 ✓              |
| Jan 31    | Apr 30              | Quarterly     | Jul 31          | Jul 30 ✗ (drift!)     |

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 + FluentAssertions 6.12.2 |
| Config file | None (implicit via `FinanceTracker.Tests.csproj`) |
| Quick run command | `dotnet test FinanceTracker.Tests --filter "FullyQualifiedName~RecurrenceCalculator"` |
| Full suite command | `dotnet test FinanceTracker.Tests` |

### Phase Requirements → Test Map

| Behavior | Test Type | Automated Command | File Exists? |
|----------|-----------|-------------------|--------------|
| Daily: AddDays(1) | unit | `dotnet test --filter "RecurrenceCalculator"` | ❌ Wave 0 |
| Weekly: AddDays(7) | unit | same | ❌ Wave 0 |
| BiWeekly: AddDays(14) | unit | same | ❌ Wave 0 |
| Monthly snap-back (happy path) | unit | same | ❌ Wave 0 |
| Quarterly snap-back (happy path) | unit | same | ❌ Wave 0 |
| SemiAnnually snap-back (happy path) | unit | same | ❌ Wave 0 |
| Annually snap-back (happy path) | unit | same | ❌ Wave 0 |
| Custom: AddDays(intervalDays) | unit | same | ❌ Wave 0 |
| Snap-back: Feb 28 → Mar 31 (Jan 31 start) | unit | same | ❌ Wave 0 |
| Year-end rollover: Dec 31 → Jan 31 | unit | same | ❌ Wave 0 |
| Feb 29 leap year clamping | unit | same | ❌ Wave 0 |
| Custom with null IntervalDays throws | unit | same | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `dotnet test FinanceTracker.Tests --filter "FullyQualifiedName~RecurrenceCalculator"`
- **Per wave merge:** `dotnet test FinanceTracker.Tests`
- **Phase gate:** Full suite green before `/gsd-verify-work`

### Wave 0 Gaps

- [ ] `FinanceTracker.Tests/Domain/RecurrenceCalculatorTests.cs` — new file covering all 12 test cases above
- [ ] No framework changes needed — xUnit and FluentAssertions already in `.csproj`

---

## Environment Availability

Step 2.6: SKIPPED — this phase is purely code/config changes. No external tools, services, databases, or CLI utilities beyond the project's own .NET 8 SDK are required. No new NuGet packages are added.

---

## Open Questions

1. **`DateTimeKind` assumption for `RecurringTransaction.StartDate` / `NextOccurrenceDate`**
   - What we know: Existing tests construct dates with `DateTimeKind.Utc` (e.g., `new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)`)
   - What's unclear: Whether the calculator should assert `Kind.Utc` on inputs, strip time entirely (date-only), or preserve whatever `Kind` was passed
   - Recommendation: Preserve `currentDate.Kind` in the returned value (as shown in the implementation above); add no input validation on `Kind` since this is pure math — the caller owns the contract.

2. **Time-of-day components in `currentDate`**
   - What we know: All current tests use midnight UTC. The Phase 4 background service will likely work with `DateTime.UtcNow.Date`.
   - What's unclear: Should `NextOccurrence` strip time (return date-only midnight) or preserve hours/minutes from `currentDate`?
   - Recommendation: Preserve time components from `currentDate` (no stripping), using `new DateTime(y, m, d, currentDate.Hour, currentDate.Minute, currentDate.Second, currentDate.Kind)`. This is the least-surprising contract. Phase 4 can normalize to midnight before calling if desired.

---

## Sources

### Primary (HIGH confidence)

- .NET 8 BCL — `System.DateTime.AddMonths`, `DateTime.DaysInMonth`, `DateTimeKind` — verified against current .NET documentation (behavior unchanged since .NET 1.x; highly stable)
- `FinanceTracker.Domain/Entities/Frequency.cs` — verified all 8 `FrequencyType` enum values
- `FinanceTracker.Domain/Entities/RecurringTransaction.cs` — verified `StartDate`, `NextOccurrenceDate` field types
- `FinanceTracker.Tests/FinanceTracker.Tests.csproj` — verified xUnit 2.9.2 + FluentAssertions 6.12.2 already present

### Secondary (MEDIUM confidence)

- `FinanceTracker.Tests/Domain/RecurringTransactionDomainModelTests.cs` — established test structure and assertion style for this project

---

## Metadata

**Confidence breakdown:**

- Standard stack: HIGH — no new dependencies; BCL DateTime is highly stable
- Architecture: HIGH — static class pattern is trivial; snap-back formula is well-understood
- Pitfalls: HIGH — drift bug is a known BCL gotcha with documented workaround
- Test design: HIGH — xUnit `[Theory]` + `[InlineData]` for enum coverage is idiomatic C#

**Research date:** 2026-04-26
**Valid until:** Stable indefinitely (pure BCL math, no third-party libraries)
