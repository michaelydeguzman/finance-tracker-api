# Phase 2: Redesign recurring transaction domain model with template and instance separation - Context

**Gathered:** 2026-04-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Introduce a `RecurringTransaction` entity as the template (master definition) for a recurring transaction, and wire individual `Transaction` instances back to it via a nullable FK. This phase establishes the foundational domain model and EF Core configuration that Phases 3 (frequency interval logic), 4 (background generation service), and 5 (pause/cancel/skip) all build on.

This phase does NOT implement the generation service, frequency interval calculations, or lifecycle actions — those belong in Phases 3–5.

</domain>

<decisions>
## Implementation Decisions

### Persistence Architecture
- **D-01:** Use two separate tables — `RecurringTransactions` (template) and the existing `Transactions` (instances). No discriminator columns, no TPH.
- **D-02:** `Transaction` gets a nullable `RecurringTransactionId` FK pointing back to its template. A transaction with `RecurringTransactionId = null` is a standalone one-off transaction.

### RecurringTransaction Template Fields
- **D-03:** Template entity fields:
  - `Id` (Guid, PK)
  - `Name` (required, MaxLength 250)
  - `Description` (optional, MaxLength 500)
  - `DefaultAmount` (decimal — default amount for generated instances)
  - `CategoryId` (Guid, FK → Category)
  - `FrequencyId` (Guid, FK → Frequency)
  - `StartDate` (DateTime — first occurrence anchor date)
  - `EndDate` (DateTime?, nullable — generation stops after this date)
  - `NextOccurrenceDate` (DateTime — background service reads and advances this after each generation)
  - `Status` (enum: Active / Paused / Cancelled)
  - `CreatedAt` (DateTime)
  - `CreatedBy` (string)
- **D-04:** `DefaultAmount` is the template-level default. Individual `Transaction` instances may have a different `Amount` (overridden when the generated transaction is edited). No separate `Amount` override column on `Transaction` — the existing `Transaction.Amount` field serves as the per-instance value.

### Template Status
- **D-05:** `RecurringTransactionStatus` enum has three values: `Active`, `Paused`, `Cancelled`. No `Completed` state — templates that reach their `EndDate` naturally are handled by the generation service (Phase 4) stopping, not by a status transition.

### NextOccurrenceDate
- **D-06:** `NextOccurrenceDate` is stored on the template. The background service (Phase 4) reads it to know what to generate next and advances it after each generation. Phase 3 provides the calendar logic for computing the next date.

### Frequency Entity
- **D-07:** `Frequency` lookup table is retained as-is. `RecurringTransaction.FrequencyId` is a required FK to `Frequency`. No changes to the `Frequency` entity or its data in this phase.
- **D-08:** `Transaction.FrequencyId` is removed. Frequency information belongs on the template, not individual instances.

### Existing Data Migration
- **D-09:** Existing `Transaction` rows that have a `FrequencyId` set are converted to standalone one-off transactions: `FrequencyId` is nulled out in the migration data step, then the `FrequencyId` column and its FK/index are dropped from the `Transactions` table.
- **D-10:** No `RecurringTransaction` templates are auto-created from existing data. Existing transactions lose their recurring marker and become regular one-off entries.

### Claude's Discretion
- EF Core configuration details (index strategy, cascade delete behavior on `RecurringTransactionId` FK)
- Exact migration sequencing (null-out data → drop FK → add new table → add FK on Transaction)
- Navigation property naming conventions

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Current Domain Entities (read before modifying)
- `FinanceTracker.Domain/Entities/Transaction.cs` — current Transaction entity; `FrequencyId` FK to be removed, `RecurringTransactionId` FK to be added
- `FinanceTracker.Domain/Entities/Frequency.cs` — lookup table retained as-is; `FrequencyType` enum and `IntervalDays` used by Phase 3
- `FinanceTracker.Domain/Entities/Category.cs` — referenced by template's `CategoryId` FK

### Infrastructure (EF Core)
- `FinanceTracker.Infrastructure/Persistence/FinanceTrackerContext.cs` — add `DbSet<RecurringTransaction>`
- `FinanceTracker.Infrastructure/Persistence/Configurations/TransactionConfiguration.cs` — remove `FrequencyId` FK config, add `RecurringTransactionId` FK config
- `FinanceTracker.Infrastructure/Migrations/` — existing migrations for reference; new migration required for this phase

### Solution Structure
- `FinanceTracker/FinanceTracker.API.sln` — solution root
- `FinanceTracker.Application/FinanceTracker.Application.csproj` — Application layer project reference

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Frequency` entity and `FrequencyType` enum — retained and referenced by template FK; no changes needed
- `Category` entity — referenced by template FK; no changes needed
- EF Core configuration pattern (`IEntityTypeConfiguration<T>`) — already established in `Configurations/`; new `RecurringTransactionConfiguration.cs` follows the same pattern

### Established Patterns
- Entity configurations live in `FinanceTracker.Infrastructure/Persistence/Configurations/` as separate `IEntityTypeConfiguration<T>` files — one per entity
- All entities use `Guid` PKs with `[Key]` attribute and data annotation constraints
- `CreatedAt` + `CreatedBy` audit fields on every entity
- Navigation properties with explicit FK properties (e.g. `CategoryId` + `Category`)
- `modelBuilder.ApplyConfigurationsFromAssembly()` auto-discovers configurations — new `RecurringTransactionConfiguration` will be picked up automatically

### Integration Points
- `FinanceTrackerContext` — add `DbSet<RecurringTransaction> RecurringTransactions`
- `Transaction` entity — remove `FrequencyId`/`Frequency` members, add `RecurringTransactionId`/`RecurringTransaction` members
- Application layer queries — `GetTransactionsListQueryHandler` and related handlers will need to stop joining/including `Frequency` (no longer on Transaction)

</code_context>

<specifics>
## Specific Ideas

- No specific references or examples provided — design is based on discussion decisions above.

</specifics>

<deferred>
## Deferred Ideas

- None — discussion stayed within phase scope. Generation service logic deferred to Phase 4. Pause/cancel/skip lifecycle actions deferred to Phase 5. Calendar-based frequency interval calculations deferred to Phase 3.

</deferred>

---

*Phase: 02-redesign-recurring-transaction-domain-model-with-template-and-instance-separation*
*Context gathered: 2026-04-26*
