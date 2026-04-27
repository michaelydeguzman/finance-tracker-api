---
phase: 03
slug: fix-calendar-based-frequency-interval-logic-for-monthly-quarterly-and-annual-recurrences
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-26
---

# Phase 03 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 + FluentAssertions 6.12.2 |
| **Config file** | None (implicit via `FinanceTracker.Tests.csproj`) |
| **Quick run command** | `dotnet test FinanceTracker.Tests --filter "FullyQualifiedName~RecurrenceCalculator"` |
| **Full suite command** | `dotnet test FinanceTracker.Tests` |
| **Estimated runtime** | ~5 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test FinanceTracker.Tests --filter "FullyQualifiedName~RecurrenceCalculator"`
- **After every plan wave:** Run `dotnet test FinanceTracker.Tests`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** ~5 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|----------|-----------|-------------------|-------------|--------|
| 03-01-01 | 01 | 0 | Create RecurrenceCalculatorTests.cs stub | unit | `dotnet test --filter "RecurrenceCalculator"` | ❌ W0 | ⬜ pending |
| 03-01-02 | 01 | 1 | RecurrenceCalculator static class + all FrequencyTypes | unit | `dotnet test --filter "RecurrenceCalculator"` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `FinanceTracker.Tests/Domain/RecurrenceCalculatorTests.cs` — stub file with 12 test cases (8 happy path via `[Theory]` + 4 edge cases as `[Fact]`); tests will fail until Wave 1 implements the calculator

*All 12 tests must exist as stubs before any implementation begins (Nyquist requirement — test-first feedback loop).*

---

## Manual-Only Verifications

*All phase behaviors have automated verification.*

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references (`RecurrenceCalculatorTests.cs`)
- [ ] No watch-mode flags
- [ ] Feedback latency < 5s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
