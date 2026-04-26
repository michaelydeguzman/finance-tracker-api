# Phase 1: Clean up recurring transaction dead code and reconcile domain projects - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-25
**Phase:** 01-clean-up-recurring-transaction-dead-code-and-reconcile-domain-projects
**Areas discussed:** Cleanup scope

---

## Cleanup scope

| Option | Description | Selected |
|--------|-------------|----------|
| Delete only the dead project | Remove `FinanceTracker.Domain/` and its 4 dead files; leave live project folder name as-is | |
| Rename folder + delete dead project | Rename `Finance.Tracker.Domain/` to `FinanceTracker.Domain/`, update all .sln and .csproj references, delete dead project | ✓ |

**User's choice:** Rename the live folder to `FinanceTracker.Domain/` — fix the naming inconsistency as part of this cleanup.

---

## Build Verification

| Option | Description | Selected |
|--------|-------------|----------|
| Include build verification | `dotnet build` must pass with 0 errors/warnings as acceptance criterion | ✓ |
| Skip build check | Verify manually after execution | |

**User's choice:** Include build verification as an explicit acceptance criterion.

---

## Claude's Discretion

- Order of operations for deletion and rename (Claude decides safest sequence)
- Whether to use `git mv` or folder copy for renaming (Claude decides for git history preservation)
