# Phase 4: Add Background Service to Generate Transaction Instances — Research

**Researched:** 2026-04-27
**Domain:** .NET 8 Console App — Batch Transaction Generation, EF Core, DI/Configuration
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** Standalone console application project (`FinanceTracker.Worker`) — NOT an in-process `BackgroundService`. API does not need to be running.
- **D-02:** Scheduling via Windows Task Scheduler. App runs, does work, exits. No internal polling loop.
- **D-03:** Run interval defaults to every 24 hours but is configurable. Task Scheduler controls the actual trigger.
- **D-04:** Full catch-up on every run — loop from `NextOccurrenceDate` until `NextOccurrenceDate > now`, generating a `Transaction` per iteration.
- **D-05:** Only `Active` templates processed. `Paused` and `Cancelled` skipped entirely.
- **D-06:** Filter: `Status == Active AND NextOccurrenceDate <= DateTime.UtcNow`.
- **D-07:** `TransactionDate` = template's `NextOccurrenceDate` at generation time (not wall-clock run time).
- **D-08:** `Name` = `template.Name`.
- **D-09:** `Amount` = `template.DefaultAmount`.
- **D-10:** `CategoryId` = `template.CategoryId`.
- **D-11:** `RecurringTransactionId` = `template.Id`.
- **D-12:** `CreatedBy` = `template.CreatedBy`.
- **D-13:** `EndDate` is generation boundary only. When `NextOccurrenceDate > EndDate`, stop generating — no `Status` side-effects, no advancement of `NextOccurrenceDate`.
- **D-14:** Advance `NextOccurrenceDate` via `RecurrenceCalculator.NextOccurrence(type, intervalDays, currentNextOccurrenceDate, template.StartDate)` after each generated instance.
- **D-15:** Per-template error isolation — catch exceptions, log, skip to next template. One failure does not abort the batch.

### Claude's Discretion
- DI setup inside the console app (`IServiceCollection`, `IConfiguration`, `appsettings.json`)
- Whether to introduce `IRecurringTransactionRepository` or use `FinanceTrackerContext` directly in a scoped service
- Transaction-per-template vs. transaction-per-batch `SaveChanges` strategy
- Logging implementation (`ILogger` via `Microsoft.Extensions.Logging` or simple `Console.WriteLine`)

### Deferred Ideas (OUT OF SCOPE)
- Cloud hosting migration (Azure Functions, AWS EventBridge)
- Per-user scheduling
- `LastError` field on template
</user_constraints>

---

## Summary

Phase 4 creates `FinanceTracker.Worker` — a .NET 8 console application that queries `Active` recurring transaction templates with `NextOccurrenceDate <= now`, performs catch-up generation of all missed `Transaction` instances, and advances `NextOccurrenceDate` on each template. It is triggered externally by Windows Task Scheduler: the process starts, runs to completion, and exits.

The project uses `Microsoft.Extensions.Hosting` (Host.CreateDefaultBuilder) for DI and configuration, shares `FinanceTrackerContext` and `FinanceTracker.Infrastructure` with the API, and introduces `IRecurringTransactionRepository` for the query layer. The generation service uses the shared `DbContext` directly for batch writes to achieve per-template `SaveChanges` semantics required by D-15 error isolation.

The solution file (`FinanceTracker/FinanceTracker.API.sln`) requires a new project entry. The Worker project's own `appsettings.json` carries the same connection string as the API.

**Primary recommendation:** New console project with `Microsoft.Extensions.Hosting` + `FinanceTrackerContext` injected directly into the generation service for clean batch semantics. Introduce `IRecurringTransactionRepository` for the query-only portion (follows project patterns, keeps the service testable).

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `Microsoft.Extensions.Hosting` | 8.0.0 | `Host.CreateDefaultBuilder` — DI, configuration, logging | Standard for .NET 8 console apps needing DI/config |
| `Microsoft.EntityFrameworkCore.SqlServer` | 8.0.0 | DB access via `FinanceTrackerContext` | Already used throughout; same version as rest of solution |
| `Microsoft.Extensions.Logging` | 8.0.0 | `ILogger<T>` structured logging | Included transitively with `Hosting`; project standard |

