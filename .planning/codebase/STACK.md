# Technology stack — Finance Tracker API

This document reflects the repository as of the mapping pass: a **.NET 8** solution with a layered architecture (API → Application → Infrastructure → Domain).

## Languages and runtime

- **Language:** C# with **nullable reference types** and **implicit usings** enabled on all projects.
- **Runtime / SDK:** `net8.0` (`TargetFramework` in every `.csproj`).
- **Entry assembly:** ASP.NET Core minimal hosting + top-level statements in `FinanceTracker/Program.cs`.

## Solution and project layout

- **Solution file:** `FinanceTracker/FinanceTracker.API.sln` — includes API, Domain, Application, Infrastructure, and Tests.
- **`FinanceTracker/FinanceTracker.API.csproj`** — ASP.NET Core Web SDK; hosts controllers, versioning, Swagger, MediatR discovery, and EF Core SQL Server registration.
- **`FinanceTracker.Application/FinanceTracker.Application.csproj`** — MediatR-only NuGet dependency; CQRS-style features under `FinanceTracker.Application/Features/...`, DTOs, and application services.
- **`FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj`** — EF Core + SQL Server provider; persistence (`FinanceTracker.Infrastructure/Persistence/FinanceTrackerContext.cs`), entity configurations, repositories, and `FinanceTracker.Infrastructure/Migrations/`.
- **Domain (active):** `Finance.Tracker.Domain/FinanceTracker.Domain.csproj` — zero third-party packages; entities such as `FinanceTracker.Domain.Entities.Category` live here and are referenced by Application and Infrastructure.
- **Domain (inactive in build graph):** `FinanceTracker.Domain/` contains alternate/legacy entity files and **is not referenced** by the solution’s `.csproj` files; the live domain is **`Finance.Tracker.Domain/`** only.

## Frameworks and libraries (NuGet)

| Area | Package | Where used |
|------|---------|------------|
| Web API | `Microsoft.NET.Sdk.Web` | `FinanceTracker/FinanceTracker.API.csproj` |
| API versioning | `Asp.Versioning.Mvc` @ 8.1.1, `Asp.Versioning.Mvc.ApiExplorer` @ 8.1.1 | `FinanceTracker/Program.cs` (`AddApiVersioning` / `AddMvc` / `AddApiExplorer`) |
| Mediator | `MediatR` @ 14.0.0 | API + Application; registration via `typeof(GetCategoriesQuery).Assembly` in `FinanceTracker/Program.cs` |
| ORM | `Microsoft.EntityFrameworkCore` @ 8.0.0, `Microsoft.EntityFrameworkCore.SqlServer` @ 8.0.0 | API + Infrastructure; `UseSqlServer` in `Program.cs`, `FinanceTrackerContext` in Infrastructure |
| EF tooling | `Microsoft.EntityFrameworkCore.Design`, `Microsoft.EntityFrameworkCore.Tools` (PrivateAssets) | API + Infrastructure — migrations and design-time |
| OpenAPI | `Swashbuckle.AspNetCore` @ 6.6.2 | `FinanceTracker/Program.cs` — `AddEndpointsApiExplorer`, `AddSwaggerGen`, dev-only `UseSwagger` / `UseSwaggerUI` |
| Testing | `xunit`, `FluentAssertions`, `Moq`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.EntityFrameworkCore.InMemory`, `coverlet.collector` | `FinanceTracker.Tests/FinanceTracker.Tests.csproj` |

## Configuration files (concrete paths)

- **`FinanceTracker/appsettings.json`** — `Logging`, `AllowedHosts`, `ConnectionStrings:FinanceTrackerDB` (SQL Server; **should be overridden via User Secrets or environment in real deployments**).
- **`FinanceTracker/appsettings.Development.json`** — development logging overrides only.
- **`FinanceTracker/Properties/launchSettings.json`** — Kestrel URLs (`http://localhost:5185`, `https://localhost:7203`), `ASPNETCORE_ENVIRONMENT`, Swagger as `launchUrl`, IIS Express profile.
- **`FinanceTracker/FinanceTracker.http`** — sample HTTP file (e.g. `weatherforecast` placeholder host `http://localhost:5185`).

## Dependency injection (host assembly)

`FinanceTracker/Program.cs` registers:

- `FinanceTrackerContext` with `UseSqlServer` and `GetConnectionString("FinanceTrackerDB")`.
- Scoped pairs: `ICategoryRepository` / `CategoryRepository`, `ICategoryService` / `CategoryService`, `IFrequencyRepository` / `FrequencyRepository`, `IFrequencyService` / `FrequencyService`, `ITransactionRepository` / `TransactionRepository`, `ITransactionService` / `TransactionService`.
- `AddControllers`, API versioning (URL segment + query `api-version` + header `x-api-version`), `AddMediatR`, Swagger.

Pipeline: Development → Swagger/Swagger UI; always `UseHttpsRedirection`, `UseAuthorization` (no authentication services registered), `MapControllers`.

## API surface conventions

- Controllers use **versioned routes**, e.g. `FinanceTracker/Controllers/CategoriesV1Controller.cs` — `[Route("api/v{version:apiVersion}/categories")]` with `[ApiVersion("1.0")]`.
- Similar patterns in `FinanceTracker/Controllers/TransactionsV1Controller.cs` and `FinanceTracker/Controllers/RecurringOptionsV1Controller.cs`.

## Persistence model

- `FinanceTracker.Infrastructure/Persistence/FinanceTrackerContext.cs` exposes `DbSet<Category>`, `DbSet<Frequency>`, `DbSet<Transaction>` and applies configurations via `modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly())`.

## Test stack

- **Integration tests:** `FinanceTracker.Tests/Integration/FinanceTrackerWebApplicationFactory.cs` swaps SQL Server for **`UseInMemoryDatabase`** under environment `Testing` while keeping the same `FinanceTrackerContext` type.
- Example tests: `FinanceTracker.Tests/Integration/CategoriesApiIntegrationTests.cs`, `TransactionsApiIntegrationTests.cs` with shared JSON options in `HttpJsonOptions.cs`.
