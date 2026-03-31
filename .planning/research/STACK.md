# Technology Stack Research — Finance Tracker API (.NET 8)

**Project:** Finance Tracker API  
**Scope:** stack/patterns for a .NET 8 REST API (filtering, pagination, validation, auth, observability, OpenAPI)  
**Researched:** 2026-03-31  
**Overall confidence:** **MEDIUM** (high for MS/OpenTelemetry guidance; medium for “best” library picks like Sieve/Serilog that are ecosystem-common but optional)

## Recommended stack (2026-ready, .NET 8-compatible)

### Core framework + API surface
| Category | Recommendation | Why | Notes |
|---|---|---|---|
| Runtime | **.NET 8 / ASP.NET Core** | LTS, stable ecosystem | You already target `net8.0`. |
| API style | **Controllers** (keep) | Fits current codebase + ApiVersioning + Swashbuckle | Minimal APIs are optional; no need to rewrite. |
| API versioning | **Asp.Versioning.Mvc** + **ApiExplorer** | Industry-standard versioning for ASP.NET Core | Repo already uses `Asp.Versioning.Mvc` + `Asp.Versioning.Mvc.ApiExplorer`. Source: `https://github.com/dotnet/aspnet-api-versioning`. |
| OpenAPI | **Swashbuckle.AspNetCore** (keep) | Widely used for Swagger UI + schema generation | MS now ships a built-in OpenAPI feature set too; see “Alternatives” below. Source: `https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-8.0`. |

### Authentication & authorization
| Scenario | Recommendation | Why | Notes / Pattern |
|---|---|---|---|
| Dev/local tokens | **`dotnet user-jwts` + JwtBearer** | Quick, safe local testing without standing up an IdP | MS guidance: `dotnet user-jwts` integrates with JwtBearer. Source: `https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn?view=aspnetcore-8.0`. |
| Production (typical) | **External OIDC provider + JwtBearer** | Avoid running your own auth server unless required | Use policy-based authorization for claims/roles. Source: `https://learn.microsoft.com/en-us/aspnet/core/security/authorization/introduction?view=aspnetcore-8.0`. |
| “We must self-host auth” | **OpenIddict** | Mature OSS option for OIDC server/client/validation in .NET | Official docs: `https://documentation.openiddict.com/`. |

**Auth patterns to standardize:**
- **Policies over roles** for most checks (claims-based, evolvable).
- **Explicit authorization scopes/claims** for “write” operations (e.g., `transactions:write`) and allow read-only tokens where useful.
- **ProblemDetails for auth failures** (401/403) so clients get consistent error shapes.

### Validation (DTOs / commands / queries)
| Category | Recommendation | Why | Notes |
|---|---|---|---|
| Validator library | **FluentValidation (core package)** | Ergonomic, strongly-typed validation rules | Source: `https://github.com/FluentValidation/FluentValidation`. |
| ASP.NET Core integration | **Avoid `FluentValidation.AspNetCore` for new work** | Project is **unsupported / not maintained**; also auto-validation has async limitations | Source: `https://github.com/FluentValidation/FluentValidation.AspNetCore` (repo) and current docs/results indicating unsupported status. |
| Where validation runs | **MediatR pipeline behavior** (recommended) | Matches your CQRS/MediatR usage; works for async rules | Pattern: validate request objects (commands/queries) before handler executes; throw a domain/app validation exception that maps to ProblemDetails. |

**Practical pattern for this repo:**
- Keep `[ApiController]` + data annotations for “surface-level” constraints (required fields, formatting) **or** move most rules into FluentValidation for consistency.
- For query-string filters (like `from`, `to`, `page`, `pageSize`), validate in the MediatR query validator so your controller stays thin.

### Filtering, sorting, pagination
You have a concrete requirement: optional paging (1-based `page`, capped `pageSize <= 20`), deterministic ordering, `totalCount` only when paging is used, and backward compatibility when paging params are omitted.

