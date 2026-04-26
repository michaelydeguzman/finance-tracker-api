---
phase: 02-redesign-recurring-transaction-domain-model-with-template-and-instance-separation
plan: 01
subsystem: domain
tags: [domain-model, recurring-transactions, entity, refactor]
dependency_graph:
  requires: []
  provides:
    - RecurringTransaction entity with RecurringTransactionStatus enum
    - Transaction.RecurringTransactionId nullable FK
    - Frequency.RecurringTransactions nav collection
    - Category.RecurringTransactions nav collection
    - Clean-compiling solution with 0 errors
  affects:
    - FinanceTracker.Domain
    - FinanceTracker.Application
    - FinanceTracker.Infrastructure
    - FinanceTracker.Tests
tech_stack:
  added: []
  patterns:
    - Enum-in-same-file pattern (RecurringTransactionStatus with RecurringTransaction class)
    - required keyword for non-nullable navigation properties
    - Nullable FK pattern (Guid? RecurringTransactionId) for optional relationship
key_files:
  created:
    - FinanceTracker.Domain/Entities/RecurringTransaction.cs
  modified:
    - FinanceTracker.Domain/Entities/Transaction.cs
    - FinanceTracker.Domain/Entities/Frequency.cs
    - FinanceTracker.Domain/Entities/Category.cs
    - FinanceTracker.Application/Dtos/CreateTransactionDto.cs
    - FinanceTracker.Application/Dtos/UpdateTransactionDto.cs
    - FinanceTracker.Application/Dtos/Responses/TransactionResponseDto.cs
    - FinanceTracker.Application/Features/Transactions/Commands/CreateTransaction/CreateTransactionCommandHandler.cs
    - FinanceTracker.Application/Services/TransactionService.cs
    - FinanceTracker.Infrastructure/Persistence/TransactionRepository.cs
    - FinanceTracker.Infrastructure/Persistence/Configurations/TransactionConfiguration.cs
    - FinanceTracker.Tests/Unit/Handlers/CreateTransactionCommandHandlerTests.cs
    - FinanceTracker.Tests/Unit/Handlers/UpdateTransactionCommandHandlerTests.cs
    - FinanceTracker.Tests/Integration/TransactionsApiIntegrationTests.cs
decisions:
  - RecurringTransactionStatus has Active/Paused/Cancelled (no Completed) per D-05; background service advances NextOccurrenceDate rather than marking complete
  - Transaction.RecurringTransaction uses nullable navigation (no `required`) per standard nullable-nav pattern; RecurringTransaction.Category/Frequency use `required` as they are non-nullable
  - Frequency.ICollection<Transaction> replaced with ICollection<RecurringTransaction> to match Plan 02 EF configuration expectations and prevent spurious shadow FK
metrics:
  duration_minutes: ~8
  completed_date: "2026-04-26"
  tasks_completed: 3
  files_modified: 13
---

# Phase 02 Plan 01: Domain Model Introduction and FrequencyId Scrub Summary

**One-liner:** RecurringTransaction entity with enum introduced; Transaction/Frequency/Category restructured; all FrequencyId references scrubbed from app/infra/test layers for clean 0-error build.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | Create RecurringTransaction.cs with RecurringTransactionStatus enum | 6438c72 |
| 2 | Restructure Transaction/Frequency/Category entity nav properties | e40f91c |
| 3 | Remove all FrequencyId references from app, infra, and test layers | f499c7c |

## What Was Built

- **RecurringTransaction entity** with all 13 D-03 fields: Id, Name, Description, DefaultAmount, CategoryId, Category, FrequencyId, Frequency, StartDate, EndDate, NextOccurrenceDate, Status, CreatedAt, CreatedBy
- **RecurringTransactionStatus enum**: Active, Paused, Cancelled (no Completed per D-05)
- **Transaction.cs restructured**: FrequencyId/Frequency removed; nullable RecurringTransactionId/RecurringTransaction added
- **Frequency.cs restructured**: ICollection<Transaction> Transactions replaced with ICollection<RecurringTransaction> RecurringTransactions
- **Category.cs extended**: ICollection<RecurringTransaction> RecurringTransactions added (existing Transactions kept)
- **Application/Infrastructure/Test layers**: all FrequencyId property references, Include(x => x.Frequency) calls, and EF HasOne(Frequency) configuration removed

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] TransactionConfiguration.cs not in plan's file list but had stale Frequency references**
- **Found during:** Task 3 dotnet build — 2 CS1061 errors
- **Issue:** `HasOne(e => e.Frequency).WithMany(f => f.Transactions).HasForeignKey(e => e.FrequencyId)` and `HasIndex(e => e.FrequencyId)` in EF configuration
- **Fix:** Removed the HasOne(Frequency) relationship block and FrequencyId index
- **Files modified:** `FinanceTracker.Infrastructure/Persistence/Configurations/TransactionConfiguration.cs`
- **Commit:** f499c7c

**2. [Rule 3 - Blocking] UpdateTransactionCommandHandlerTests.cs not in plan's file list but had stale FrequencyId references**
- **Found during:** Task 3 dotnet build (second pass) — 3 CS0117 errors
- **Issue:** `FrequencyId = null` in UpdateTransactionDto initializer and two Transaction initializers in the test
- **Fix:** Removed all three FrequencyId assignments
- **Files modified:** `FinanceTracker.Tests/Unit/Handlers/UpdateTransactionCommandHandlerTests.cs`
- **Commit:** f499c7c

## Known Stubs

None — no UI rendering stubs or placeholder data introduced in this plan.

## Verification

- `dotnet build FinanceTracker/FinanceTracker.API.sln` → exit code 0, 0 errors, 0 warnings ✓
- RecurringTransaction.cs exists with all 13 fields and RecurringTransactionStatus enum (Active/Paused/Cancelled) ✓
- Transaction.cs has RecurringTransactionId/RecurringTransaction, no FrequencyId/Frequency ✓
- Frequency.cs has RecurringTransactions nav, not Transactions nav ✓
- Category.cs has both Transactions and RecurringTransactions nav collections ✓

## Self-Check: PASSED