> All three packages are already pinned at `8.0.0` in the solution. The worker project references `FinanceTracker.Infrastructure` which already contains EF Core. `Microsoft.Extensions.Hosting` is the only direct new package dependency.

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.Extensions.Configuration.Json` | 8.0.0 | Load `appsettings.json` in console app | Included transitively via `Hosting` — no explicit reference needed |

### Not Needed

- `MediatR` — no command/query bus required in a batch processor
- `Asp.Versioning` — no HTTP endpoint
- `Swashbuckle` — no Swagger

### Installation

```xml
<!-- FinanceTracker.Worker.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\FinanceTracker.Infrastructure\FinanceTracker.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

---

## Architecture Patterns

### Recommended Project Structure

```
FinanceTracker.Worker/
├── FinanceTracker.Worker.csproj
├── appsettings.json                     # connection string (same as API)
├── Program.cs                           # host setup, DI, resolve + run service
└── Services/
    └── TransactionGenerationService.cs  # core generation logic

FinanceTracker.Infrastructure/
├── Persistence/
│   ├── IRecurringTransactionRepository.cs   # NEW — query interface
│   └── RecurringTransactionRepository.cs    # NEW — EF Core implementation
```

### Pattern 1: Host.CreateDefaultBuilder for Console DI

```csharp
// Program.cs
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddDbContext<FinanceTrackerContext>(options =>
            options.UseSqlServer(context.Configuration.GetConnectionString("FinanceTrackerDB")));
        services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();
        services.AddScoped<TransactionGenerationService>();
    })
    .Build();

using var scope = host.Services.CreateScope();
var generator = scope.ServiceProvider.GetRequiredService<TransactionGenerationService>();
await generator.RunAsync();
```

`Host.CreateDefaultBuilder` automatically loads `appsettings.json` and `appsettings.{Environment}.json`, sets up console logging, and wires `IConfiguration`. No manual JSON reading required.

### Pattern 2: Per-Template SaveChanges (Scoped DbContext)

**Rationale:** D-15 requires per-template error isolation. The existing `TransactionRepository.AddAsync` calls `SaveChangesAsync()` after every single instance — this would commit partial state mid-catch-up. The generation service instead injects `FinanceTrackerContext` directly and calls `SaveChangesAsync()` once after the full catch-up loop for each template completes.

```csharp
// TransactionGenerationService.cs
public class TransactionGenerationService
{
    private readonly FinanceTrackerContext _context;
    private readonly IRecurringTransactionRepository _recurringRepo;
    private readonly ILogger<TransactionGenerationService> _logger;

    public async Task RunAsync()
    {
        var now = DateTime.UtcNow;
        var templates = await _recurringRepo.GetActiveOverdueAsync(now);

        _logger.LogInformation("Found {Count} active overdue templates", templates.Count);

        foreach (var template in templates)
        {
            try
            {
                await GenerateForTemplateAsync(template, now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate transactions for template {Id}", template.Id);
            }
        }
    }

    private async Task GenerateForTemplateAsync(RecurringTransaction template, DateTime now)
    {
        int count = 0;

        while (template.NextOccurrenceDate <= now)
        {
            // D-13: stop at EndDate boundary — do not advance NextOccurrenceDate past it
            if (template.EndDate.HasValue && template.NextOccurrenceDate > template.EndDate.Value)
                break;

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                Name = template.Name,                          // D-08
                CategoryId = template.CategoryId,              // D-10
                Category = template.Category,
                Amount = template.DefaultAmount,               // D-09
                TransactionDate = template.NextOccurrenceDate, // D-07
                RecurringTransactionId = template.Id,          // D-11
                CreatedBy = template.CreatedBy,                // D-12
                CreatedAt = DateTime.UtcNow,
                Description = string.Empty
            };

            _context.Transactions.Add(transaction);

            // D-14: advance NextOccurrenceDate
            template.NextOccurrenceDate = RecurrenceCalculator.NextOccurrence(
                template.Frequency.Type,
                template.Frequency.IntervalDays,
                template.NextOccurrenceDate,
                template.StartDate);

            count++;
        }

        if (count > 0)
        {
            await _context.SaveChangesAsync(); // commits all instances + updated NextOccurrenceDate
        }

        _logger.LogInformation("Template {Id}: generated {Count} transaction(s)", template.Id, count);
    }
}
```

### Pattern 3: IRecurringTransactionRepository (query only)

