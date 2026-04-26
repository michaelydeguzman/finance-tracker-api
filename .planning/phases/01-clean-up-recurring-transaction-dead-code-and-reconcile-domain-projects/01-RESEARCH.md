# Phase 1: Clean up recurring transaction dead code and reconcile domain projects - Research

**Researched:** 2026-04-25
**Domain:** .NET solution structure / git rename / MSBuild project references
**Confidence:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Delete the entire `FinanceTracker.Domain/` folder and all 4 files within it:
  - `FinanceTracker.Domain/Categories/Category.cs`
  - `FinanceTracker.Domain/Categories/Recurrence.cs`
  - `FinanceTracker.Domain/Entities/Category.cs`
  - `FinanceTracker.Domain/Entities/FrequencyType.cs`
- **D-02:** The dead project is NOT referenced in the solution or by any other `.csproj` — deletion is safe.
- **D-03:** Rename live domain folder from `Finance.Tracker.Domain/` to `FinanceTracker.Domain/`.
- **D-04:** After renaming, update references in:
  - `FinanceTracker/FinanceTracker.API.sln`
  - `FinanceTracker.Application/FinanceTracker.Application.csproj`
  - `FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj`
- **D-05:** Entity file contents and namespaces (`FinanceTracker.Domain.Entities`) do NOT change.
- **D-06:** `dotnet build` must pass with 0 errors and 0 warnings as explicit acceptance criterion.

### Claude's Discretion
- Order of operations (delete dead project first vs. rename live project first) — Claude decides safest sequence.
- Whether to rename via `git mv` or create new folder + copy files — Claude decides based on git history preservation.

### Deferred Ideas (OUT OF SCOPE)
- None.
</user_constraints>

---

## Summary

This is a structural cleanup phase with no code logic changes. The goal is to eliminate a dead draft project (`FinanceTracker.Domain/`) that was never wired into the solution and to rename the live domain project folder (`Finance.Tracker.Domain/`) to match the `FinanceTracker.*` naming convention used by every other project.

The critical finding from codebase inspection is that **the current build already passes** (`dotnet build` → 0 errors, 0 warnings on .NET SDK 9.0.311 targeting net8.0). This serves as a clean baseline. After the rename, the same 0-error/0-warning outcome is required.

A non-obvious runtime state finding: the file `Finance.Tracker.Domain/bin/Debug/net8.0/FinanceTracker.Domain.deps.json` is tracked in git (it was committed before the `bin/` gitignore rule took effect). This artifact must be explicitly removed from git tracking as part of the operation.

**Primary recommendation:** Use `git rm -r` to delete the dead project, `git mv` to rename the live project folder (preserving history), then update the three reference strings in `.sln` and two `.csproj` files, and verify with `dotnet build`.

---

## Standard Stack

No new libraries required. This phase uses only the tools already in the project.

### Core
| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 9.0.311 (installed) | Build tool — `dotnet build` verification |
| Git | (installed) | `git rm` + `git mv` for tracked-file operations |
| PowerShell | Windows built-in | `Remove-Item` for leftover on-disk directories |

### Installation
No new packages. No `dotnet add` or `npm install` required.

---

## Architecture Patterns

### Recommended Operation Sequence

The safest sequence — confirmed by inspecting the actual file states:

```
Step 1: git rm -r FinanceTracker.Domain/          → removes 5 tracked dead-project files from git index + disk
Step 2: git rm "Finance.Tracker.Domain/bin/..."   → removes stale git-tracked bin artifact
Step 3: git mv Finance.Tracker.Domain FinanceTracker.Domain  → renames live folder, preserves git history
Step 4: Edit FinanceTracker/FinanceTracker.API.sln            → update 1 path string
Step 5: Edit FinanceTracker.Application/...csproj             → update 1 ProjectReference path
Step 6: Edit FinanceTracker.Infrastructure/...csproj          → update 1 ProjectReference path
Step 7: dotnet build FinanceTracker/FinanceTracker.API.sln    → acceptance criterion (0 errors, 0 warnings)
```

**Why this order:** Deleting the dead project first avoids confusion between the two `FinanceTracker.Domain` folder names existing simultaneously during the operation. Removing the bin artifact before `git mv` avoids it appearing as a tracked file at the new path.

### git mv vs. copy-then-delete

Use `git mv`, not manual copy + delete.

| Approach | History preserved | Tracked file handling | Notes |
|----------|------------------|-----------------------|-------|
| `git mv Finance.Tracker.Domain FinanceTracker.Domain` | Yes — `git log --follow` works | All tracked files move atomically | Preferred |
| Manual: create new folder, copy files, `git rm` old | No | Requires per-file staging | Loses history |

`git mv` on a directory works correctly on Windows when the source and target names differ (this is a genuine rename, not a case-change, so no Windows case-insensitivity issues apply).

### What `git mv` Does to bin/obj Directories

