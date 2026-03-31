# Coding conventions — `FinanceTracker` solution

This repo is a .NET **8** layered API: `FinanceTracker` (host), `FinanceTracker.Application`, `FinanceTracker.Infrastructure`, `FinanceTracker.Domain`, with tests in `FinanceTracker.Tests`. Paths below are relative to the repository root unless noted.

## Style and language

- **C#** with **nullable reference types** enabled (`Nullable` / `ImplicitUsings` on API and test projects; see `FinanceTracker/FinanceTracker.API.csproj`, `FinanceTracker.Tests/FinanceTracker.Tests.csproj`).
- **File-scoped namespaces** are common (for example `FinanceTracker.Controllers` in `FinanceTracker/Controllers/TransactionsV1Controller.cs`). Some infrastructure types still use block namespaces (for example `FinanceTracker.Infrastructure.Persistence` in `FinanceTracker.Infrastructure/Persistence/ITransactionRepository.cs`).
- **Top-level statements** in `FinanceTracker/Program.cs` for host setup; `public partial class Program` at file end supports `WebApplicationFactory<Program>` in tests.

## Naming

- **Projects**: `FinanceTracker.API` assembly in folder `FinanceTracker/` (csproj name `FinanceTracker.API.csproj`).
- **API surface**: controllers use version suffix + `V1` + `Controller`, for example `TransactionsV1Controller` in `FinanceTracker/Controllers/TransactionsV1Controller.cs`.
- **CQRS / MediatR**: feature folders under `FinanceTracker.Application/Features/<Area>/` with `Commands/<Verb><Entity>/` and `Queries/<Verb><Entity>/` containing `<Name>Command.cs`, `<Name>CommandHandler.cs`, `<Name>Query.cs`, `<Name>QueryHandler.cs`.
- **DTOs**: request/response shapes in `FinanceTracker.Application/Dtos/` and `FinanceTracker.Application/Dtos/Responses/` (for example `ApiResponseDto<T>` in `FinanceTracker.Application/Dtos/Responses/ApiResponseDto.cs`).
- **Async**: `*Async` suffix on service and repository methods (for example `AddTransactionAsync`, `GetByIdAsync` in `FinanceTracker.Application/Services/TransactionService.cs` and `FinanceTracker.Infrastructure/Persistence/ITransactionRepository.cs`).

## Architectural patterns

- **MediatR** handlers implement `IRequestHandler<,>`; controllers depend on `ISender` and delegate with `Send(...)` — see `FinanceTracker/Controllers/TransactionsV1Controller.cs` and handlers such as `FinanceTracker.Application/Features/Transactions/Commands/CreateTransaction/CreateTransactionCommandHandler.cs`.
- **Thin controllers**: validation gates (`ModelState`), HTTP mapping, and envelope `ApiResponseDto<T>`; domain work lives in application/infrastructure.
- **Application services** wrap repositories and orchestrate use cases (`FinanceTracker.Application/Services/` with interfaces `I*`).
- **EF Core**: `FinanceTrackerContext` and repositories in `FinanceTracker.Infrastructure/Persistence/`; configurations under `Configurations/`.
- **API versioning**: Asp.Versioning with URL segment `api/v{version:apiVersion}/...` in `FinanceTracker/Program.cs` and controller attributes in files under `FinanceTracker/Controllers/`.

## Error handling and HTTP responses

- Consistent envelope: `ApiResponseDto<T>.Ok(...)` / `ApiResponseDto<T>.Fail(message)` in `FinanceTracker.Application/Dtos/Responses/ApiResponseDto.cs`.
- Controllers return `BadRequest` / `NotFound` with the same envelope where applicable (for example invalid model and missing transaction in `FinanceTracker/Controllers/TransactionsV1Controller.cs`).
- **No global exception middleware** surfaced in `FinanceTracker/Program.cs`; failures are largely explicit branch returns at the controller or null/bool results from handlers.

## Async / cancellation

- Handlers and controllers use `async Task<...>`; MediatR `Handle` accepts `CancellationToken` (for example `CreateTransactionCommandHandler` in `FinanceTracker.Application/Features/Transactions/Commands/CreateTransaction/CreateTransactionCommandHandler.cs`). Call sites in controllers typically do not pass the token through to `Send` in the snippets reviewed — a possible consistency improvement.

## Dependency injection (`FinanceTracker/Program.cs`)

- **DbContext**: `AddDbContext<FinanceTrackerContext>` with SQL Server from configuration connection string `FinanceTrackerDB`.
- **Scoped** registrations for repositories (`ICategoryRepository`, `IFrequencyRepository`, `ITransactionRepository`), services (`ICategoryService`, `IFrequencyService`, `ITransactionService`), and concrete infrastructure types.
- **MediatR**: `RegisterServicesFromAssemblies` anchored to `GetCategoriesQuery` assembly (`FinanceTracker.Application`).
- Standard ASP.NET Core: `AddControllers`, Swagger/OpenAPI in Development, HTTPS, authorization pipeline placeholder, `MapControllers`.

## Related paths

- Host: `FinanceTracker/Program.cs`
- Example controller: `FinanceTracker/Controllers/TransactionsV1Controller.cs`
- Example handler: `FinanceTracker.Application/Features/Transactions/Commands/CreateTransaction/CreateTransactionCommandHandler.cs`
- Service layer: `FinanceTracker.Application/Services/TransactionService.cs`
