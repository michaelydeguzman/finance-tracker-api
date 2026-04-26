---
phase: 02
slug: redesign-recurring-transaction-domain-model-with-template-and-instance-separation
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-04-26
---

# Phase 02 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xUnit 2.9.2 |
| **Config file** | None (standard xUnit discovery) |
| **Quick run command** | `dotnet test FinanceTracker/FinanceTracker.API.sln --filter "Category=Unit"` |
| **Full suite command** | `dotnet test FinanceTracker/FinanceTracker.API.sln` |
| **Estimated runtime** | ~15 seconds |

---

## Sampling Rate

- **After every task commit:** Run `dotnet build FinanceTracker/FinanceTracker.API.sln` (build must stay green)
- **After every plan wave:** Run `dotnet test FinanceTracker/FinanceTracker.API.sln` (full 21+ test suite)
- **Before `/gsd-verify-work`:** Full suite must be green with 0 failed tests
- **Max feedback latency:** ~15 seconds

---

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|-----------|-------------------|-------------|--------|
| 02-01-01 | 01 | 1 | D-03 (entity) | build | `dotnet build FinanceTracker/FinanceTracker.API.sln` | ❌ W0 | ⬜ pending |
| 02-01-02 | 01 | 1 | D-01/D-08 (Transaction changes) | build | `dotnet build FinanceTracker/FinanceTracker.API.sln` | ✅ | ⬜ pending |
| 02-01-03 | 01 | 1 | D-07 (Frequency nav update) | build | `dotnet build FinanceTracker/FinanceTracker.API.sln` | ✅ | ⬜ pending |
| 02-02-01 | 02 | 1 | D-01/D-02 (EF config) | build | `dotnet build FinanceTracker/FinanceTracker.API.sln` | ❌ W0 | ⬜ pending |
| 02-02-02 | 02 | 1 | D-09 (migration + data step) | integration | `dotnet test FinanceTracker/FinanceTracker.API.sln` | ❌ W0 | ⬜ pending |
| 02-03-01 | 03 | 2 | D-08 (app layer cleanup) | unit | `dotnet test --filter "Category=Unit"` | ✅ | ⬜ pending |
| 02-03-02 | 03 | 2 | D-09 (test updates) | unit+integration | `dotnet test FinanceTracker/FinanceTracker.API.sln` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `FinanceTracker.Tests/Domain/RecurringTransactionDomainModelTests.cs` — entity creation, status enum values, EF Core round-trip for new `RecurringTransactions` table

*Note: All other test infrastructure exists. Only the new entity test file needs Wave 0 creation.*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| EF migration Up/Down idempotent against real SQL Server | D-09 | Integration tests use InMemory; only real SQL Server runs actual migration | Run `dotnet ef database update` on a dev SQL Server instance; verify schema with SSMS or `dotnet ef dbcontext info` |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 15s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
