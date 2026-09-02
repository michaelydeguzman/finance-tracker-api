# Finance Tracker API

ASP.NET Core 8 REST API for personal finance tracking. Clean Architecture, EF Core
(SQL Server), MediatR, URL-segment API versioning, Swagger.

## Layout

| Project | Role |
|---|---|
| `FinanceTracker/` | API host — controllers, `Program.cs`. Assembly is `FinanceTracker.API`. |
| `FinanceTracker.Domain/` | Entities, pure domain services, and the repository interfaces under `Repositories/`. Depends on nothing else. |
| `FinanceTracker.Application/` | DTOs, MediatR commands/queries + handlers, service interfaces. |
| `FinanceTracker.Infrastructure/` | `FinanceTrackerContext`, entity configurations, repository implementations, EF migrations. |
| `FinanceTracker.Worker/` | Run-and-exit console app that materializes recurring transactions. Triggered by Windows Task Scheduler. |
| `FinanceTracker.Tests/` | xunit + FluentAssertions + Moq. Unit, integration, and worker tests. |

Dependencies point inward. `Domain` references nothing; `Application` and `Infrastructure` are
siblings that each reference only `Domain`; the API host references both and composes them.

Application deliberately does **not** reference Infrastructure. It consumes the repository
contracts from `Domain/Repositories/`, and `Program.cs` binds them to the EF implementations.
Do not add that reference back — it is what put the persistence layer inside the inner one.

Solution file: `FinanceTracker/FinanceTracker.API.sln`

## Running and testing

```bash
dotnet build FinanceTracker/FinanceTracker.API.sln
dotnet test  FinanceTracker/FinanceTracker.API.sln
```

Dev URLs: `https://localhost:7203` and `http://localhost:5185`; Swagger at `/swagger`.

**Visual Studio holds file locks on `bin/Debug/net8.0/*.dll`.** When the API is running
under the VS debugger, CLI builds fail with `MSB3027` / `MSB3021` ("being used by another
process"). That is a lock, not a code error. Check whether it is already serving before
trying to build or start it:

```bash
curl -k https://localhost:7203/api/v1/categories
```

Stop the VS session if you genuinely need a CLI build.

## Build configuration

Three root-level files own settings that used to be repeated per project. A new project
inherits all of it and should declare almost nothing itself.

| File | Owns |
|---|---|
| `Directory.Build.props` | `TargetFramework`, `Nullable`, `ImplicitUsings` for every project. |
| `Directory.Packages.props` | Every package version, via central package management. |
| `.editorconfig` | Code style — file-scoped namespaces, `_camelCase` private fields, Allman braces. |

**Do not put a `Version` attribute on a `PackageReference`.** With central package management
on, that is an error (NU1008). Add or change the version in `Directory.Packages.props`
instead; the `.csproj` names the package only.

Style rules are advisory: `EnforceCodeStyleInBuild` is deliberately not set, so nothing in
`.editorconfig` fails a build. Apply them in bulk with:

```bash
dotnet format style FinanceTracker/FinanceTracker.API.sln
```

## Configuration and secrets

The SQL Server connection string lives in **`dotnet user-secrets`**, not in `appsettings*.json`:

```bash
dotnet user-secrets list --project FinanceTracker/FinanceTracker.API.csproj
```

`appsettings.Development.json` is git-tracked and deliberately has no `ConnectionStrings`
block. Do not add one, and never commit credentials — a previous commit had to strip them.

## Data safety — read before touching the database

**The local database holds real personal financial records, not seed data.**

- Never run destructive or bulk-update SQL against it.
- *Generating* an EF migration is safe anywhere. **Applying** one
  (`dotnet ef database update`) is a deliberate, local, eyes-on operation. Never apply
  migrations from a cloud or remote session — those have no route to this database and no
  business mutating real data.
- Tests never touch it. Integration tests swap in EF Core InMemory via
  `FinanceTracker.Tests/Integration/FinanceTrackerWebApplicationFactory.cs`, so the entire
  suite runs with no local infrastructure — including from a cloud session.

## Recurring transactions

Templates and instances are separate:

- `RecurringTransaction` is the **template** (own table), with `Status` of
  `Active` / `Paused` / `Cancelled`.
- `Transaction` rows are the generated **instances**, linked by a nullable
  `RecurringTransactionId`.
- `FinanceTracker.Worker` expands due templates: it catches up every missed occurrence,
  respects `EndDate`, advances `NextOccurrenceDate`, and isolates failures per template —
  detaching added-but-unsaved entities so one bad template cannot poison the next
  `SaveChangesAsync`.

`RecurrenceCalculator` (`Domain/Services/`) is pure and static. Two things to preserve:

- Its snap-back anchor derives `targetDay` from **`startDate.Day`, never `currentDate.Day`**.
  Otherwise a date clamped by a short month (Jan 31 → Feb 28) drifts permanently.
- Keep it free of I/O and dependencies. That is what makes the date logic trivially testable
  and lets the whole suite run anywhere.

The repository feeding the worker deliberately does **not** use `AsNoTracking()` — EF change
tracking is required to advance `NextOccurrenceDate`.

## Households

A **household** is a group of users who share one set of financial records. It does not
replace the user as the tenancy root — it is a second, wider scope on top of it.

- `User.HouseholdId` is the membership, and it is at most one household per person.
- `Category`, `Transaction` and `RecurringTransaction` each carry a nullable `HouseholdId`,
  stamped from the writer's membership at write time (and copied onto worker-generated rows
  from the template).