```csharp
public interface IRecurringTransactionRepository
{
    Task<List<RecurringTransaction>> GetActiveOverdueAsync(DateTime asOf);
}

public class RecurringTransactionRepository : IRecurringTransactionRepository
{
    private readonly FinanceTrackerContext _context;

    public RecurringTransactionRepository(FinanceTrackerContext context)
        => _context = context;

    public Task<List<RecurringTransaction>> GetActiveOverdueAsync(DateTime asOf)
        => _context.RecurringTransactions
            .Include(r => r.Frequency)
            .Include(r => r.Category)    // needed for Category nav on generated Transaction
            .Where(r => r.Status == RecurringTransactionStatus.Active
                     && r.NextOccurrenceDate <= asOf)
            .ToListAsync();
}
```

**Critical:** Do NOT use `.AsNoTracking()` here. The generation service mutates `template.NextOccurrenceDate` and relies on EF change tracking to persist the update in `SaveChangesAsync()`.

### Pattern 4: Solution File Registration

The `.sln` file is at `FinanceTracker/FinanceTracker.API.sln`. New project entry required:

```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "FinanceTracker.Worker", "..\FinanceTracker.Worker\FinanceTracker.Worker.csproj", "{NEW-GUID}"
EndProject
```

Plus corresponding `GlobalSection(ProjectConfigurationPlatforms)` entries for Debug/Release.

### Anti-Patterns to Avoid

- **Using `AsNoTracking()` in `GetActiveOverdueAsync`:** Mutations to `NextOccurrenceDate` won't be tracked; `SaveChangesAsync` won't persist template updates.
- **Calling `TransactionRepository.AddAsync` per instance:** It calls `SaveChangesAsync` immediately — breaks catch-up atomicity. Use `_context.Transactions.Add(tx)` directly.
- **Single batch-level `SaveChangesAsync`:** If any template fails, a single save means you either save nothing or can't isolate failures. Save per-template per D-15.
- **Not eager-loading `Frequency`:** `RecurrenceCalculator.NextOccurrence` needs `template.Frequency.Type` and `template.Frequency.IntervalDays`. Lazy loading is not configured; a missing `.Include(r => r.Frequency)` will produce `NullReferenceException`.
- **Using `Microsoft.NET.Sdk.Worker`:** That SDK is for persistent Windows Services / hosted workers. The decided pattern (D-01/D-02) is a run-and-exit console app — use `Microsoft.NET.Sdk` with `OutputType=Exe`.
- **Checking `EndDate` after advancing `NextOccurrenceDate`:** The boundary check must occur BEFORE generation for the current date value, not after advancement.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Host/DI/Config wiring | Manual `IServiceCollection` bootstrap | `Host.CreateDefaultBuilder` | Handles env-specific config, logging providers, disposal |
| Next occurrence calculation | Custom date math | `RecurrenceCalculator.NextOccurrence` | Already implemented + tested with 37 passing tests; snap-back anchoring handles month-end edge cases |
| EF tracked entity mutation | Manual `UPDATE` SQL | EF change tracking + `SaveChangesAsync` | Cleaner, consistent with project patterns |

---

## Common Pitfalls

### Pitfall 1: Missing Frequency Eager-Load Causes NullReferenceException
**What goes wrong:** `RecurrenceCalculator.NextOccurrence` accesses `template.Frequency.Type` and `template.Frequency.IntervalDays`. Without `.Include(r => r.Frequency)` in the query, the navigation property is `null` at runtime.
**Why it happens:** EF Core 8 does not use lazy loading by default; navigation properties are only populated when explicitly loaded.
**How to avoid:** Always `.Include(r => r.Frequency)` in `GetActiveOverdueAsync`.
**Warning signs:** `NullReferenceException` thrown inside the catch block with template Id logged; all templates "fail" with the same error.

### Pitfall 2: AsNoTracking Silently Drops NextOccurrenceDate Updates
**What goes wrong:** Templates are loaded with `.AsNoTracking()`, `NextOccurrenceDate` is mutated in memory, `SaveChangesAsync()` is called — but EF has no change record so the DB update is silently skipped. The worker re-processes the same templates on every run, generating duplicate transactions.
**Why it happens:** `AsNoTracking()` detaches entities from the change tracker. Mutations are invisible to EF.
**How to avoid:** Do not use `AsNoTracking` in `GetActiveOverdueAsync`.
**Warning signs:** Transaction count grows rapidly on each run; `NextOccurrenceDate` never advances in the DB.

