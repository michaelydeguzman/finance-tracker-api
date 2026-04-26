---
phase: 1
slug: clean-up-recurring-transaction-dead-code-and-reconcile-domain-projects
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-25
---

# Phase 1 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit 2.9.2 (existing) |
| **Config file** | `FinanceTracker.Tests/FinanceTracker.Tests.csproj` |
| **Quick run command** | `dotnet build FinanceTracker/FinanceTracker.API.sln` |
| **Full suite command** | `dotnet test FinanceTracker/FinanceTracker.API.sln` |
| **Estimated runtime** | ~10 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build FinanceTracker/FinanceTracker.API.sln`
- **After every plan wave:** Run `dotnet test FinanceTracker/FinanceTracker.API.sln`
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** ~10 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | Status |
|---------|------|------|-------------|-----------|-------------------|--------|
| 1-01-01 | 01 | 1 | D-01/D-02 | structural | `Test-Path FinanceTracker.Domain/ \| Should be False` | ⬜ pending |
| 1-01-02 | 01 | 1 | D-03/D-04 | structural | `Test-Path FinanceTracker.Domain/FinanceTracker.Domain.csproj \| Should be True` | ⬜ pending |
| 1-01-03 | 01 | 1 | D-06 | build | `dotnet build FinanceTracker/FinanceTracker.API.sln` | ⬜ pending |
| 1-01-04 | 01 | 1 | D-06 | test suite | `dotnet test FinanceTracker/FinanceTracker.API.sln` | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

Existing infrastructure covers all phase requirements. No new test files needed — `dotnet build` and `dotnet test` are the primary gates.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Dead project files no longer exist | D-01/D-02 | Structural filesystem check | `Test-Path FinanceTracker.Domain/` returns False |
| Live project at new path | D-03 | Structural filesystem check | `Test-Path FinanceTracker.Domain/FinanceTracker.Domain.csproj` returns True |
| No stale bin artifact at old path | Research finding | Git tracking check | `git ls-files Finance.Tracker.Domain/` returns empty |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 10s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