- The query filters in `FinanceTrackerContext` therefore admit a row **two ways**:
  `e.UserId == CurrentUserId || (CurrentHouseholdId != null && e.HouseholdId == CurrentHouseholdId)`.
  Keep the explicit null guard — it is what states, rather than accidentally implies, that a
  caller outside any household matches on ownership alone.
- Joining stamps the member's own rows (`StampRecordsAsync`); leaving clears them
  (`DetachRecordsAsync`). That is what keeps the filter a scalar compare instead of a
  subquery over the membership table, and what lets someone leave with their history intact.

**The required-navigation trap, which has two halves.** `Transaction.Category` and
`RecurringTransaction.Category` are *required* navigations to a filtered entity. If a
category stops being visible to someone, the required join silently drops every row of
theirs that points at it — out of their list, their totals and their exports, and out of
`GetByIdAsync` too, so there is no way to reach it through the API at all. Under sharing,
a row and its category can belong to different people, so both halves have to be handled:

- **Categories other people's records depend on do not move**, in either direction.
  `StampRecordsAsync` and `DetachRecordsAsync` both skip them. Leaving a household must not
  strand the people still in it, and joining a new one must not drag a category out of the
  household still using it.
- **Records that depend on other people's categories get a private copy.**
  `ForkBorrowedCategoriesAsync` runs before anything moves on the way out: it gives the
  leaver their own category with the same name and type — reusing one they already have
  rather than tripping the unique index — and re-points their rows at it. That reuse check
  goes through `CollationKey`, which must keep agreeing with how SQL Server compares the
  `(UserId, CategoryType, Name)` index: a plain case-insensitive comparison says `"Snacks "`
  and `"Snacks"` differ where the database says they do not, and the disagreement inserts a
  duplicate the index rejects — leaving the member unable to leave the household at all.

Fixing only the first half is not a fix. It protects the people staying and silently costs
the person leaving their entire history under that category.

That in turn means a household can have rows pointing at it that belong to people who have
left, and every tenancy FK is `Restrict`. `ClearHouseholdStampAsync` runs immediately before
a household is deleted for exactly that reason — without it the last member's departure
throws `DbUpdateException` and the household can never be closed.

`CurrentHouseholdId` comes from `ICurrentUserAccessor.HouseholdId`, which in the API host is
whatever `HouseholdScopeMiddleware` resolved for the request — **not** a JWT claim. A claim
minted at sign-in would keep saying "no household" for the life of the access token after
someone accepted an invitation. The middleware sits between `UseAuthentication` and
`UseAuthorization`: authentication is what puts a principal there to read, and the query
filters consult the answer while EF is composing a query, far too late to go and fetch it.

Membership changes only by **invitation** (`HouseholdInvitation`, addressed to an email).
Never add a user to a household directly — joining publishes the joiner's own records to
everyone already in it, so it has to be their answer about their own data.

Three rules guard that consent, all load-bearing:

- **A confirmed email address** is required to accept an invitation, and to create a
  household or invite anyone. Inviting a typo'd address would otherwise hand a stranger's
  records to whoever registers it next.
- **An invitation dies with its sender's membership.** Accept re-checks that
  `InvitedByUserId` is still in the household. An offer is from a person, not a standing
  property of the group: without this, A invites B, A leaves, ownership passes to C, and B's
  acceptance days later publishes B's history to someone B has never heard of.
- **Invitations are rate limited** (`RateLimitPolicies.HouseholdInvitations`, ceiling in
  `AuthOptions.HouseholdInvitesPerMinute`). It is the only endpoint outside auth that mails
  an address the caller names, with a household name the caller also chose in the subject
  line — an open relay without a ceiling. The integration suite raises the limit rather than
  being throttled by a rule it is not testing.

Two consequences worth knowing:

- Household members can edit and delete each other's records. That follows from the widened
  filter and is deliberate; the repositories' 404-on-another-tenant behaviour is unchanged
  for everyone outside the household.
- Category uniqueness is still scoped to `(UserId, CategoryType, Name)`, so a household can
  see two categories with the same name if two members each created one.

## Conventions

- One MediatR command/query plus its handler per folder under
  `Application/Features/<Area>/{Commands,Queries}/<Name>/`.
- Responses use the `ApiResponseDto` envelope: `{ success, message, data }`. Model validation
  failures included — `InvalidModelStateResponseFactory` in `Program.cs` wraps them, since
  `[ApiController]` rejects an invalid model before any action body runs. Do not add per-action
  `ModelState.IsValid` checks; they are unreachable.
- Controllers are versioned: `/api/v{version}/...`.
- Transactions list pagination is 1-based, `pageSize` caps at 20, and paged responses carry
  `totalCount`. Calls without paging params must keep returning the full list.
- **A transaction's `CategoryId` is validated through the tenancy-scoped repository** on
  create and update, the way the recurring handlers already did it. That lookup doubles as
  the reachability check: accepting any id the foreign key allows would write a row whose
  required category nobody can see, which is invisible to every member and uncorrectable.
- Every repository and service method that does I/O takes a trailing
  `CancellationToken cancellationToken = default` and forwards it to the EF call. MediatR
  handlers pass the token they are given; a handler that drops it leaves queries running
  after the client has gone. `ITransactionRepository.GetTransactionsQueryable()` is the
  exception — nothing executes until the caller enumerates it, and that call site passes
  its own token.
- Add or update tests alongside behavior changes; prefer writing the failing test first.