### Pitfall 3: EndDate Check After Advancement
**What goes wrong:** Worker advances `NextOccurrenceDate` first, then checks `> EndDate`. The final occurrence (equal to or before EndDate) is generated, NextOccurrenceDate is advanced past EndDate, and the check fires. But now `NextOccurrenceDate` is persisted past EndDate — the outer loop condition `NextOccurrenceDate <= now` may re-trigger this template on future runs if the advanced value is still `<= now`.
**Why it happens:** Off-by-one in loop ordering.
**How to avoid:** Check `template.EndDate.HasValue && template.NextOccurrenceDate > template.EndDate.Value` at the TOP of the while loop body, before generating or advancing.

### Pitfall 4: Connection String Not Present in Worker's appsettings.json
**What goes wrong:** Worker builds successfully but `UseSqlServer(connectionString)` receives `null`; EF throws at first DB call.
**Why it happens:** Worker has its own `appsettings.json`; it doesn't inherit from the API project's config.
**How to avoid:** Create `FinanceTracker.Worker/appsettings.json` with the same `ConnectionStrings:FinanceTrackerDB` key.

### Pitfall 5: Category Not Eager-Loaded — Transaction Insert Fails
**What goes wrong:** `new Transaction { Category = template.Category }` when `template.Category` is null (not eager-loaded) may cause issues in some EF configurations.
**Why it happens:** `Transaction.Category` is `required` in the entity definition; setting it to null can violate EF's required nav property validation.
**How to avoid:** `.Include(r => r.Category)` in `GetActiveOverdueAsync`, OR omit the nav property and set only `CategoryId` (valid since FK is sufficient for insert). Recommended: set `CategoryId` only, leave `Category` nav unset on the new `Transaction`.

---

## Code Examples

### Full Worker Program.cs

```csharp
// FinanceTracker.Worker/Program.cs
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddDbContext<FinanceTrackerContext>(options =>
            options.UseSqlServer(
                context.Configuration.GetConnectionString("FinanceTrackerDB")));

        services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();
        services.AddScoped<TransactionGenerationService>();
    })
    .Build();

using var scope = host.Services.CreateScope();
var generator = scope.ServiceProvider.GetRequiredService<TransactionGenerationService>();
await generator.RunAsync();
```

### Worker appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "FinanceTrackerDB": "Data Source=MichaelDG\\SQLEXPRESS03;Initial Catalog=FinanceTrackerDB;Persist Security Info=False;User ID=master;Password=control_123;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=True;Connection Timeout=120;"
  }
}
```

### IRecurringTransactionRepository

```csharp
// FinanceTracker.Infrastructure/Persistence/IRecurringTransactionRepository.cs
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Infrastructure.Persistence;

public interface IRecurringTransactionRepository
{
    Task<List<RecurringTransaction>> GetActiveOverdueAsync(DateTime asOf);
}
```

### RecurringTransactionRepository

```csharp
// FinanceTracker.Infrastructure/Persistence/RecurringTransactionRepository.cs
using FinanceTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence;

public class RecurringTransactionRepository : IRecurringTransactionRepository
{
    private readonly FinanceTrackerContext _context;

    public RecurringTransactionRepository(FinanceTrackerContext context)
        => _context = context;