| Need | Recommendation | Why | Notes |
|---|---|---|---|
| “Simple, domain-specific filters” | **Hand-rolled filter DTO + explicit LINQ** | Best clarity, easiest to keep backwards-compatible semantics | For your case (`categoryType`, `categoryIds[]`, `from/to`), explicit code is usually simpler than a generic query language. |
| “Generic filtering/sorting across many endpoints” | **Sieve** (optional) | Popular lightweight approach for query-string driven filtering/sorting/paging | Source: `https://github.com/Biarity/Sieve`. Use when you want consistent filtering syntax across many resources. |
| Pagination style | **Offset paging now**, consider **keyset paging later** | Offset paging fits current UI needs; keyset avoids deep-page costs | Always include a deterministic order (`TransactionDate desc, Id desc`) before paging. |
| Counting | **`totalCount` only for paged requests** | Keeps unpaged requests cheap; matches your stated contract | Implement as a separate `CountAsync()` over the filtered query before `Skip/Take`. |

**Concrete patterns to standardize:**
- **Request DTO**: `TransactionsListQuery` includes filters + optional paging fields.
- **Deterministic ordering**: always apply order before `Skip/Take` (tie-break by `Id`).
- **Two-phase query**:
  - `filtered = baseQuery.Where(...)`
  - `totalCount = await filtered.CountAsync()` (only when paging requested)
  - `items = await filtered.OrderByDescending(...).ThenByDescending(...).Skip(...).Take(...).ToListAsync()`
- **AsNoTracking** for list queries (default unless you need tracking).

### Data access (EF Core / SQL Server)
| Category | Recommendation | Why | Notes / Pattern |
|---|---|---|---|
| ORM | **EF Core 8 + SQL Server provider** (keep) | Good fit, already in repo | EF Core repo: `https://github.com/dotnet/efcore`. Querying docs: `https://learn.microsoft.com/en-us/ef/core/querying/`. |
| Query performance hygiene | **AsNoTracking + projection + cancellation tokens** | Lower memory/CPU, avoid over-fetching | Standardize list endpoints to project to DTOs in-query where practical. |
| Query composition | **IQueryable boundary discipline** | Prevent accidental client-eval or premature materialization | Keep `IQueryable` in repository/service until all filters applied; materialize once. |
| Paging correctness | **Stable ordering** | Avoid duplicates/missing items across pages | Your contract already calls this out—make it a “must”. |

### Observability (logs, metrics, traces)
| Signal | Recommendation | Why | Sources |
|---|---|---|---|
| Traces + metrics + logs export | **OpenTelemetry SDK + OTLP exporter** | Vendor-neutral; first-class guidance in .NET ecosystem | MS overview + package list: `https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel` and OTel .NET docs: `https://opentelemetry.io/docs/languages/net/`. |
| Instrumentation | **AspNetCore + HttpClient + SqlClient (+ EFCore if used)** | Covers incoming HTTP, outbound calls, DB spans | MS packages list includes these instrumentations. Source: `https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel`. |
| Local dev experience | **Aspire Dashboard (optional)** | Great local trace/log/metric loop without committing to a vendor | Mentioned in MS OTel guidance as a local dashboard option. Source: `https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel`. |
| Structured logging | **Serilog (optional) OR stick to `Microsoft.Extensions.Logging` + OTel logs** | Serilog is widely used; but OTel logs + MEL can be enough | Serilog ASP.NET Core repo: `https://github.com/serilog/serilog-aspnetcore`. MS logging fundamentals: `https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-8.0`. |

**Observability patterns to standardize:**
- **Correlation**: ensure request id / trace id is included in logs (Serilog enrichers or MEL scopes).
- **HTTP request logging**: log status code + elapsed time + route template (avoid logging PII).
- **Exception handling**: global exception handler that returns ProblemDetails and emits structured logs + trace events.

### API error + contract conventions
| Category | Recommendation | Why | Notes |
|---|---|---|---|
| Error shape | **RFC 7807 ProblemDetails everywhere** | Consistent client handling | If not already, standardize validation errors + exception mapping to ProblemDetails. |
| OpenAPI-first discipline | **Keep Swagger accurate** | Prevent client drift | Use schema examples for list envelopes and paging parameters. |

## “Solid defaults” library set (NuGet shortlist)
This is a pragmatic set that tends to age well for REST APIs:

- **API/versioning/OpenAPI**
  - `Asp.Versioning.Mvc`
  - `Asp.Versioning.Mvc.ApiExplorer`
  - `Swashbuckle.AspNetCore` (or MS OpenAPI alternative below)
- **Validation**
  - `FluentValidation`
  - *(Optional)* `FluentValidation.DependencyInjectionExtensions`
- **Observability**
  - `OpenTelemetry`
  - `OpenTelemetry.Exporter.OpenTelemetryProtocol`
  - `OpenTelemetry.Instrumentation.AspNetCore`
  - `OpenTelemetry.Instrumentation.Http`
  - `OpenTelemetry.Instrumentation.SqlClient`
  - *(Optional)* `OpenTelemetry.Instrumentation.EntityFrameworkCore` (if you want EF-specific spans; otherwise SqlClient spans may be enough depending on setup)
- **Logging (optional)**
  - `Serilog.AspNetCore` + a sink (Console/Seq/ApplicationInsights/etc.)
- **Filtering/sorting/paging (optional)**
  - `Sieve`

## Alternatives (when/why you’d choose them)

### OpenAPI: Microsoft OpenAPI vs Swashbuckle
- **Recommendation for this repo:** keep **Swashbuckle** for now (least churn; you already have it).
- **Consider Microsoft OpenAPI** if you want tighter alignment with built-in ASP.NET Core OpenAPI support and less third-party surface area. Source: `https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-8.0`.

### Filtering/paging: explicit code vs Sieve
- **Explicit code** wins when:
  - Filters are domain-specific and few (your current case).
  - You need nuanced backward compatibility semantics (like “use `categoryType` only when `categoryIds` is absent”).
- **Sieve** wins when:
  - You want a consistent generic filtering language across many resources.
  - You’re OK standardizing on its query parameter syntax for the long term.

### Auth server: don’t self-host unless required
- If the app is personal/small-team, the simplest “solid” approach is **JwtBearer with an external IdP** (or local dev tokens with `user-jwts`).
- If you truly need a self-hosted authorization server, **OpenIddict** is the primary OSS candidate to research deeper. Source: `https://documentation.openiddict.com/`.

## Implementation notes tailored to your current architecture
You’re using **MediatR** and already have service/repository layers. The most maintainable pattern here is:

- **Controllers**: only parse query/body → send a MediatR request → return result/envelope.
- **MediatR pipeline behaviors**:
  - **ValidationBehavior** (FluentValidation)
  - *(Optional)* **LoggingBehavior** (structured “command/query started/finished”)
  - *(Optional)* **TransactionBehavior** for write operations (if you centralize unit-of-work)
- **EF Core query shaping** happens in your service/repository layer with explicit LINQ so paging/count semantics are correct and auditable.

## Sources (authoritative first)
- Microsoft Learn — OpenAPI in ASP.NET Core: `https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-8.0`
- Microsoft Learn — JWT auth & `dotnet user-jwts` (updated 2025-08-08): `https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn?view=aspnetcore-8.0`
- Microsoft Learn — Authorization overview: `https://learn.microsoft.com/en-us/aspnet/core/security/authorization/introduction?view=aspnetcore-8.0`
- Microsoft Learn — .NET observability with OpenTelemetry: `https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel`
- OpenTelemetry docs — .NET language docs (modified 2026-01-27): `https://opentelemetry.io/docs/languages/net/`
- Serilog ASP.NET Core integration (official repo): `https://github.com/serilog/serilog-aspnetcore`
- FluentValidation (official repo): `https://github.com/FluentValidation/FluentValidation`
- FluentValidation.AspNetCore (official repo; marked unsupported per maintainer messaging in ecosystem): `https://github.com/FluentValidation/FluentValidation.AspNetCore`
- OpenIddict docs: `https://documentation.openiddict.com/`
- EF Core querying docs: `https://learn.microsoft.com/en-us/ef/core/querying/`
- ASP.NET API Versioning (official repo): `https://github.com/dotnet/aspnet-api-versioning`
- Sieve (filtering/sorting/pagination): `https://github.com/Biarity/Sieve`

