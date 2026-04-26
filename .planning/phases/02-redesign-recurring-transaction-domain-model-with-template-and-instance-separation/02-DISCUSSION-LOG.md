# Phase 2: Redesign recurring transaction domain model with template and instance separation - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-26
**Phase:** 02-redesign-recurring-transaction-domain-model-with-template-and-instance-separation
**Areas discussed:** Template entity shape, Instance linkage, Frequency entity fate, Existing data migration

---

## Persistence Architecture (raised during Template entity shape)

| Option | Description | Selected |
|--------|-------------|----------|
| Two separate tables | `RecurringTransactions` template table + existing `Transactions`; nullable FK on Transaction | ✓ |
| Single table with discriminator (TPH) | `TransactionType` column distinguishes templates from instances | |
| Owned type / JSON column | `RecurrenceRule` JSON column on `Transactions` | |

**User's choice:** Two separate tables (Option A)
**Notes:** User confirmed this matched their intent when asked directly.

---

## Template Entity Shape

### Amount behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Fixed amount | Every instance has the same amount; changes require editing the template | |
| Default amount, instances can override | Template sets default; individual instances may have a different amount | ✓ |
| Claude's discretion | | |

**User's choice:** Amount is a default — instances can override

---

### End date

| Option | Description | Selected |
|--------|-------------|----------|
| Open-ended (no end date) | Recurrence continues indefinitely until paused/cancelled | |
| End date optional (nullable) | Template has nullable `EndDate`; generation stops after it | ✓ |
| Claude's discretion | | |

**User's choice:** End date optional (nullable `EndDate`)

---

### Template status

| Option | Description | Selected |
|--------|-------------|----------|
| Active / Paused / Cancelled | Three states mapping directly to Phase 5 capabilities | ✓ |
| Active / Paused / Cancelled / Completed | Adds `Completed` for templates that ran to their EndDate naturally | |
| Claude's discretion | | |

**User's choice:** Active / Paused / Cancelled

---

### NextOccurrenceDate

| Option | Description | Selected |
|--------|-------------|----------|
| Stored on template | Background service reads and advances `NextOccurrenceDate` after each generation | ✓ |
| Calculated at generation time | Phase 4 derives next date from last instance's TransactionDate + frequency interval | |
| Claude's discretion | | |

**User's choice:** `NextOccurrenceDate` stored on the template

---

## Frequency Entity Fate

| Option | Description | Selected |
|--------|-------------|----------|
| Inline on template | `FrequencyType` enum + nullable `IntervalDays` directly on template; `Frequency` table dropped | |
| Keep `Frequency` as lookup table | `RecurringTransaction.FrequencyId` FK; users pick from predefined named frequencies | ✓ |
| Claude's discretion | | |

**User's choice:** Keep `Frequency` as a lookup table
**Notes:** User reasoning — lookup table allows adding/removing frequency types without changing the template entity.

### Transaction.FrequencyId removal

| Option | Description | Selected |
|--------|-------------|----------|
| Remove it | `FrequencyId` only lives on the template; instances don't need it | ✓ |
| Keep it | Retain existing column on Transaction alongside new template FK | |

**User's choice:** Remove `Transaction.FrequencyId`

---

## Existing Data Migration

| Option | Description | Selected |
|--------|-------------|----------|
| Null out FrequencyId, leave as standalone | Existing transactions become one-off entries; FrequencyId column dropped | ✓ |
| Convert to template + instance pairs | Create a template from each transaction's frequency, link as first instance | |
| Claude's discretion | | |

**User's choice:** Null out `FrequencyId` and drop the column — existing recurring-tagged transactions become standalone one-offs

---

## Claude's Discretion

- EF Core configuration details (index strategy, cascade delete behavior)
- Exact migration sequencing
- Navigation property naming conventions

## Deferred Ideas

- None