    public Task<List<RecurringTransaction>> GetActiveOverdueAsync(DateTime asOf)
        => _context.RecurringTransactions
            .Include(r => r.Frequency)
            .Include(r => r.Category)
            .Where(r => r.Status == RecurringTransactionStatus.Active
                     && r.NextOccurrenceDate <= asOf)
            .ToListAsync();
}
```

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xUnit 2.9.2 + FluentAssertions 6.12.2 + Moq 4.20.72 |
| Config file | `FinanceTracker.Tests/FinanceTracker.Tests.csproj` (existing) |
| Quick run command | `dotnet test FinanceTracker.Tests/FinanceTracker.Tests.csproj --filter "FullyQualifiedName~TransactionGeneration"` |
| Full suite command | `dotnet test FinanceTracker.Tests/FinanceTracker.Tests.csproj` |

### Phase Requirements → Test Map

| Decision | Behavior | Test Type | File |
|----------|----------|-----------|------|
| D-04 | Multiple missed occurrences → multiple transactions generated | Unit (InMemory EF) | `TransactionGenerationServiceTests.cs` ❌ Wave 0 |
| D-05/D-06 | Paused/Cancelled/Future templates → skipped | Unit (InMemory EF) | `TransactionGenerationServiceTests.cs` ❌ Wave 0 |
| D-07..D-12 | Transaction fields mapped correctly from template | Unit (InMemory EF) | `TransactionGenerationServiceTests.cs` ❌ Wave 0 |
| D-13 | EndDate boundary stops generation, no status change | Unit (InMemory EF) | `TransactionGenerationServiceTests.cs` ❌ Wave 0 |
| D-14 | NextOccurrenceDate advanced via RecurrenceCalculator | Unit (InMemory EF) | `TransactionGenerationServiceTests.cs` ❌ Wave 0 |
| D-15 | Exception on one template → others continue | Unit (mocked context or InMemory) | `TransactionGenerationServiceTests.cs` ❌ Wave 0 |

**Recommended test approach:** Use `UseInMemoryDatabase` (already a dependency in `FinanceTracker.Tests.csproj`) to seed templates and verify generated transactions. This follows the exact pattern in `RecurringTransactionDomainModelTests.cs`. No new test infrastructure package needed.

> Note: `Transaction.Category` is declared `required` in the entity. When seeding test data, always include a valid `Category` entity and set `Category = ...` on the `Transaction` object or configure InMemory DB to not enforce required relationships.

### Sampling Rate
- **Per task commit:** `dotnet test --filter "FullyQualifiedName~TransactionGeneration"`
- **Per wave merge:** `dotnet test FinanceTracker.Tests/FinanceTracker.Tests.csproj`
- **Phase gate:** Full suite green (37 existing + new tests) before `/gsd-verify-work`

### Wave 0 Gaps
- [ ] `FinanceTracker.Tests/Worker/TransactionGenerationServiceTests.cs` — covers D-04, D-05/D-06, D-07..D-12, D-13, D-14, D-15

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | Build `FinanceTracker.Worker` | ✓ | 9.0.311 (targets net8.0) | — |
| SQL Server Express | `FinanceTrackerDB` connection | ✓ | Instance: `MichaelDG\SQLEXPRESS03` | — |
| Windows Task Scheduler | External trigger (D-02) | ✓ | Built into Windows 10 | — |

**No missing blocking dependencies.** The environment is fully ready for this phase.

---

## Open Questions

1. **`Transaction.Category` required nav property on insert**
   - What we know: `Transaction.Category` is declared `required` in the entity; `TransactionRepository` sets it in EF round-trip tests
   - What's unclear: Whether EF Core 8 enforces this at `Add()` time or only validates via data annotations at the API layer
   - Recommendation: Set `CategoryId` only on new `Transaction` objects (FK insert is sufficient); EF Core does not validate `required` navigation properties at the `Add()` call — only at model build time for `required` reference properties in Fluent API, not data annotations. Verify at implementation time.

2. **Solution GUID for Worker project**
   - What we know: `.sln` uses hard-coded GUIDs; a new project needs a unique one
   - What's unclear: Whether to generate with `dotnet sln add` (auto-generates GUID) or hand-edit
   - Recommendation: Use `dotnet sln FinanceTracker/FinanceTracker.API.sln add FinanceTracker.Worker/FinanceTracker.Worker.csproj` which handles GUID generation and section entries automatically.

---

## Sources

### Primary (HIGH confidence)
- Codebase inspection: `FinanceTracker.Domain/Entities/RecurringTransaction.cs`, `Transaction.cs`, `Frequency.cs`
- Codebase inspection: `FinanceTracker.Domain/Services/RecurrenceCalculator.cs`
- Codebase inspection: `FinanceTracker.Infrastructure/Persistence/FinanceTrackerContext.cs`, `TransactionRepository.cs`
- Codebase inspection: `FinanceTracker/Program.cs` — existing DI pattern
- Codebase inspection: `FinanceTracker.Tests/Domain/RecurringTransactionDomainModelTests.cs` — existing test pattern
- Codebase inspection: `.planning/phases/04-.../04-CONTEXT.md` — locked decisions

### Secondary (MEDIUM confidence)
- `Microsoft.Extensions.Hosting` 8.0.0 — `Host.CreateDefaultBuilder` API (consistent with .NET 8 documentation patterns; version 8.0.0 matches solution-wide EF Core pin)
- `dotnet sln add` command for solution file registration — standard .NET CLI

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages already in solution; only `Microsoft.Extensions.Hosting` is new
- Architecture: HIGH — patterns derived directly from existing codebase (`Program.cs`, repository pattern, test setup)
- Pitfalls: HIGH — derived from EF Core behavior and existing code analysis (AsNoTracking, required nav properties)
- Generation algorithm: HIGH — derived directly from locked decisions D-04..D-15

**Research date:** 2026-04-27
**Valid until:** 2026-05-27 (stable stack — .NET 8 LTS)