`git mv dir1 dir2` physically renames the directory on the filesystem — **all contents move**, including gitignored `bin/` and `obj/` subdirectories. After the operation:
- `Finance.Tracker.Domain/` no longer exists on disk
- `FinanceTracker.Domain/bin/` and `FinanceTracker.Domain/obj/` exist on disk but remain gitignored (no action needed for git; they will be regenerated correctly on next build)

### Exact Strings to Update

**`FinanceTracker/FinanceTracker.API.sln` — line 8:**
```
Before: "..\Finance.Tracker.Domain\FinanceTracker.Domain.csproj"
After:  "..\FinanceTracker.Domain\FinanceTracker.Domain.csproj"
```

**`FinanceTracker.Application/FinanceTracker.Application.csproj` — line 4:**
```xml
Before: <ProjectReference Include="..\Finance.Tracker.Domain\FinanceTracker.Domain.csproj" />
After:  <ProjectReference Include="..\FinanceTracker.Domain\FinanceTracker.Domain.csproj" />
```

**`FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj` — line 4:**
```xml
Before: <ProjectReference Include="..\Finance.Tracker.Domain\FinanceTracker.Domain.csproj" />
After:  <ProjectReference Include="..\FinanceTracker.Domain\FinanceTracker.Domain.csproj" />
```

No other files reference `Finance.Tracker.Domain` — confirmed by csproj-scoped grep across all `.csproj` files. The `FinanceTracker.Tests.csproj` references only `FinanceTracker.API.csproj` and `FinanceTracker.Application.csproj` (no direct domain reference). The API csproj references only Application and Infrastructure — no direct domain reference.

### Anti-Patterns to Avoid
- **Editing namespace declarations in entity files:** D-05 locks namespaces as unchanged. No C# source files need editing.
- **Deleting bin/obj manually before `git mv`:** These directories are gitignored and will move with the folder rename; no pre-clean needed.
- **Using `cp -r` / `xcopy` instead of `git mv`:** Loses git history for the entity files.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead |
|---------|-------------|-------------|
| Tracking renamed files in git | Custom file copy + re-add | `git mv` — handles staging atomically |
| Removing tracked files | Manual `del` without git | `git rm` — removes from index and disk |
| Build verification | Custom scripts | `dotnet build` — authoritative MSBuild output |

---

## Runtime State Inventory

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | None — this is a pure folder/file operation; no databases reference the domain project path | None |
| Live service config | None — no external services (CI/CD, n8n, etc.) reference the `Finance.Tracker.Domain` folder path | None |
| OS-registered state | None — no Task Scheduler tasks, pm2 processes, or other OS registrations reference this path | None |
| Secrets/env vars | None — no env vars or secrets reference `Finance.Tracker.Domain` | None |
| Build artifacts / installed packages | **Finance.Tracker.Domain/bin/Debug/net8.0/FinanceTracker.Domain.deps.json** is tracked in git (committed before `bin/` gitignore rule took effect). Other bin/obj contents are gitignored and on-disk only. | `git rm` the tracked deps.json artifact before or during the rename |

**Nothing found in remaining categories** — verified by `git ls-files` inspection.

---

## Common Pitfalls

### Pitfall 1: Forgetting the tracked bin artifact
**What goes wrong:** `git mv Finance.Tracker.Domain FinanceTracker.Domain` moves the tracked `bin/.../FinanceTracker.Domain.deps.json` to the new path. It's now staged as a rename at `FinanceTracker.Domain/bin/...` — a build artifact still committed in git, which contradicts the gitignore.
**Why it happens:** The file was committed before `bin/` was added to gitignore.
**How to avoid:** Run `git rm "Finance.Tracker.Domain/bin/Debug/net8.0/FinanceTracker.Domain.deps.json"` *before* `git mv`, so the file is removed from tracking at the old path and never appears at the new path.
**Warning signs:** After `git mv`, `git status` shows `Finance.Tracker.Domain/bin/...` renamed to `FinanceTracker.Domain/bin/...`.

### Pitfall 2: Dead project's bin/obj directories left on disk after git rm
**What goes wrong:** `git rm -r FinanceTracker.Domain/` removes only git-tracked files. The `FinanceTracker.Domain/bin/` and `FinanceTracker.Domain/obj/` directories on disk are gitignored and NOT removed by `git rm`.
**Why it happens:** `git rm` operates only on tracked files.
**How to avoid:** After `git rm -r FinanceTracker.Domain/`, run `Remove-Item -Recurse -Force FinanceTracker.Domain` (PowerShell) to clean up any remaining untracked/gitignored contents.
**Warning signs:** `FinanceTracker.Domain/` still appears in file explorer after `git rm`.

