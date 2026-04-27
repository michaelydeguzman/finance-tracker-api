# Phase 4: Add background service to generate transaction instances from recurring templates - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-27
**Phase:** 04-add-background-service-to-generate-transaction-instances-from-recurring-templates
**Areas discussed:** Catch-up behavior, Service run schedule, Generated transaction field mapping, EndDate handling, Error handling strategy

---

## Catch-up Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Generate all missed occurrences | Loop forward from NextOccurrenceDate, creating a Transaction for every occurrence up to now | ✓ |
| Generate one, then advance | Create one transaction for the oldest missed date, advance once; self-healing over time | |
| Fast-forward, no catch-up | Skip missed occurrences, advance NextOccurrenceDate to next future date, create nothing for the gap | |

**User's choice:** Generate all missed occurrences  
**Notes:** Full catch-up on every run — if the worker wasn't triggered for days, all missed instances are generated.

---

## Service Run Schedule

| Option | Description | Selected |
|--------|-------------|----------|
| Every hour (in-process BackgroundService) | Polling loop inside API; API must be running | |
| Every 24 hours (in-process BackgroundService) | Same as above, daily interval | |
| Configurable via appsettings.json, default 24h | No hardcoded interval; tunable without code change | ✓ |

**User's choice:** Configurable, default every 24 hours  
**Notes:** User initially asked about Task Scheduler integration. Discussion revealed that in-process BackgroundService requires the API to always be running, which led to the architecture question below.

### Architecture Follow-up: In-process vs. External

| Option | Description | Selected |
|--------|-------------|----------|
| Separate .NET Worker Service (Windows Service) | Independent process; runs 24/7 regardless of API; installable via `sc create` | |
| Task Scheduler + CLI trigger | Console app triggered externally; API process not needed at trigger time | ✓ |
| In-process BackgroundService | Simplest; only works if API always running | |

**User's choice:** Task Scheduler + standalone console project  
**Notes:** User noted this is best for now but may change if app is hosted on cloud for multiple users. Cloud migration path noted as deferred idea.

---

## Generated Transaction Field Mapping

| Field | Option A | Option B | Selected |
|-------|----------|----------|----------|
| TransactionDate | NextOccurrenceDate (scheduled date) | DateTime.UtcNow (actual run time) | NextOccurrenceDate ✓ |
| Name | Copy from template.Name | Custom format | Copy from template.Name ✓ |
| CreatedBy | Copy from template.CreatedBy | Hardcode "system" marker | Copy from template.CreatedBy ✓ |

**User's choice:** TransactionDate = NextOccurrenceDate, Name = template.Name, CreatedBy = template.CreatedBy  
**Notes:** CreatedBy will propagate correctly once auth is wired to templates in the auth milestone.

---

## EndDate Handling

| Option | Description | Selected |
|--------|-------------|----------|
| Auto-cancel the template | Set Status = Cancelled when NextOccurrenceDate > EndDate | |
| Leave status unchanged, just stop generating | Check EndDate and skip generation if past it; Status stays Active | ✓ |
| Generate last occurrence, then auto-cancel | Generate if NextOccurrenceDate <= EndDate; cancel after last one | |

**User's choice:** Leave status unchanged, just stop generating  
**Notes:** EndDate is a generation boundary only; no status side-effects.

---

## Error Handling Strategy

| Option | Description | Selected |
|--------|-------------|----------|
| Log and skip | Catch exception per-template, log it, continue remaining templates | ✓ |
| Abort the batch | Stop entire run on first failure; retry everything next scheduled run | |
| Log, skip, and flag template | Same as log+skip but also mark template with LastError field (schema change) | |

**User's choice:** Log and skip  
**Notes:** One broken template does not block the batch. LastError flagging deferred as a future enhancement.

---

## Claude's Discretion

- DI setup inside the console app (IServiceCollection, IConfiguration, appsettings.json wiring)
- Whether to introduce IRecurringTransactionRepository or use FinanceTrackerContext directly
- Transaction-per-template vs. transaction-per-batch SaveChanges strategy
- Logging implementation (ILogger via Microsoft.Extensions.Logging or Console.WriteLine)

## Deferred Ideas

- Cloud hosting migration: adapt console/Task Scheduler to Worker Service or cloud-native scheduler when app moves to multi-user cloud hosting
- Per-user scheduling: different users on different schedules — deferred until auth milestone
- LastError field on template: flagging consistently-failing templates — deferred, log-and-skip sufficient for now
