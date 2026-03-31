# Finance Tracker API — Architecture

## Solution and entry point

- **Solution file:** `FinanceTracker/FinanceTracker.API.sln` groups the runnable host and libraries.
- **Composition root / host:** `FinanceTracker/Program.cs` configures SQL Server (`FinanceTrackerDB`), registers EF `DbContext`, repository and application service pairs, ASP.NET Core MVC, API versioning, MediatR (scanning the Application assembly via `GetCategoriesQuery`), and Swagger.
- **Public HTTP surface:** controllers under `FinanceTracker/Controllers/` (`*V1Controller.cs`) expose versioned routes; handlers are thin and delegate to MediatR `ISender`.

## Layering and project dependencies

The codebase follows a **layered, CQRS-flavored** layout inspired by clean architecture, with **pragmatic deviations**:

| Layer | Project | References |
|--------|---------|------------|
| **Presentation** | `FinanceTracker` (`FinanceTracker.API.csproj`) | `FinanceTracker.Application`, `FinanceTracker.Infrastructure` |
| **Application** | `FinanceTracker.Application` | `Finance.Tracker.Domain` (on disk), `FinanceTracker.Infrastructure` |
| **Infrastructure** | `FinanceTracker.Infrastructure` | `Finance.Tracker.Domain` |
| **Domain** | `Finance.Tracker.Domain` (`FinanceTracker.Domain.csproj`) | *(none — class library)* |

**Note:** On disk the domain project lives under `Finance.Tracker.Domain/` while root namespace is `FinanceTracker.Domain`. A second folder `FinanceTracker.Domain/` exists with older/parallel types and is **not** referenced by the solution projects analyzed here.

**Dependency observation:** `FinanceTracker.Application` references `FinanceTracker.Infrastructure`, so the Application layer is **not** strictly dependency-inverted relative to persistence: repository **interfaces** (`ICategoryRepository`, etc.) live in `FinanceTracker.Infrastructure/Persistence/` alongside EF implementations, and Application services consume those abstractions directly. The host remains the place that wires concrete types.

## Patterns in use

- **MediatR** — Commands and queries live under `FinanceTracker.Application/Features/{Aggregate}/Commands|Queries/...` with `IRequest` / `IRequestHandler` handlers. Controllers send messages; handlers often call **`ICategoryService`**, `ITransactionService`, or `IFrequencyService` abstractions in `FinanceTracker.Application/Services/`.
- **Application services** — `CategoryService`, `TransactionService`, `FrequencyService` encapsulate use-case orchestration over repositories (still infrastructure-defined interfaces).
- **Repository + EF Core** — `FinanceTracker.Infrastructure/Persistence/` contains `FinanceTrackerContext`, entity `IConfiguration` classes under `Persistence/Configurations/`, and repository classes targeting domain entities.
- **API versioning** — `Asp.Versioning` with URL segment, query string, and header readers; controllers use `[ApiVersion("1.0")]` and route prefixes like `api/v{version:apiVersion}/...`.
- **Uniform API responses** — `ApiResponseDto<T>` and entity-mapped response DTOs under `FinanceTracker.Application/Dtos/`.

## Data flow (request path)

1. HTTP request hits a controller in `FinanceTracker/Controllers/`.
2. Controller validates `ModelState` where applicable and calls `_sender.Send(new SomeCommandOrQuery(...))`.
3. MediatR dispatches to a handler in `FinanceTracker.Application/Features/...`.
4. Handler uses an application service; the service calls a repository interface implemented in Infrastructure.
5. Repository uses `FinanceTrackerContext` to persist or read; configurations in `Persistence/Configurations/` define the model.
6. Handler maps domain entities to response DTOs and returns to the controller, which wraps or returns them per `ApiResponseDto` conventions.

## Domain model

- **Entities:** `Category`, `Frequency`, `Transaction` in `Finance.Tracker.Domain/Entities/` (namespace `FinanceTracker.Domain.Entities`).
- Entities carry **data annotations** (`[Key]`, `[Required]`, etc.), aligning the domain assembly with EF mapping expectations (pragmatic anemic model).

## Testing

- `FinanceTracker.Tests` references the API and Application projects, supporting integration-style tests against the host (`Microsoft.AspNetCore.Mvc.Testing`) and unit tests with xUnit, Moq, FluentAssertions, and EF InMemory where needed.

## Architectural trade-offs (summary)

- **Strengths:** Clear separation of Web host vs Application feature folders vs EF persistence; testable handlers; versioned API.
- **Tightening options (not current state):** Move repository (and unit-of-work) **interfaces** into Application or Domain and drop Application → Infrastructure reference; keep Infrastructure implementing interfaces only; optionally reduce data annotations from entities in favor of Fluent API only in Infrastructure.
