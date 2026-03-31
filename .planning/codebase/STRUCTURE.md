# Finance Tracker API — Repository Structure

## Top-level layout

```
FinanceTracker/                 # ASP.NET Core Web API host (composition root)
FinanceTracker.Application/   # Features (MediatR), DTOs, application services
Finance.Tracker.Domain/       # Domain entities (referenced by Application + Infrastructure)
FinanceTracker.Domain/        # Legacy/duplicate domain folder (not wired into current .csproj chain)
FinanceTracker.Infrastructure/# EF Core, migrations, repositories
FinanceTracker.Tests/         # xUnit tests
.github/                      # CI workflows
.planning/                    # Planning artifacts (this folder)
```

Primary solution entry: `FinanceTracker/FinanceTracker.API.sln`.

## Host (`FinanceTracker/`)

- `Program.cs` — DI, middleware, MediatR registration, Swagger.
- `FinanceTracker.API.csproj` — Web SDK; package refs for EF Core, MediatR, versioning, Swagger.
- `Controllers/` — Versioned API controllers:
  - `CategoriesV1Controller.cs` — category CRUD and list.
  - `TransactionsV1Controller.cs` — transaction operations.
  - `RecurringOptionsV1Controller.cs` — frequency/recurrence reads.
  - `DashboardV1Controller.cs` — stub/placeholder (namespace differs from sibling controllers).

## Application (`FinanceTracker.Application/`)

- `Features/` — Vertical slices by area:
  - `Categories/Commands/...`, `Categories/Queries/...`
  - `Transactions/Commands/...`, `Transactions/Queries/...`
  - `Frequencies/Queries/...`
- `Dtos/` — Request bodies (`Create*Dto`, `Update*Dto`) and `Responses/` (`ApiResponseDto`, `*ResponseDto`).
- `Services/` — `ICategoryService`, `ITransactionService`, `IFrequencyService` and implementations used by MediatR handlers.
- `FinanceTracker.Application.csproj` — references `Finance.Tracker.Domain` path and `FinanceTracker.Infrastructure`.

## Domain (`Finance.Tracker.Domain/`)

- `Entities/` — `Category.cs` (includes `CategoryType` enum), `Frequency.cs`, `Transaction.cs`.
- `FinanceTracker.Domain.csproj` — net8 class library with no project references.

## Infrastructure (`FinanceTracker.Infrastructure/`)

- `Persistence/FinanceTrackerContext.cs` — `DbContext`; applies configurations from executing assembly.
- `Persistence/Configurations/` — `CategoryConfiguration`, `FrequencyConfiguration`, `TransactionConfiguration`.
- `Persistence/*Repository.cs` — concrete EF repositories.
- `Persistence/I*Repository.cs` — persistence ports **co-located** with Infrastructure (consumed by Application services).
- `Migrations/` — EF Core migration snapshots and designer files.
- `FinanceTracker.Infrastructure.csproj` — EF Core SqlServer; references domain project only.

## Tests (`FinanceTracker.Tests/`)

- `FinanceTracker.Tests.csproj` — references `FinanceTracker` (API) and `FinanceTracker.Application`.
- `Integration/` — `FinanceTrackerWebApplicationFactory`, `*ApiIntegrationTests`, JSON helpers.
- `Unit/Controllers/` — controller-focused tests (e.g. `TransactionsV1ControllerTests`).
- `Unit/Handlers/` — MediatR handler tests (`CreateCategoryCommandHandlerTests`, transaction command handler tests).
- `GlobalUsings.cs` — shared test usings.

## Naming conventions observed

- **Projects:** `FinanceTracker.*` for application, infrastructure, tests; API host project folder is `FinanceTracker` with assembly/API naming `FinanceTracker.API`.
- **Controllers:** `*V1Controller`, routes `api/v{version:apiVersion}/...`.
- **MediatR:** `*Command` / `*Query` with matching `*Handler` in the same feature folder.
- **DTOs:** `Create*`, `Update*` for input; `*ResponseDto` for output; `ApiResponseDto<T>` for envelope.
- **Persistence:** `IEntityRepository` + `EntityRepository`; `FinanceTrackerContext`; `*Configuration` for Fluent API.
- **Namespaces:** `FinanceTracker.Controllers`, `FinanceTracker.Application.*`, `FinanceTracker.Infrastructure.Persistence`, `FinanceTracker.Domain.Entities` (physical folder `Finance.Tracker.Domain`).

## Paths quick reference

| Concern | Location |
|---------|----------|
| Run / configure app | `FinanceTracker/Program.cs` |
| REST endpoints | `FinanceTracker/Controllers/` |
| Use cases & handlers | `FinanceTracker.Application/Features/` |
| Cross-cutting app API shape | `FinanceTracker.Application/Dtos/` |
| Orchestration services | `FinanceTracker.Application/Services/` |
| Core model | `Finance.Tracker.Domain/Entities/` |
| Database & repos | `FinanceTracker.Infrastructure/Persistence/` |
| Schema migrations | `FinanceTracker.Infrastructure/Migrations/` |
