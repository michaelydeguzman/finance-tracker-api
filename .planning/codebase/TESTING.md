# Testing — `FinanceTracker.Tests`

Test project: `FinanceTracker.Tests/FinanceTracker.Tests.csproj` targets **net8.0**, references the API (`FinanceTracker/FinanceTracker.API.csproj`) and application layer (`FinanceTracker.Application/FinanceTracker.Application.csproj`).

## Framework and libraries

- **xUnit** (`xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`) — primary test framework.
- **FluentAssertions** for expressive assertions (for example integration tests in `FinanceTracker.Tests/Integration/TransactionsApiIntegrationTests.cs`).
- **Moq** for mocking (`ISender`, `ITransactionService`, etc.) — see `FinanceTracker.Tests/Unit/Controllers/TransactionsV1ControllerTests.cs` and `FinanceTracker.Tests/Unit/Handlers/CreateTransactionCommandHandlerTests.cs`.
- **Microsoft.AspNetCore.Mvc.Testing** for `WebApplicationFactory<Program>` — `FinanceTracker.Tests/Integration/FinanceTrackerWebApplicationFactory.cs`.
- **Microsoft.EntityFrameworkCore.InMemory** — integration DB substitute registered in the factory.
- **coverlet.collector** — coverage collection package (runner integration; no custom coverlet config found in-repo).

Global usings: `FinanceTracker.Tests/GlobalUsings.cs` imports `Xunit`.

## Layout

- **`FinanceTracker.Tests/Integration/`** — full-stack HTTP tests against the running host with swapped EF provider:
  - `FinanceTrackerWebApplicationFactory.cs`: `WebApplicationFactory<Program>`, environment `Testing`, replaces `FinanceTrackerContext` / `DbContextOptions` with **InMemory** and a unique database name per factory instance.
  - `TransactionsApiIntegrationTests.cs`, `CategoriesApiIntegrationTests.cs` — API workflows (`IClassFixture<FinanceTrackerWebApplicationFactory>`).
  - `HttpJsonOptions.cs` — shared JSON serializer options for API contract consistency in tests.
- **`FinanceTracker.Tests/Unit/Handlers/`** — MediatR handler tests with services mocked (`MockBehavior.Strict` in sampled handler tests).
- **`FinanceTracker.Tests/Unit/Controllers/`** — controller tests with MediatR `ISender` mocked; documents “thin controller” pattern in XML summary on `TransactionsV1ControllerTests`.

## Patterns

- **Integration**: `CreateClient()`, optional per-test DB reset via `EnsureDeleted`/`EnsureCreated` on `FinanceTrackerContext` from the factory’s `IServiceProvider` (`TransactionsApiIntegrationTests`).
- **Unit**: build `sut` with mocked dependencies, `await sut.Method(...)`, `Verify` Moq setups for side effects and call counts.
- **JSON**: `PostAsJsonAsync` / `ReadFromJsonAsync` with `HttpJsonOptions.ForApi` to align with API serialization settings.

## CI / automation

- No `.github/workflows/` or other YAML CI definitions were found under this repository root at analysis time. Tests are run locally or via IDE/`dotnet test` on the solution (`FinanceTracker/FinanceTracker.API.sln`).

## Quick reference paths

- Project file: `FinanceTracker.Tests/FinanceTracker.Tests.csproj`
- Factory: `FinanceTracker.Tests/Integration/FinanceTrackerWebApplicationFactory.cs`
- Sample integration test: `FinanceTracker.Tests/Integration/TransactionsApiIntegrationTests.cs`
- Sample unit (handler): `FinanceTracker.Tests/Unit/Handlers/CreateTransactionCommandHandlerTests.cs`
- Sample unit (controller): `FinanceTracker.Tests/Unit/Controllers/TransactionsV1ControllerTests.cs`
