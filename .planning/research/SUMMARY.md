# Project Research Summary

**Project:** Finance Tracker API (`finance-tracker-api`)
**Domain:** Personal finance tracking REST API (categories, transactions, recurrence options)
**Researched:** 2026-03-31
**Confidence:** MEDIUM

## Executive Summary

This repository is a **.NET 8** layered Web API for a finance-tracking domain, implemented with **ASP.NET Core MVC controllers**, **MediatR** for a CQRS-flavored request pipeline, and **EF Core + SQL Server** for persistence. The public HTTP surface is versioned (v1) and currently focuses on core CRUD-style operations around **categories**, **transactions**, and **recurring options** (frequencies), with an `ApiResponseDto<T>` response envelope used across controllers.

The recommended approach for roadmap planning is to treat the current codebase as an already-established baseline: stabilize the domain model and API contracts, harden persistence and test coverage, then address cross-cutting production concerns (auth, validation, error handling, secrets, CI/CD). The architecture is clear and testable, but it has a notable pragmatic trade-off: the **Application** project currently depends on **Infrastructure**, and repository interfaces live in Infrastructure rather than being owned by Application/Domain. That’s workable short-term, but it constrains long-term decoupling and evolution.

Primary risks are (1) **security/ops gaps** (no auth configured; connection string management needs care), (2) **layering drift** (Application → Infrastructure reference makes persistence choices “leak” upward), and (3) **feature ambiguity** because the expected feature set and pitfalls were not captured in dedicated research documents. Mitigation is straightforward: lock down configuration/secrets, add authentication/authorization early if this is intended for real users, and decide whether to keep the pragmatic layering or invest in dependency inversion.

## Key Findings

### Recommended Stack

The codebase is already standardized on **C# / .NET 8** with nullable reference types and implicit usings, using a conventional layered solution structure (API host, Application, Infrastructure, Domain, Tests). Persistence is implemented with **EF Core 8** and the **SQL Server** provider; OpenAPI documentation is provided via Swashbuckle in Development environments only.

**Core technologies:**
- **.NET 8 / ASP.NET Core MVC**: API host + routing/controllers — stable LTS-style platform and current repo baseline
- **MediatR**: request/response orchestration — keeps controllers thin and use cases testable
- **EF Core 8 + SQL Server**: persistence + migrations — aligns with existing `FinanceTrackerContext` and migration history
- **Asp.Versioning.Mvc**: versioned API routes — supports evolution without breaking clients
- **xUnit + FluentAssertions + Moq + WebApplicationFactory**: unit and integration testing — already present and used

### Expected Features

No dedicated `.planning/research/FEATURES.md` exists in this workspace. Based on controllers and application features present, the “current expected” capabilities are:

**Must have (table stakes) — inferred from implementation:**
- Category management (create/update/list/get/delete or equivalent) — required for transaction classification
- Transaction management (create/update/list/get/delete or equivalent) — core financial record-keeping
- Recurrence/frequency options (read/list) — supports recurring transaction patterns
- Basic dashboard endpoint placeholder exists but appears incomplete

**Should have (competitive) — not evidenced as implemented today:**
- Authentication/authorization for user data isolation
- Validation standardization (request validation beyond `ModelState`, domain rules)
- Robust error handling (global exception handling, consistent problem details)
- Reporting/analytics endpoints (summaries, trends, spending by category, etc.)

**Defer (v2+) — not evidenced; requires product decisions:**
- Multi-currency, budgeting, goals, bank sync, import/export, advanced recurrence

### Architecture Approach

The solution follows a **layered, CQRS-flavored** architecture:
- Controllers receive HTTP requests on **versioned routes** and delegate to MediatR `ISender`.
- Handlers in `FinanceTracker.Application/Features/...` orchestrate use cases, often through application services.
- Infrastructure implements repositories and EF Core mappings via `FinanceTrackerContext` and configuration classes.

**Major components:**
1. **API Host (`FinanceTracker/`)** — composition root, DI, middleware, controllers, versioning, Swagger
2. **Application (`FinanceTracker.Application/`)** — MediatR features, DTOs, application services orchestrating use cases
3. **Infrastructure (`FinanceTracker.Infrastructure/`)** — EF Core context, migrations, repositories, persistence configuration
4. **Domain (`Finance.Tracker.Domain/`)** — entities (`Category`, `Transaction`, `Frequency`), data annotations present
5. **Tests (`FinanceTracker.Tests/`)** — integration tests with EF InMemory and unit tests for handlers/controllers

### Critical Pitfalls

No dedicated `.planning/research/PITFALLS.md` exists in this workspace. Pitfalls inferred from the current codebase state and planning docs:

1. **Security hardening gap (no auth configured)** — add `AddAuthentication`/`AddAuthorization` and protect endpoints early if this will host real user data
2. **Secrets risk (connection strings in config)** — ensure production uses User Secrets/env vars/secret store; avoid committing credentials
3. **Layering drift (Application depends on Infrastructure)** — decide whether to keep pragmatic coupling or refactor ports (interfaces) into Application/Domain
4. **Duplicate/legacy domain folder confusion** — only `Finance.Tracker.Domain/` is live; avoid adding new types under the inactive `FinanceTracker.Domain/`
5. **Test realism (EF InMemory differs from SQL Server)** — keep InMemory tests, but add SQL Server-based integration tests (container/CI) for query and constraint fidelity when needed

## Implications for Roadmap

Based on the existing implementation and inferred gaps, suggested phase structure:

### Phase 1: Baseline stabilization and hygiene
**Rationale:** Build on current working architecture; remove sources of confusion and reduce operational risk before adding major features.
**Delivers:** Clear domain project usage, consistent conventions, verified endpoints, tightened configuration/secrets handling.
**Addresses:** Category/Transaction/Frequency baseline flows (as-is), removes ambiguity around the inactive domain folder.
**Avoids:** “Two domains” confusion; accidental credential leaks.

### Phase 2: API correctness + persistence fidelity
**Rationale:** EF InMemory is useful but not fully faithful; correctness requires contract + data-layer confidence.
**Delivers:** Expanded integration tests for critical workflows, migration verification, contract checks for versioned routes and `ApiResponseDto<T>`.
**Uses:** EF Core 8 + SQL Server semantics and existing test patterns.
**Implements:** Strengthens handler/service/repository flow under real constraints.

### Phase 3: Cross-cutting production concerns
**Rationale:** Finance data typically needs access control and predictable error semantics.
**Delivers:** Authentication/authorization, standardized validation, global exception handling, and observability basics.
**Addresses:** Inferred “should-have” items: auth, validation, error handling.
**Avoids:** Shipping an unprotected finance API; inconsistent failure modes.

### Phase 4: Architecture tightening (optional, decision point)
**Rationale:** If long-term maintainability matters, reduce coupling by moving repository interfaces out of Infrastructure.
**Delivers:** Dependency inversion (Application/Domain owns ports), Infrastructure implements adapters; clearer boundaries.
**Avoids:** Persistence-driven constraints leaking into use cases.

### Phase Ordering Rationale

- Stabilize and clarify the solution structure before scaling features.
- Raise confidence in correctness (tests + DB fidelity) before productionizing.
- Add cross-cutting concerns (auth/validation/errors) before expanding the feature surface.
- Treat “clean architecture tightening” as optional, justified by expected project lifespan and contributor count.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 3 (Auth/Validation/Error handling):** Requires selecting auth scheme (JWT/OIDC), user/tenant model, and response standardization.
- **Phase 4 (Dependency inversion refactor):** Requires careful refactor plan to avoid breaking DI and handlers.

Phases with standard patterns (skip deeper research; follow established practices):
- **Phase 1 (Hygiene):** Standard .NET solution cleanup and project-structure alignment.
- **Phase 2 (Testing/persistence fidelity):** Standard EF Core + ASP.NET Core testing approaches.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Directly evidenced in `.planning/codebase/STACK.md` and csproj references |
| Features | LOW | No `FEATURES.md`; only inferred from controllers and feature folders |
| Architecture | HIGH | Directly evidenced in `.planning/codebase/ARCHITECTURE.md` and structure docs |
| Pitfalls | LOW | No `PITFALLS.md`; pitfalls inferred from current state and conventions/integrations docs |

**Overall confidence:** MEDIUM

### Gaps to Address

- **Explicit product requirements:** Create a dedicated feature inventory (MVP vs v2) and acceptance criteria.
- **Pitfall register:** Capture known risks (security, data integrity, layering decisions, testing realism) with mitigations and owners.
- **Deployment/CI story:** No CI/CD artifacts were identified in planning docs; define environments and pipeline expectations.

## Sources

### Primary (HIGH confidence)
- `.planning/codebase/STACK.md` — repository stack, packages, configuration, DI
- `.planning/codebase/ARCHITECTURE.md` — layering, data flow, trade-offs
- `.planning/codebase/STRUCTURE.md` — directory layout and conventions
- `.planning/codebase/INTEGRATIONS.md` — SQL Server, testing DB provider, auth absence
- `.planning/codebase/CONVENTIONS.md` — naming, patterns, error envelope
- `.planning/codebase/TESTING.md` — test tooling and patterns

---
*Research completed: 2026-03-31*
*Ready for roadmap: yes (with noted gaps)*
