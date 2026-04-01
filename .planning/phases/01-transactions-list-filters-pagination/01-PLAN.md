---
wave: 1
depends_on: []
autonomous: true
requirements: [TRX-01, TRX-02, TRX-03, TRX-04, TRX-05, TRX-06, TRX-07, TRX-08, TRX-09]
files_modified:
  - FinanceTracker.Application/FinanceTracker.Application.csproj
  - FinanceTracker.Application/Dtos/Responses/PagedTransactionsResponseDto.cs
  - FinanceTracker.Application/Features/Transactions/Queries/GetTransactionsList/GetTransactionsListQuery.cs
  - FinanceTracker.Application/Features/Transactions/Queries/GetTransactionsList/GetTransactionsListQueryHandler.cs
  - FinanceTracker.Application/Features/Transactions/Queries/GetTransactionsList/GetTransactionsListResult.cs
  - FinanceTracker.Infrastructure/Persistence/ITransactionRepository.cs
  - FinanceTracker.Infrastructure/Persistence/TransactionRepository.cs
  - FinanceTracker/Controllers/TransactionsV1Controller.cs
---

<objective>
Implement `GET /api/v1/transactions` list filters (date range, multi-category Guid IDs), optional pagination with `items` + `totalCount`, deterministic ordering (paginated vs unpaginated per research), `pageSize` cap 20, empty `categoryIds` guard, and backward-compatible unpaginated envelope—without breaking existing `categoryType`-only callers.
</objective>

<must_haves>
- Unpaginated requests return `ApiResponseDto<List<TransactionResponseDto>>` with ordering unchanged from pre-phase behavior (`CreatedAt` desc).
- Paginated requests return `ApiResponseDto<PagedTransactionsResponseDto>` with `Items` and `TotalCount`, ordered by `TransactionDate` desc then `Id` desc.
- `categoryIds` use **Guid** values; TRX-03 preserved when `categoryIds` absent.
- TRX-07/TRX-09 return HTTP 400 with clear `ApiResponseDto` failure message (match existing error style).
</must_haves>

<verification>
- `dotnet build FinanceTracker/FinanceTracker.API.sln` exits 0.
- `grep -r "GetAllTransactionsQuery" FinanceTracker` only appears if obsolete type removed or unused (prefer removed / no controller references).
- Controller contains action named `GetTransactions` with bindings for date and paging parameters (e.g. `from`, `to`, `categoryIds`, `page`, `pageSize`) alongside `categoryType`.
</verification>

---

## Task 1-01-00 — EF Core package for async query operators in Application

