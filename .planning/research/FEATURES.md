# Feature Landscape: Transaction List + Filtering (Personal Finance)
**Domain:** Personal finance transaction list experience (list, filters, presets, pagination, sorting, export)  
**Researched:** 2026-03-31  
**Scope fit:** Designed to inform your `.NET 8` API roadmap constraints (optional pagination, stable ordering, date/category filters, export)

## Table Stakes

Features users expect in a modern transaction list. Missing these makes the experience feel “broken” or incomplete.

| Feature | Why expected | Complexity | Notes (API/UI implications) |
|---|---|---:|---|
| **Fast text search** (payee/merchant + memo/notes) | Users often remember “where” not “when” | Med | Requires indexed/searchable fields; consider server-side “contains” search plus debounced UI. |
| **Date range filter** (custom) | Core mental model for finance browsing | Low | Inclusive `from`/`to` bounds; timezone policy should be explicit (“client supplies intended bounds”). |
| **Quick date presets** (This month, Last month, YTD, Last 30/90 days) | Reduces friction vs. always picking dates | Low | UI-only if API supports from/to; presets just map to bounds. |
| **Category filter** (single + multi-select) | Standard way to narrow spending | Med | Your active requirement `categoryIds[]` supports multi-select; keep “All” obvious. |
| **Account filter** (per-account and “All accounts”) | People reconcile per account | Med | If you have account dimension; if not yet, expect demand soon. |
| **Amount filter** (min/max + income/expense toggle) | Common “find big transactions” task | Med | Better as server-side range filter; include sign handling (credits vs debits). |
| **Sorting** (date, amount; stable) | Users scan newest-first but sometimes need “largest” | Low | Default: `TransactionDate desc, Id` tie-break (good). Allow override sort fields when ready. |
| **Pagination or infinite scroll** with stable results | Lists can be large; mobile especially | Med | Your API: optional paging + `totalCount` when paged. Consider cursor-based later if scale grows. |
| **Deterministic ordering across pages** | Prevents duplicates/missing rows | Low | Tie-breaker by `Id` (or `CreatedAt`) is required. |
| **Clear filter state** (chips/badges) + “Clear all” | Users get lost in filter combinations | Low | UI: show active filters; API: echo applied filters is a nice-to-have. |
| **Export to CSV from filtered view** | Finance apps commonly offer exports | Med | Expect “export what I’m looking at” (filters applied). Consider async export for large ranges. |

## Differentiators

These make the transaction list feel premium. Build selectively; each adds product leverage but also edge cases.

| Feature | Value proposition | Complexity | Notes (what tends to be tricky) |
|---|---|---:|---|
| **Saved filter presets / views** (“My reviews”, “Work reimbursements”, “Subscriptions”) | Turns repeated filtering into 1 click | Med | Needs a “saved search” model (name + filter JSON + ownership). Decide whether presets are per-user only. |
| **Rules / auto-categorization from list** (merchant → category, tags) | Reduces ongoing manual work | High | Rule priority, conflicts, retroactive apply, preview, audit history. |
| **Bulk edit** (category, notes, tags, account, delete) | Fixes imports quickly | High | Requires careful permissions, validations, and idempotency; partial failure UX. |
| **Duplicate detection / merge suggestions** | Fixes bank import quirks | High | Matching heuristics; avoid false positives; allow undo. |
| **Split transactions** | Handles real-world receipts accurately | High | Parent/child modeling, reporting correctness, export semantics. |
| **Attachments** (receipt upload) | Tax/audit readiness | High | Storage, privacy, retention, cost, mobile UX. |
| **Inline enrichments** (merchant logo, location, category suggestions) | Faster scanning + trust | Med | Vendor dependencies; privacy; caching. |
| **Keyboard-first desktop UX** (power user mode) | Finance apps have spreadsheet-like expectations | Med | Selection model, shortcuts, accessibility. |
| **Audit trail** (what changed, when, by who) | Trust + debugging | Med | Particularly valuable if rules/bulk edit exist. |
| **Export “reports” formats** (PDF, categorized summaries) | Share with advisors/records | Med/High | Often not MVP; ensure consistent totals and formatting. |

## Anti-Features (Avoid)

These commonly create complexity without proportional user value for an MVP “transaction list with filters” experience.

| Anti-Feature | Why avoid | What to do instead |
|---|---|---|
| **Too many filters at once** (dense advanced panel) | Discoverability suffers; users feel overwhelmed | Start with date/category/account/search/amount; add “More filters” progressively. |
| **Sorting by “computed” or unstable fields** early | Breaks paging and creates confusing ordering | Keep server-side stable sorts (date, amount, id). Add advanced sorts after you have cursor-based paging or strong ordering guarantees. |
| **Client-only filtering on large datasets** | Slow, memory heavy, inconsistent with export | Keep filtering server-side; UI can do small local refinements only when data is already paged. |
| **Export that ignores filters** or exports only “current page” silently | Violates user expectations; creates distrust | Always export “filtered set” (with clear range). If you must export page-only, label it explicitly. |
| **Complex “smart” categories** (auto-hierarchies) too early | Users disagree with automation; migration pain | Keep explicit categories + optional tags; add hierarchy later with migration plan. |
| **Timezone normalization changes mid-stream** | Creates hard-to-debug off-by-one-day issues | Keep current “API dumb, client provides bounds” policy; document it and be consistent. |

## Feature Dependencies (Practical Ordering)

```text
Stable deterministic ordering → Pagination/infinite scroll
Date range filter → Date presets (UI mapping)
Multi-category filtering → Saved views (better usability)
Bulk edit → Audit trail / undo (strongly recommended)
Rules/auto-categorization → Audit trail + backfill strategy
Split transactions → Correct export semantics + reporting alignment
Export from filtered view → Server-side filtering + consistent field schema
```

## MVP Recommendation (Transaction List Experience)

Prioritize (table stakes, aligned to your active requirements):
1. **Date range filter (`from`/`to`) + quick presets**
2. **Multi-category filter (`categoryIds[]`)** with backward-compatible `categoryType` fallback
3. **Optional pagination with stable default ordering** + `totalCount` in an envelope when paged
4. **Export CSV of filtered result set**
5. **Search by payee/memo** (if not already present)

Defer (differentiators that expand scope sharply):
- **Rules/auto-categorization**, **bulk edit**, **split transactions**, **attachments**: high complexity and lots of edge cases; best after your list endpoint contract is stable.

## Sources

- **YNAB**: Export transactions (entire budget or selected after search/filter) and search/filter capabilities. `https://www.youneedabudget.com/blog/filing-your-taxes-just-got-a-little-easier` and related help/release notes surfaced via web search; verify in current YNAB Help Center for exact UI paths.  
- **Monarch Money**: Download transaction history CSV; filters on Transactions page affect what’s downloaded. `https://help.monarchmoney.com/hc/en-us/articles/15526600975764-Download-your-transaction-history`  
- **Rocket Money**: Export transactions as CSV; apply filters before export. `https://help.rocketmoney.com/en/articles/10296106-exporting-transactions`  
- **Actual Budget**: Transaction filtering documentation (stackable filters). `https://actualbudget.org/docs/transactions/filters/`

