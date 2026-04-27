# Phase 4: Add background service to generate transaction instances from recurring templates - Context

**Gathered:** 2026-04-27
**Status:** Ready for planning

<domain>
## Phase Boundary

Implement a standalone console application (`FinanceTracker.Worker`) that is triggered externally by Windows Task Scheduler to query all `Active` `RecurringTransaction` templates with `NextOccurrenceDate <= now`, generate `Transaction` instances from them (including catch-up for missed occurrences), and advance `NextOccurrenceDate` on each template using `RecurrenceCalculator`.

This phase does NOT implement the API endpoints for managing recurring transactions, pause/cancel/skip lifecycle actions (Phase 5), or user authentication wiring. It does not run as an in-process `BackgroundService` — scheduling is handled externally by Task Scheduler.

</domain>

<decisions>
## Implementation Decisions

### Execution Model
- **D-01:** Implemented as a standalone console application project (`FinanceTracker.Worker`) — not an in-process `BackgroundService` inside the API process. The API does not need to be running for generation to occur.
- **D-02:** Scheduling is managed externally by Windows Task Scheduler. The console app runs, does its work, and exits. No polling loop inside the process.
- **D-03:** Run interval defaults to every 24 hours but is configurable. Task Scheduler controls the actual trigger; a default suggestion of daily is documented but not enforced in code.

### Catch-up Behavior
- **D-04:** Full catch-up on every run — if `NextOccurrenceDate` is in the past (e.g., service was not triggered for several days), the worker loops forward from `NextOccurrenceDate`, generating a `Transaction` for each missed occurrence and advancing `NextOccurrenceDate` each iteration, until `NextOccurrenceDate > now`.

### Template Selection
- **D-05:** Only `Active` templates are processed. `Paused` and `Cancelled` templates are skipped entirely (carried from Phase 2, D-05).
- **D-06:** Templates are filtered by `Status == Active` AND `NextOccurrenceDate <= DateTime.UtcNow`. Templates with a future `NextOccurrenceDate` are not touched.

### Generated Transaction Field Mapping
- **D-07:** `TransactionDate` = template's `NextOccurrenceDate` at the time of generation (the scheduled date, not the actual wall-clock run time).
- **D-08:** `Name` = copied from `template.Name`.
- **D-09:** `Amount` = `template.DefaultAmount`.
- **D-10:** `CategoryId` = `template.CategoryId`.
- **D-11:** `RecurringTransactionId` = `template.Id` (links instance back to template).
- **D-12:** `CreatedBy` = copied from `template.CreatedBy` for now; will be replaced with proper user identity once auth is implemented in the API milestone.

### EndDate Handling
- **D-13:** `EndDate` is a generation boundary only. When `NextOccurrenceDate > EndDate`, the worker stops generating for that template and does not advance `NextOccurrenceDate` further. Template `Status` is left unchanged (`Active`) — no auto-cancel side effect.

### NextOccurrenceDate Advancement
- **D-14:** After generating each `Transaction` instance, `NextOccurrenceDate` is advanced using `RecurrenceCalculator.NextOccurrence(template.Frequency.Type, template.Frequency.IntervalDays, currentNextOccurrenceDate, template.StartDate)`. The catch-up loop repeats until `NextOccurrenceDate > now` (or `> EndDate`).

### Error Handling
- **D-15:** Per-template error isolation — if a single template throws during generation (e.g., DB write failure), the exception is caught and logged, and the worker continues processing the remaining templates. One broken template does not abort the batch.

### Claude's Discretion
- DI setup inside the console app (IServiceCollection, IConfiguration, appsettings.json)
- Whether to introduce `IRecurringTransactionRepository` or use `FinanceTrackerContext` directly in a scoped service
- Transaction-per-template vs. transaction-per-batch SaveChanges strategy
- Logging implementation (ILogger via Microsoft.Extensions.Logging or simple Console.WriteLine)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Domain Entities
- `FinanceTracker.Domain/Entities/RecurringTransaction.cs` — template entity: `Status`, `NextOccurrenceDate`, `EndDate`, `StartDate`, `FrequencyId`, `DefaultAmount`, `CreatedBy`
- `FinanceTracker.Domain/Entities/Transaction.cs` — instance entity to be created by the worker; all fields the worker must populate
- `FinanceTracker.Domain/Entities/Frequency.cs` — `FrequencyType` enum and `IntervalDays` for `Custom` type; worker must eager-load `Frequency` from template

