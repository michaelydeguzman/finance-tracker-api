# External integrations — Finance Tracker API

This inventory lists **out-of-process dependencies and third-party surfaces** implied by code and configuration. The API is intentionally small: **no outbound HTTP clients, webhooks, or cloud SDKs** appear in the codebase.

## Databases

### Microsoft SQL Server (primary datastore)

- **Evidence:** `Microsoft.EntityFrameworkCore.SqlServer` in `FinanceTracker/FinanceTracker.API.csproj` and `FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj`.
- **Wiring:** `FinanceTracker/Program.cs` calls `options.UseSqlServer(builder.Configuration.GetConnectionString("FinanceTrackerDB"))` for `FinanceTracker.Infrastructure.Persistence.FinanceTrackerContext`.
- **Configuration key:** `ConnectionStrings:FinanceTrackerDB` in `FinanceTracker/appsettings.json` points at a named SQL Server instance and database (`FinanceTrackerDB`). **Credentials in committed config are a security risk**; production should use **User Secrets**, **environment variables**, or a secret store—never rely on the checked-in string alone.
- **Schema evolution:** EF Core migrations under `FinanceTracker.Infrastructure/Migrations/` (e.g. `InitialMigration`, `CreateTransactionTable`, `AddTransactionDate`) target SQL Server semantics (`SqlServerModelBuilderExtensions.UseIdentityColumns` in snapshot/designer files).

### EF Core In-Memory provider (tests only)

- **Package:** `Microsoft.EntityFrameworkCore.InMemory` in `FinanceTracker.Tests/FinanceTracker.Tests.csproj`.
- **Usage:** `FinanceTracker.Tests/Integration/FinanceTrackerWebApplicationFactory.cs` removes the registered `DbContextOptions<FinanceTrackerContext>` and `FinanceTrackerContext`, then reRegisters `AddDbContext` with `UseInMemoryDatabase` and a unique database name per factory instance. This is **not** a production integration; it isolates integration tests from a real SQL Server.

## Authentication and authorization

- **ASP.NET Core:** `FinanceTracker/Program.cs` includes `app.UseAuthorization()` but **does not** call `AddAuthentication`, register JWT bearer, OpenID Connect, or ASP.NET Core Identity.
- **Effect:** Endpoints are not protected by an auth handler in the current codebase; any hardening would be additive (not present today).

## External HTTP APIs and webhooks

- **Outbound:** No `HttpClient`, `IHttpClientFactory`, gRPC clients, or third-party REST SDKs were found in application or infrastructure projects.
- **Inbound webhooks:** No webhook controllers or signature-validation middleware.

## Third-party / vendor services

- **None identified** in NuGet references beyond Microsoft ecosystem packages (EF Core, ASP.NET Core testing), MediatR, Asp.Versioning, Swashbuckle, and test libraries (xUnit, Moq, FluentAssertions, Coverlet).

## Local development tooling (not production integrations)

- **Swagger / OpenAPI:** `Swashbuckle.AspNetCore` exposes OpenAPI in **Development** only (`if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }` in `FinanceTracker/Program.cs`). This documents HTTP APIs for developers; it is not an external SaaS dependency.

## HTTP endpoints this API exposes (for orchestration context)

Versioned REST routes under `FinanceTracker/Controllers/` include:

- `api/v{version:apiVersion}/categories` — `CategoriesV1Controller.cs`
- `api/v{version:apiVersion}/transactions` — `TransactionsV1Controller.cs`
- `api/v{version:apiVersion}/recurring-options` — `RecurringOptionsV1Controller.cs`

Clients and integration tests call these over HTTP/S; see `FinanceTracker/Properties/launchSettings.json` for default dev ports and `FinanceTracker.Tests/Integration/*ApiIntegrationTests.cs` for example request paths (e.g. `POST /api/v1/categories`).

## CI/CD and hosting

- No `.github/workflows` or Dockerfile was found in the repository at mapping time; deployment targets (Azure, container registry, etc.) are **outside the current repo artifacts**.

## Summary table

| Integration type | Technology | Role |
|------------------|------------|------|
| Database | SQL Server + EF Core | Primary persistence |
| Database (test) | EF InMemory | Isolated integration tests |
| API docs | Swashbuckle | Dev OpenAPI/Swagger UI |
| Auth providers | — | Not configured |
| External APIs / webhooks | — | Not present |
