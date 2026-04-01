---
wave: 2
depends_on: ["01"]
autonomous: true
requirements: [TRX-01, TRX-02, TRX-03, TRX-04, TRX-05, TRX-06, TRX-07, TRX-08, TRX-09]
files_modified:
  - FinanceTracker.Tests/Integration/TransactionsApiIntegrationTests.cs
---

<objective>
Add integration tests proving `GET /api/v1/transactions` meets TRX-01–TRX-09: date bounds, multi-category Guid filters, `categoryType` backward compatibility, pagination envelope + ordering + cap, unpaginated legacy shape, and 400s for bad paging and empty `categoryIds`.
</objective>

<must_haves>
- Every requirement TRX-01–TRX-09 has at least one automated integration assertion mapped in comments or test names.
- Tests use **Guid** category IDs (create categories via existing API helpers).
- `dotnet test FinanceTracker/FinanceTracker.API.sln` exits 0.
</must_haves>

<verification>
- `dotnet test FinanceTracker/FinanceTracker.API.sln` exits 0.
- New test methods include `TransactionsList_` prefix in their names for `--filter` ergonomics.
</verification>

---

## Task 1-02-01 — Integration tests for transactions list

<task id="1-02-01">
<action>
Extend `FinanceTracker.Tests/Integration/TransactionsApiIntegrationTests.cs` (or add `TransactionsListApiIntegrationTests.cs` if you prefer separation—**prefer same file** to reuse `CreateCategoryViaApiAsync` and `ResetDatabase` patterns).

Add **independent** `[Fact]` methods (names must start with `TransactionsList_`) covering:

| Test | Requirement | Concrete checks |
|------|-------------|----------------|
| `TransactionsList_ByDateRange_FiltersInclusive` | TRX-01 | Seed txs with different `TransactionDate` (via POST); GET with `from`/`to`; expect only in-range IDs |
| `TransactionsList_ByCategoryIds_FiltersToSelectedGuids` | TRX-02 | Two categories + txs; GET `?categoryIds={a}&categoryIds={b}` returns only those |
| `TransactionsList_CategoryType_WhenCategoryIdsOmitted_StillFilters` | TRX-03 | Matching old behavior: only `categoryType=Expense` without `categoryIds` excludes other type |
| `TransactionsList_Paged_ReturnsItemsAndTotalCount` | TRX-04/05 | `?page=1&pageSize=2`; deserialize `ApiResponseDto<PagedTransactionsResponseDto>`; `TotalCount >= 3` if seeded 3+; `Items.Count <= 2` |
| `TransactionsList_Paged_OrderedByTransactionDateDescThenIdDesc` | TRX-06 | Seed same calendar date, different Ids; verify order in `Items` |
| `TransactionsList_PageSizeOver20_Returns400` | TRX-07 | `pageSize=21` expect `HttpStatusCode.BadRequest` |
| `TransactionsList_Unpaged_ReturnsListEnvelope_NotPagedDto` | TRX-08 | GET without page/pageSize returns `ApiResponseDto<List<TransactionResponseDto>>` with 200 (use `ReadFromJsonAsync` with `JsonSerializerOptions` matching `HttpJsonOptions.ForApi`—**not** paged DTO) |
| `TransactionsList_EmptyCategoryIdsQuery_Returns400` | TRX-09 | Request includes `categoryIds` key with empty value (e.g. `/api/v1/transactions?categoryIds=`); expect 400 |

**Seeding notes:** Use `CreateTransactionDto` + POST `/api/v1/transactions` like the existing end-to-end test. Use UTC dates consistent with existing tests.

**JSON deserialization:** For paged responses, `ApiResponseDto<PagedTransactionsResponseDto>` must deserialize case-insensitive matching `HttpJsonOptions.ForApi` (same as existing tests).

If `PagedTransactionsResponseDto` uses `IReadOnlyList<>` and System.Text.Json struggles, deserialize with a DTO that uses `List<TransactionResponseDto>` for `Items` in tests only—**prefer fixing production DTO** to be JSON-friendly (init-only properties on record are usually fine).

After adding tests, run `dotnet test FinanceTracker/FinanceTracker.API.sln` and fix failures by correcting tests or filing a bug—**executor goal: green suite**.
</action>
<read_first>
- FinanceTracker.Tests/Integration/TransactionsApiIntegrationTests.cs
- FinanceTracker.Tests/Integration/HttpJsonOptions.cs
- FinanceTracker.Application/Dtos/Responses/PagedTransactionsResponseDto.cs
- FinanceTracker.Application/Dtos/Responses/ApiResponseDto.cs
</read_first>
<acceptance_criteria>
- At least **one** test method name starts with `TransactionsList_` per row in the table above (8 methods minimum).
- `dotnet test FinanceTracker/FinanceTracker.API.sln` exits **0**.
- `TransactionsApiIntegrationTests.cs` (or new file) contains the string `PagedTransactionsResponseDto`.
</acceptance_criteria>
</task>

---

## PLANNING COMPLETE