### Pitfall 3: Naming collision between old dead project name and new live project name
**What goes wrong:** After the rename, the live project directory is `FinanceTracker.Domain/` — the same name as the dead project was. If the dead project deletion is done AFTER the rename rather than before, both folders briefly have the same target name.
**Why it happens:** Doing operations out of order.
**How to avoid:** Always delete the dead `FinanceTracker.Domain/` folder FIRST, then rename `Finance.Tracker.Domain/` to `FinanceTracker.Domain/`.
**Warning signs:** "Directory already exists" error from `git mv`.

### Pitfall 4: dotnet build picks up stale obj/ artifacts
**What goes wrong:** After the folder rename, MSBuild may fail to restore/build because the `obj/` directory from the old path still has cached paths.
**Why it happens:** `project.assets.json` in `obj/` embeds absolute paths.
**How to avoid:** Run `dotnet restore` before `dotnet build` after updating references, or run `dotnet build` with `--no-incremental` on first run. In practice, dotnet is robust enough that a clean `dotnet build` will re-restore automatically.
**Warning signs:** Build error mentioning a path to `Finance.Tracker.Domain`.

---

## Code Examples

### git rm to delete all tracked files in dead project directory
```powershell
# From repo root
git rm -r FinanceTracker.Domain/
```
This removes all 5 tracked files and stages the deletions.

### Remove stale tracked bin artifact from live project
```powershell
git rm "Finance.Tracker.Domain/bin/Debug/net8.0/FinanceTracker.Domain.deps.json"
```

### git mv to rename live project folder
```powershell
git mv Finance.Tracker.Domain FinanceTracker.Domain
```
All tracked files under `Finance.Tracker.Domain/` are staged as renames to `FinanceTracker.Domain/`.

### Clean up any leftover on-disk untracked content in dead project directory
```powershell
# After git rm -r, remove any gitignored bin/obj that remain on disk
if (Test-Path "FinanceTracker.Domain") {
    Remove-Item -Recurse -Force "FinanceTracker.Domain"
}
```
Note: Run this AFTER `git rm -r FinanceTracker.Domain/` but BEFORE `git mv Finance.Tracker.Domain FinanceTracker.Domain` — otherwise the target name is clear.

### Build verification command
```powershell
dotnet build FinanceTracker/FinanceTracker.API.sln
```
Expected output: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|-------------|-----------|---------|----------|
| .NET SDK | `dotnet build` verification | ✓ | 9.0.311 | — |
| Git | `git rm`, `git mv` | ✓ | (confirmed by git status in context) | — |
| PowerShell | `Remove-Item` cleanup | ✓ | Windows built-in | — |

No missing dependencies.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 (existing) |
| Config file | `FinanceTracker.Tests/FinanceTracker.Tests.csproj` |
| Quick run command | `dotnet build FinanceTracker/FinanceTracker.API.sln` |
| Full suite command | `dotnet test FinanceTracker/FinanceTracker.API.sln` |

### Phase Requirements → Test Map
| Behavior | Test Type | Automated Command | Notes |
|----------|-----------|-------------------|-------|
| Solution builds with 0 errors, 0 warnings after rename | build verification | `dotnet build FinanceTracker/FinanceTracker.API.sln` | D-06 acceptance criterion |
| Existing tests pass after rename | test suite | `dotnet test FinanceTracker/FinanceTracker.API.sln` | Namespaces unchanged; tests should be unaffected |
| Dead project files no longer exist on disk | manual check | `Test-Path FinanceTracker.Domain/` → should return False | Structural verification |
| Live project folder at correct path | manual check | `Test-Path FinanceTracker.Domain/FinanceTracker.Domain.csproj` → should return True | Structural verification |

### Sampling Rate
- **Per task commit:** `dotnet build FinanceTracker/FinanceTracker.API.sln`
- **Phase gate:** Full suite `dotnet test FinanceTracker/FinanceTracker.API.sln` green before `/gsd-verify-work`

### Wave 0 Gaps
None — existing test infrastructure covers all phase requirements. This phase requires no new test files; `dotnet build` is the primary gate.

---

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection — `git ls-files`, file reads of all 3 reference files and both domain project csprojs
- `dotnet build` execution — confirmed baseline 0 errors/0 warnings on current state
- `.sln` file read — confirmed exact path string needing update (line 8)
- `.csproj` reads — confirmed exact `<ProjectReference>` strings in Application and Infrastructure

### Secondary (MEDIUM confidence)
- `git mv` directory rename behavior on Windows — confirmed via git documentation; standard behavior, no known Windows-specific issues for genuine renames (non-case-change)

---

## Metadata

**Confidence breakdown:**
- Operation sequence: HIGH — based on direct file inspection and git tracking state
- Exact strings to update: HIGH — read directly from the files
- git mv behavior: HIGH — standard git operation, well-documented
- Tracked bin artifact: HIGH — confirmed by `git ls-files`

**Research date:** 2026-04-25
**Valid until:** 2026-05-25 (stable .NET/git operations; no fast-moving dependencies)
