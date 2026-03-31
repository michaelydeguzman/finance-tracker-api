# Codebase concerns — technical debt and risks

Scope: `D:\PersonalProjects\finance-tracker-api` (ASP.NET Core 8, EF Core, MediatR).

## Security

- **Committed database credentials** — `FinanceTracker/appsettings.json` embeds a full SQL Server connection string (host instance, user, password). This is a secret leak and supply-chain/history exposure risk; rotate credentials and move to user secrets, environment variables, or a vault. Never commit production credentials.
- **TrustServerCertificate=true** — Weakens TLS validation for SQL; acceptable only for tightly controlled local dev; risky for shared or production targets.
- **No authentication** — `Program.cs` calls `UseAuthorization()` with no authentication registration (`AddAuthentication` / JWT / cookies). All APIs are effectively anonymous.
- **Client-trusted audit field** — `CreateTransactionDto.CreatedBy` is required from the body with no server identity binding; trivial to spoof.
- **AllowedHosts: "*"`** in `FinanceTracker/appsettings.json` — Host header not constrained; review for deployment hardening.

## Performance

- **In-memory filtering after full table read** — `FinanceTracker.Application/Services/TransactionService.cs` `GetByCategoryType` loads all categories of a type, then `GetAllAsync` loads **all** transactions and filters in process. This does not scale; should be a single query with `Where` on navigations or joins in `FinanceTracker.Infrastructure/Persistence/TransactionRepository.cs`.
- **List endpoints always include navigation graphs** — `GetAllAsync` always `Include`s Category and Frequency; large payloads and heavier SQL for list views that may not need both.

## Architecture and duplication

- **Two domain folders / one active** — Active project: `Finance.Tracker.Domain/` (assembly name `FinanceTracker.Domain`). A second tree `FinanceTracker.Domain/` is **not** referenced by Application or Infrastructure and **fails to build** (`Entities/Category.cs` assigns `IsIncome` which is not defined; overlapping `Category` models in `Entities/` vs `Categories/`). Same logical namespace `FinanceTracker.Domain.Entities` appears in different folders, which invites copy-paste drift and onboarding confusion.
- **Application → Infrastructure dependency** — `FinanceTracker.Application/FinanceTracker.Application.csproj` references Infrastructure, coupling application use cases to persistence. Typical clean architecture would invert this (interfaces in Application, implementations in Infrastructure only).
- **MediatR as pass-through** — Handlers delegate to `*Service` types that mirror repositories; thin value unless you plan cross-cutting in pipelines—fine for now but adds ceremony without clear boundaries yet.
- **Anemic domain with persistence attributes** — Entities in `Finance.Tracker.Domain/Entities` use `[Key]`, `[Required]`, etc. Domain is coupled to EF/data annotations instead of pure models with mapping in Infrastructure.
- **Namespace drift in API** — Most controllers use `FinanceTracker.Controllers`; `FinanceTracker/Controllers/DashboardV1Controller.cs` uses `FinanceTracker.API.Controllers`. Empty stub controller is dead surface area.

## Fragile / operational

- **Solution path assumptions** — `FinanceTracker/FinanceTracker.API.sln` references projects with `..\` paths; builds are expected from the `FinanceTracker/` directory—CI and `dotnet build` from repo root need an explicit sln path or a root-level solution.
- **EF/package version skew** — API/Infrastructure use EF Core `8.0.0`; `FinanceTracker.Tests` uses `8.0.11` / `Microsoft.AspNetCore.Mvc.Testing` `8.0.11`. Minor mismatch can cause subtle test vs runtime behavior differences.

## Hygiene

- **No TODO/FIXME markers** in tracked `.cs` files (good), but the orphaned `FinanceTracker.Domain` project is implicit debt without comments.
- **`null!` on navigation stubs** — Used when constructing `Transaction` without `Category`; safe if EF never materializes invalid graphs, but it hides incomplete invariants at compile time.

## Recommendations (priority sketch)

1. Remove secrets from git history and config; use secret management; rotate DB password.
2. Add authentication and derive audit fields from claims, not request body.
3. Delete or fix `FinanceTracker.Domain/` (merge into `Finance.Tracker.Domain` or remove) so the repo has a single coherent domain.
4. Push category filtering into the repository with proper SQL.
5. Normalize controller namespaces and implement or remove `DashboardV1Controller`.