<task id="1-01-00">
<action>
Add a **PackageReference** to `FinanceTracker.Application/FinanceTracker.Application.csproj`:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
```

(Version **8.0.0** to match `FinanceTracker.Infrastructure`.)
</action>
<read_first>
- FinanceTracker.Application/FinanceTracker.Application.csproj
- FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj
</read_first>
<acceptance_criteria>
- `FinanceTracker.Application.csproj` contains `Microsoft.EntityFrameworkCore` Version `8.0.0`.
- `dotnet build FinanceTracker/FinanceTracker.API.sln` exits 0.
</acceptance_criteria>
</task>

---

## Task 1-01-01 — Response and MediatR result types

<task id="1-01-01">
<action>
Add `FinanceTracker.Application/Dtos/Responses/PagedTransactionsResponseDto.cs` as a **record** with properties exactly:
- `IReadOnlyList<TransactionResponseDto> Items`
- `int TotalCount`

Add `FinanceTracker.Application/Features/Transactions/Queries/GetTransactionsList/GetTransactionsListResult.cs` as a **record** with:
- `bool IsPaged`
- `IReadOnlyList<TransactionResponseDto> Items`
- `int? TotalCount` (non-null when `IsPaged` is true)

Add `FinanceTracker.Application/Features/Transactions/Queries/GetTransactionsList/GetTransactionsListQuery.cs` as a **sealed record** `GetTransactionsListQuery` implementing `IRequest<GetTransactionsListResult>` with parameters:
- `CategoryType? CategoryType`
- `DateTime? FromUtc`
- `DateTime? ToUtc`
- `IReadOnlyList<Guid>? CategoryIds`
- `bool CategoryIdsParameterPresent` (true when the HTTP request included `categoryIds` at all, even if empty)
- `int? Page` (1-based)
- `int? PageSize`
</action>
<read_first>
- FinanceTracker.Application/Features/Transactions/Queries/GetAllTransactions/GetAllTransactionsQuery.cs
- FinanceTracker.Application/Dtos/Responses/TransactionResponseDto.cs
- FinanceTracker.Application/Dtos/Responses/ApiResponseDto.cs
</read_first>
<acceptance_criteria>
- `PagedTransactionsResponseDto.cs` exists and contains literal property names `Items` and `TotalCount`.
- `GetTransactionsListQuery.cs` exists and references `GetTransactionsListResult` as its `IRequest<>` type argument.
- `dotnet build FinanceTracker/FinanceTracker.API.sln` exits 0.
</acceptance_criteria>
</task>

---

## Task 1-01-02 — Repository composable query

<task id="1-01-02">
<action>
Extend `ITransactionRepository` with a new method: `IQueryable<Transaction> GetTransactionsQueryable();`

Implement in `TransactionRepository` to return `_context.Transactions.AsNoTracking().Include(x => x.Category).Include(x => x.Frequency)` **without** materializing. Do **not** change existing `GetAllAsync` ordering in this task (handler will apply ordering).

Remove any new usings that violate project style; keep file-scoped or block namespaces consistent with each file.
</action>
<read_first>
- FinanceTracker.Infrastructure/Persistence/ITransactionRepository.cs
- FinanceTracker.Infrastructure/Persistence/TransactionRepository.cs
- Finance.Tracker.Domain/Entities/Transaction.cs
</read_first>
<acceptance_criteria>
- `ITransactionRepository` declares `GetTransactionsQueryable`.
- `TransactionRepository.GetTransactionsQueryable` body contains `AsNoTracking()`, `Include(x => x.Category)`, `Include(x => x.Frequency)`.
- `dotnet build FinanceTracker/FinanceTracker.API.sln` exits 0.
</acceptance_criteria>
</task>

---

## Task 1-01-03 — Handler: filters, ordering, paging, categoryType rules

<task id="1-01-03">
<action>
Add `GetTransactionsListQueryHandler` implementing `IRequestHandler<GetTransactionsListQuery, GetTransactionsListResult>`.

Inject `ITransactionRepository` (`FinanceTracker.Infrastructure.Persistence`). Add `using Microsoft.EntityFrameworkCore;` for `CountAsync` / `ToListAsync`.

**Preconditions (enforced in Task 1-01-04, not here):** `Page`/`PageSize` both set or both null; if set then `page >= 1` and `1 <= pageSize <= 20`; if `CategoryIdsParameterPresent` then `CategoryIds` is non-null and `Count > 0`.

**Algorithm:**

1. `var query = _transactionRepository.GetTransactionsQueryable();`

2. **TRX-01 — dates:** If `FromUtc` has value → `query = query.Where(t => t.TransactionDate >= request.FromUtc.Value)`. If `ToUtc` has value → `query = query.Where(t => t.TransactionDate <= request.ToUtc.Value)`.

3. **TRX-02 / TRX-03 / combined:** If `CategoryIds` is non-null with any items → `query = query.Where(t => request.CategoryIds!.Contains(t.CategoryId))`. If `request.CategoryType` has value → always apply `query = query.Where(t => t.Category.CategoryType == request.CategoryType.Value)` (intersection with ID filter when both apply).

4. **Paging (TRX-04/05):** If `Page` and `PageSize` are non-null → **paged** branch. Else → **unpaged** branch.

5. **Unpaged (TRX-08):** `query = query.OrderByDescending(t => t.CreatedAt)`; `var list = await query.ToListAsync(cancellationToken)`; return `new GetTransactionsListResult(false, list.Select(TransactionResponseDto.FromEntity).ToList(), null)`.

6. **Paged (TRX-05/06/07):** `query = query.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.Id)`; `var total = await query.CountAsync(cancellationToken)`; `var page = request.Page!.Value`; `var size = request.PageSize!.Value`; `var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken)`; return `new GetTransactionsListResult(true, items.Select(TransactionResponseDto.FromEntity).ToList(), total)`.

Remove `GetAllTransactionsQuery` and `GetAllTransactionsQueryHandler` in this task once the controller no longer references them, or in Task 1-01-04 after compile is fixed—leave no dead code.
</action>
<read_first>
- FinanceTracker.Application/Features/Transactions/Queries/GetAllTransactions/GetAllTransactionsQueryHandler.cs
- FinanceTracker.Infrastructure/Persistence/ITransactionRepository.cs
- FinanceTracker.Application/Services/TransactionService.cs
</read_first>
<acceptance_criteria>
- `GetTransactionsListQueryHandler.cs` exists under `Features/Transactions/Queries/GetTransactionsList/`.
- Handler references `OrderByDescending` on `TransactionDate` for paginated branch (grep `TransactionDate` in that file).
- Handler references `OrderByDescending` on `CreatedAt` for unpaginated branch (grep `CreatedAt` in that file).
- `dotnet build FinanceTracker/FinanceTracker.API.sln` exits 0.
</acceptance_criteria>
</task>

---

## Task 1-01-04 — Controller: binding, validation, response shapes

<task id="1-01-04">
<action>
Update `TransactionsV1Controller.GetTransactions`:

**Parameters (example signature—adjust to compile):**
- `[FromQuery] CategoryType? categoryType`
- `[FromQuery] DateTime? from` and `[FromQuery] DateTime? to` **or** `DateOnly?` converted to UTC—choose **DateTime?** named `from`/`to` if model binding works; if not, use `string?` + `DateTime.TryParse`.

Use **`[FromQuery] List<Guid>? categoryIds`** for ID list binding.

**TRX-09:** Determine `categoryIdsParameterPresent` via `Request.Query.ContainsKey("categoryIds")` (case-insensitive per ASP.NET default). If present and list null or empty → `return BadRequest(ApiResponseDto<PagedTransactionsResponseDto>.Fail(...))` **or** use a **non-generic** fail overload consistent with the codebase—match existing `ApiResponseDto.Fail` usage from other actions.

**Partial paging:** If exactly one of `page` / `pageSize` is null (XOR) → `BadRequest` with explicit message mentioning both must be supplied together.

**TRX-07:** If paging active and `pageSize > 20` OR `page < 1` → `BadRequest`.

Build `GetTransactionsListQuery` with normalized fields and call `_sender.Send`.

**Response typing:**
- If result `IsPaged == false`: `return Ok(ApiResponseDto<List<TransactionResponseDto>>.Ok(result.Items.ToList()));`
- If true: `return Ok(ApiResponseDto<PagedTransactionsResponseDto>.Ok(new PagedTransactionsResponseDto(result.Items.ToList(), result.TotalCount!.Value)));`

Remove `GetAllTransactionsQuery` usage from this controller. Delete `GetAllTransactionsQuery.cs` and handler files **if** no other references (search solution).

**Method return type:** Use `Task<IActionResult>` or `ActionResult` if generic conflicts—Swagger may show looser typing; acceptable for this phase.
</action>
<read_first>
- FinanceTracker/Controllers/TransactionsV1Controller.cs
- FinanceTracker.Application/Dtos/Responses/ApiResponseDto.cs
- FinanceTracker.Application/Features/Transactions/Queries/GetTransactionsList/GetTransactionsListQuery.cs
</read_first>
<acceptance_criteria>
- `TransactionsV1Controller.cs` contains `GetTransactionsListQuery` string OR `new GetTransactionsListQuery` in the GET action.
- `TransactionsV1Controller.cs` contains `ContainsKey("categoryIds")` (grep).
- `dotnet build FinanceTracker/FinanceTracker.API.sln` exits 0.
- `dotnet test FinanceTracker/FinanceTracker.API.sln --no-build` may still fail (tests updated in Plan 02)—running tests is optional in this plan; **build must pass**.
</acceptance_criteria>
</task>

---

## PLANNING COMPLETE