### Domain Services
- `FinanceTracker.Domain/Services/RecurrenceCalculator.cs` — `NextOccurrence(type, intervalDays, currentDate, startDate)` used to advance `NextOccurrenceDate` after each generation

### Infrastructure
- `FinanceTracker.Infrastructure/Persistence/FinanceTrackerContext.cs` — `DbContext`; new worker project sets up its own DI with the same context
- `FinanceTracker.Infrastructure/Persistence/TransactionRepository.cs` — existing repository pattern for reference; worker may follow or use context directly

### API DI Reference
- `FinanceTracker/Program.cs` — existing DI registration patterns; worker's `Program.cs` will mirror the DbContext and repository registrations

### Prior Phase Context
- `.planning/phases/02-redesign-recurring-transaction-domain-model-with-template-and-instance-separation/02-CONTEXT.md` — D-05 (Status enum), D-06 (NextOccurrenceDate ownership), D-03 (template fields)
- `.planning/phases/03-fix-calendar-based-frequency-interval-logic-for-monthly-quarterly-and-annual-recurrences/03-CONTEXT.md` — D-04/D-05 (RecurrenceCalculator architecture and method signature)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `RecurrenceCalculator.NextOccurrence(...)` — drop-in call for advancing `NextOccurrenceDate`; pure static, no DI needed
- `FinanceTrackerContext` — shared DbContext via `Microsoft.EntityFrameworkCore.SqlServer`; worker references `FinanceTracker.Infrastructure` directly
- `IEntityTypeConfiguration<T>` configurations in `FinanceTracker.Infrastructure/Persistence/Configurations/` — auto-discovered; no changes needed
- `ITransactionRepository` / `TransactionRepository` — existing `AddAsync` can be reused if worker goes through the repository layer

### Established Patterns
- DI via `IServiceCollection` + `Microsoft.Extensions.Hosting` — API's `Program.cs` is the model; worker follows same pattern
- `Guid` PKs, `CreatedAt = DateTime.UtcNow` on entity creation — follow existing entity initialization convention
- Repository pattern (`IXRepository` interface + concrete class in Infrastructure) — if a new `IRecurringTransactionRepository` is introduced, follow this pattern
- `appsettings.json` for configuration — connection string already in this file; worker reads from same source or its own config file

### Integration Points
- New project `FinanceTracker.Worker` references `FinanceTracker.Infrastructure` and `FinanceTracker.Domain`
- Solution file (`FinanceTracker/FinanceTracker.API.sln`) must be updated to include the new project
- Connection string from `appsettings.json` (same DB as the API)

</code_context>

<specifics>
## Specific Ideas

- Cloud migration path: if the app moves to cloud hosting, `FinanceTracker.Worker` can be adapted into a .NET Worker Service (Windows Service) or a cloud job (Azure Function timer trigger, AWS Lambda scheduled) without changing the core generation logic
- Task Scheduler setup: `dotnet publish -r win-x64 --self-contained` → register exe with Task Scheduler at desired interval (daily recommended default)

</specifics>

<deferred>
## Deferred Ideas

- **Cloud hosting migration** — If the app is ever hosted for multiple users in the cloud, the console/Task Scheduler approach should be replaced with a persistent Worker Service or cloud-native scheduler (Azure Functions timer, AWS EventBridge). The generation logic in this phase is designed to be portable.
- **Per-user scheduling** — Future possibility: different users on different schedules; not relevant until multi-user auth is complete.
- **`LastError` field on template** — Flagging templates that consistently fail (option 3 from error handling discussion) deferred; simple log-and-skip is sufficient for now.

</deferred>

---

*Phase: 04-add-background-service-to-generate-transaction-instances-from-recurring-templates*
*Context gathered: 2026-04-27*
