# Phase 1: Clean up recurring transaction dead code and reconcile domain projects - Context

**Gathered:** 2026-04-25
**Status:** Ready for planning

<domain>
## Phase Boundary

Delete the dead `FinanceTracker.Domain/` project and all its unreferenced draft files, then rename the live `Finance.Tracker.Domain/` folder to `FinanceTracker.Domain/` to match the naming convention of every other project in the solution. Update all references (.sln, .csproj files) accordingly and verify the solution builds clean.

This phase does NOT change any domain entity logic, add new files, or touch Application/Infrastructure/API layers — it is purely structural cleanup.

</domain>

<decisions>
## Implementation Decisions

### Dead Project Deletion
- **D-01:** Delete the entire `FinanceTracker.Domain/` folder and all 4 files within it:
  - `FinanceTracker.Domain/Categories/Category.cs` (old draft, private setters, `Recurrence` nav)
  - `FinanceTracker.Domain/Categories/Recurrence.cs` (stub class + record, never wired up)
  - `FinanceTracker.Domain/Entities/Category.cs` (old draft with broken constructor, `Recurring RecurringProps`, `IsIncome`)
  - `FinanceTracker.Domain/Entities/FrequencyType.cs` (duplicate enum)
- **D-02:** This project is NOT referenced in the solution or by any other `.csproj` — deletion is safe with no downstream breakage.

### Live Project Rename
- **D-03:** Rename the live domain folder from `Finance.Tracker.Domain/` to `FinanceTracker.Domain/`. The folder currently holds the live `FinanceTracker.Domain.csproj` and three active entity files.
- **D-04:** After renaming, update the following references to point to the new path:
  - `FinanceTracker/FinanceTracker.API.sln` — project entry for `FinanceTracker.Domain`
  - `FinanceTracker.Application/FinanceTracker.Application.csproj` — `<ProjectReference>`
  - `FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj` — `<ProjectReference>`
- **D-05:** The three entity files and their namespaces (`FinanceTracker.Domain.Entities`) do NOT change — only the physical folder path changes.

### Build Verification
- **D-06:** After all changes, `dotnet build` must pass with 0 errors and 0 warnings as an explicit acceptance criterion for this phase.

### Claude's Discretion
- Order of operations (delete dead project first, then rename live project, then update references) — Claude decides the safest sequence.
- Whether to rename the folder in-place via git mv or by creating new folder + copying files — Claude decides based on git history preservation best practice.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Solution Structure
- `FinanceTracker/FinanceTracker.API.sln` — Solution file; contains all project registrations and paths that need updating
- `FinanceTracker.Application/FinanceTracker.Application.csproj` — References live domain project; path must be updated
- `FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj` — References live domain project; path must be updated
- `Finance.Tracker.Domain/FinanceTracker.Domain.csproj` — The live domain project being renamed

### Dead Files to Delete
- `FinanceTracker.Domain/Categories/Category.cs`
- `FinanceTracker.Domain/Categories/Recurrence.cs`
- `FinanceTracker.Domain/Entities/Category.cs`
- `FinanceTracker.Domain/Entities/FrequencyType.cs`
- `FinanceTracker.Domain/FinanceTracker.Domain.csproj`

### Live Files (do not modify content)
- `Finance.Tracker.Domain/Entities/Category.cs` — Live Category entity; namespace stays `FinanceTracker.Domain.Entities`
- `Finance.Tracker.Domain/Entities/Transaction.cs` — Live Transaction entity
- `Finance.Tracker.Domain/Entities/Frequency.cs` — Live Frequency entity + FrequencyType enum

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- None — this is a structural cleanup phase, no code assets are being built or reused.

### Established Patterns
- All other projects follow `FinanceTracker.*` naming (Application, Infrastructure, Tests, API) — the rename brings Domain into alignment.
- Namespaces inside the domain project (`FinanceTracker.Domain.Entities`) already use the correct convention — only the physical folder path is mismatched.

### Integration Points
- All usages of `FinanceTracker.Domain.Entities` types (Category, Transaction, Frequency, CategoryType, FrequencyType) are in Application and Infrastructure layers — they reference the namespace, not the folder path, so they require no changes after the rename.

</code_context>

<specifics>
## Specific Ideas

- No specific references or examples — this is mechanical cleanup work with clear before/after states.

</specifics>

<deferred>
## Deferred Ideas

- None — discussion stayed within phase scope.

</deferred>

---

*Phase: 01-clean-up-recurring-transaction-dead-code-and-reconcile-domain-projects*
*Context gathered: 2026-04-25*
