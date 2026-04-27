---
phase: 4
slug: add-background-service-to-generate-transaction-instances-from-recurring-templates
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-27
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit + FluentAssertions (existing) |
| **Config file** | `FinanceTracker.Tests/FinanceTracker.Tests.csproj` |
| **Quick run command** | `dotnet test FinanceTracker.Tests --filter "Category=Phase4"` |
| **Full suite command** | `dotnet test` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet test`
- **After every plan wave:** Run `dotnet test`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 4-01-01 | 01 | 1 | D-01/D-02 | build | `dotnet build` | ❌ W0 | ⬜ pending |
| 4-01-02 | 01 | 1 | D-05/D-06 | unit | `dotnet test` | ❌ W0 | ⬜ pending |
| 4-01-03 | 01 | 2 | D-04/D-14 | unit | `dotnet test` | ❌ W0 | ⬜ pending |
| 4-01-04 | 01 | 2 | D-07..D-12 | unit | `dotnet test` | ❌ W0 | ⬜ pending |
| 4-01-05 | 01 | 2 | D-13 | unit | `dotnet test` | ❌ W0 | ⬜ pending |
| 4-01-06 | 01 | 3 | D-15 | unit | `dotnet test` | ❌ W0 | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `FinanceTracker.Tests/Worker/TransactionGenerationServiceTests.cs` — stubs for generation logic tests
- [ ] Existing `FinanceTracker.Tests` infrastructure covers xUnit + FluentAssertions — no new framework install needed

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Task Scheduler trigger runs console app end-to-end | D-02 | Requires OS-level Task Scheduler config | Register exe in Task Scheduler, trigger manually, verify Transactions rows created in DB |
| Console app exits cleanly after run | D-02 | Process exit code check | Run `dotnet run --project FinanceTracker.Worker`, verify exit code 0 |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
