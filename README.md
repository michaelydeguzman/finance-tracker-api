# Finance Tracker API

ASP.NET Core 8 REST API for personal finance tracking — categories, transactions, and
recurring transaction templates that a background worker materializes on schedule. It is
the backend for the sibling [`finance-tracker-ui`](https://github.com/michaelydeguzman/finance-tracker-ui)
repo (Next.js).

Clean Architecture, EF Core against SQL Server, MediatR, URL-segment API versioning, Swagger.

## Projects

| Project | Role |
| --- | --- |
| `FinanceTracker/` | API host — controllers and `Program.cs`. Assembly is `FinanceTracker.API`. |
| `FinanceTracker.Domain/` | Entities, pure domain services, repository interfaces. Depends on nothing. |
| `FinanceTracker.Application/` | DTOs, MediatR commands/queries and their handlers, service interfaces. |
| `FinanceTracker.Infrastructure/` | `FinanceTrackerContext`, entity configurations, repository implementations, EF migrations. |
| `FinanceTracker.Worker/` | Run-and-exit console app that expands due recurring templates. Triggered by Windows Task Scheduler. |
| `FinanceTracker.Tests/` | xunit + FluentAssertions + Moq — unit, integration, and worker tests. |

Dependencies point inward. `Application` and `Infrastructure` are siblings that each reference
only `Domain`, and the API host composes them. **`Application` deliberately does not reference
`Infrastructure`** — it consumes the repository contracts from `Domain/Repositories/`, and
`Program.cs` binds them to the EF implementations.

## Getting started

Requires the .NET 8 SDK and a reachable SQL Server instance.

```bash
dotnet restore FinanceTracker/FinanceTracker.API.sln
dotnet build   FinanceTracker/FinanceTracker.API.sln
dotnet test    FinanceTracker/FinanceTracker.API.sln
```

Secrets live in **`dotnet user-secrets`**, never in `appsettings*.json`. `appsettings.json`
carries an empty `ConnectionStrings:FinanceTrackerDB` placeholder, and
`appsettings.Development.json` is git-tracked with no `ConnectionStrings` block at all — do
not add one.

```bash
dotnet user-secrets set "ConnectionStrings:FinanceTrackerDB" "<your connection string>" --project FinanceTracker/FinanceTracker.API.csproj

# Must match API_BFF_SECRET in the finance-tracker-ui checkout. It guards the SSO
# exchange endpoint, so anything holding it can sign in as anyone.
dotnet user-secrets set "Auth:BffSharedSecret" "<shared secret>" --project FinanceTracker/FinanceTracker.API.csproj
```

Non-secret development settings — JWT lifetimes, password rules, SMTP host — live in
`appsettings.Development.json` and are safe to read there.

Run the API and it serves `https://localhost:7203` and `http://localhost:5185`, with Swagger
at `/swagger`.

> **Visual Studio holds file locks on `bin/Debug/net8.0/*.dll`.** If the API is running under
> the VS debugger, CLI builds fail with `MSB3027` / `MSB3021`. That is a lock, not a code
> error — check whether it is already serving with
> `curl -k https://localhost:7203/api/v1/categories` before rebuilding.

## Data safety

**The local database holds real personal financial records, not seed data, and there is no
backup story.**

- Never run destructive or bulk-update SQL against it.
- *Generating* an EF migration is safe anywhere. **Applying** one
  (`dotnet ef database update`) is a deliberate, local, eyes-on operation — never from a
  cloud or remote session.
- Tests never touch it. Integration tests swap in EF Core InMemory via
  `FinanceTracker.Tests/Integration/FinanceTrackerWebApplicationFactory.cs`, so the whole
  suite runs with no local infrastructure.

`.claude/agents/data-safety-reviewer.md` is a review agent encoding these rules plus the
codebase-specific ways they get violated silently.

## Build configuration

Three root-level files own settings that used to be repeated per project, so a new project
inherits all of it and declares almost nothing itself.

| File | Owns |
| --- | --- |
| `Directory.Build.props` | `TargetFramework`, `Nullable`, `ImplicitUsings` |
| `Directory.Packages.props` | Every package version, via central package management |
| `.editorconfig` | Code style — file-scoped namespaces, `_camelCase` private fields, Allman braces |

**Do not put a `Version` attribute on a `PackageReference`** — with central package management
on that is an error (NU1008). Set the version in `Directory.Packages.props`; the `.csproj`
names the package only.

Style rules are advisory (`EnforceCodeStyleInBuild` is deliberately unset, so nothing in
`.editorconfig` fails a build). Apply them in bulk with:

```bash
dotnet format style FinanceTracker/FinanceTracker.API.sln
```

## CI

`.github/workflows/ci.yml` runs on every pull request and every push to `main`: restore,
build, then test on .NET 8. The suite needs no database, so it runs anywhere.

## Further reading

- **[CLAUDE.md](CLAUDE.md)** — conventions, recurring-transaction internals, and the rules
  that are easy to violate silently. The source of truth for how to work in this repo.
- **[MULTI_TENANCY.md](MULTI_TENANCY.md)** — how the API went from a single-household app
  with no concept of a user to a multi-tenant API that authenticates callers itself, and how
  tenancy is enforced now.
