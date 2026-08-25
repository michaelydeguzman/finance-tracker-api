# Multi-tenancy and authentication

How Finance Tracker went from a single-household app with no concept of a user to a
multi-tenant API that authenticates callers itself. Written for review of the change, and
kept afterwards as the reference for how tenancy is enforced.

## Why this came before the next feature

Recurring transactions were the obvious next thing to build — the domain model, the
`RecurrenceCalculator` and the whole worker already existed with no way to create a
template. That got deferred once the target became production with real users, because
four things made multi-user impossible:

| | Before |
|---|---|
| Ownership | No `UserId` on `Category`, `Transaction` or `RecurringTransaction` |
| Identity | `CreatedBy` was a free-text string the browser supplied, defaulting to the literal `"finance-tracker-ui"` |
| Categories | Global — every user would have seen and edited every other user's |
| API auth | `Program.cs` called `UseAuthorization()` with no `UseAuthentication()` and no `[Authorize]` anywhere |

Single-user on localhost, that is a reasonable design: the Next.js backend-for-frontend is
the only thing that can route to the API, so it is a real boundary. In production the API
has to be network-reachable, and anyone reaching it directly had full read and write on
everyone's finances.

Building recurring CRUD first would have baked the single-tenant assumption into roughly
ten more files and required a second migration against real financial records to undo.

## Design decisions

**The API owns identity.** It is the layer enforcing tenancy, so it has to answer "who is
this caller?" without trusting a header from the front end. Auth.js stays as the session and
OAuth layer.

**Identity is three tables, not one.** That is what lets one person hold a Google identity
*and* a password identity that resolve to the same account, instead of two accounts with two
disjoint sets of finances.

| Table | Holds |
|---|---|
| `User` | The stable account and tenancy root. Unique lowercased email, verification state, status |
| `UserIdentity` | One row per sign-in method, unique on `(Provider, ProviderSubject)` |
| `UserCredential` | Password hash and security stamp. Absent for SSO-only accounts |
| `UserToken` | Single-use expiring secrets for email verification, password reset, magic link and refresh |

`UserIdentity` is keyed on the provider's subject, never the email — emails change, and a
mutable natural key there would silently re-point an identity at sign-in.

`UserToken` stores a SHA-256 hash, never the value that was sent. A leaked database backup
must not hand over working password-reset links.

**`UserId` is `required`, not merely non-null.** The compiler now rejects any write that does
not name an owner, which is how every existing write path was found rather than guessed at.

**Tenancy foreign keys are `Restrict` throughout.** Deleting a user is an explicit ordered
purge, never a cascade that quietly takes financial records with it.

## Authentication

Three sign-in paths, all resolving to one account per person:

- **Email and password**, hashed with ASP.NET Core Identity's `PasswordHasher`
  (PBKDF2-HMAC-SHA512, per-hash salt, rehash detected per row). Only the hasher is borrowed;
  none of the rest of the Identity stack is in play.
- **Magic link**, doubling as recovery.
- **SSO exchange**, called server-to-server by the front end behind a shared secret compared
  in fixed time.

Access tokens are 15-minute JWTs. Refresh tokens are opaque, stored hashed, and rotate on
use, so a replay fails — which is the signature of a stolen token.

### Endpoints

All under `/api/v1/auth`, all behind a fixed-window per-address rate limit.

| Endpoint | Notes |
|---|---|
| `POST /register` | Always 202, whatever happened |
| `POST /login` | One message for every failure mode |
| `POST /exchange` | BFF-only, shared-secret header |
| `POST /magic-link/request` · `/consume` | |
| `POST /password-reset/request` · `/confirm` | |
| `POST /verify-email` · `/refresh` | |

### Account linking

Auto-linking an SSO identity onto an existing account is the expected convenience *and* a
known takeover path. The rule:

- Link automatically only when the provider asserts `email_verified`.
- Never auto-link into an existing account from an unverified provider email — refused with
  a 409.

Anyone able to register at a provider claiming an address would otherwise inherit the
financial records behind it.

### Enumeration resistance

Every endpoint that takes an email address answers identically whether or not it is known:

- **Registration** against a taken address emails the *real owner* ("someone tried to create
  an account") with a recovery link, rather than telling the caller the address is taken.
  That email is what makes the silence safe rather than merely opaque.
- **Login** burns equivalent CPU on a decoy hash when no account matches, so "unknown email"
  and "wrong password" cannot be told apart by response time.
- **Reset and magic-link requests** return the same acknowledgement either way.

### Password reset ends every session

A reset is the remedy for a suspected compromise, so it rotates the security stamp *and*
consumes outstanding refresh and magic-link tokens. Changing the password alone would leave
an attacker's refresh token working.

### The claim-name trap

The JWT handler's default inbound mapping rewrites `sub` to a long `ClaimTypes` URI. That
would have silently broken tenant resolution — the accessor reads null, and every write
fails closed with a confusing error. Mapping is disabled and the claim name is a single
shared constant used by both the issuing and reading halves. `ClockSkew` is zero; the
default five minutes would quietly extend every 15-minute token.

## How tenancy is enforced

A **model-level EF query filter**, not a `.Where()` in each repository. One forgotten clause
would leak another person's finances, so the filter is applied where it cannot be omitted by
accident.

It **fails closed**. With no user context the filters match nothing, so a caller that has
lost its identity sees an empty result — noticeable and harmless — rather than every
tenant's records.

The worker is the single legitimate exception. It sweeps every user's templates and opts out
by name with `IgnoreQueryFilters()`; each generated `Transaction` inherits its owner from its
template. It is also given an explicit `NoTenantAccessor`, so the escape is deliberate rather
than incidental. There is exactly one `IgnoreQueryFilters()` in production code.

`Frequency` is reference data shared by everyone and is deliberately not scoped.

### `FindAsync` was worth checking

`FindAsync` is a documented exception to query filters in some EF Core versions, and the
repositories use it on **both** the update and delete paths. If it bypassed the filter, every
repository write would be a cross-tenant write.

It honours the filter in EF Core 8. `QueryFilterProbeTests` pins that down rather than
trusting it, because the behaviour is version-dependent and an upgrade could silently reopen
it.

### `CreatedBy` is server-derived

It came off `CreateTransactionDto` entirely and now comes from the token's email claim.
Removing it from the DTO made the compiler enumerate every caller that had been supplying its
own.

## Email delivery

A config line, not a rewrite:

| Provider | Use |
|---|---|
| `Logging` | Default, so an unconfigured environment cannot mail real people |
| `Smtp` | A local catcher (Mailpit on 1025) in development, a real relay in production |
| `Resend` | Plain `HttpClient` — the payload is four fields, so an SDK would be more dependency than it is worth |

## Testing

112 tests, no infrastructure required — the suite runs anywhere, including from a cloud
session.

| Area | Count |
|---|---|
| Auth | 35 |
| Domain | 34 |
| Integration | 21 |
| Worker | 14 |
| Unit | 8 |

Integration tests do **not** stub identity. The factory signs a real JWT with the key the
host validates against, so requests travel the genuine authentication pipeline. Stubbing the
accessor would have left the bearer setup, the claim names and the tenancy filters untested —
which is most of what those tests exist to cover.

### The isolation tests were verified by breaking the code

Eight tests assert the actual goal through real HTTP: another tenant's rows are absent from
both lists, and update and delete of their transaction return not-found while leaving the row
intact.

All eight passed on the first run, which is exactly when a suite deserves suspicion. Removing
the query filters and re-running failed five of the eight — including
`AnotherTenantsTransaction_SurvivesTheAttemptedDelete`, proving that without the filter
another tenant's row is genuinely destroyed, not merely exposed. The three that still passed
are the two authentication checks and the stamping test, which correctly do not depend on the
filter.

### The pre-existing red suite

The suite had 11 failures before this work, which made it useless as a signal. Both causes
were test-only:

- Ten worker tests failed at `Database.GetDbConnection()`. `RunAsync` took the `sp_getapplock`
  run lock inline, and that call is relational-only, so every test on the InMemory provider
  threw before reaching the code under test. The lock moved behind `IRunLock`, with
  `SqlServerRunLock` holding the `sp_getapplock` work and the connection it must stay pinned
  to. Production semantics are unchanged: same resource name, same `Session` owner, same zero
  timeout, still skipped-not-released when acquisition fails.
- One was a stale assertion. `RecurrenceCalculator` rejects null, zero and negative
  `IntervalDays` with "must be a positive number of days"; the test still expected older
  wording. The message is the more accurate of the two, so the test moved — and widened to
  cover zero and negative, which nothing had exercised.

Extracting the lock also made its contract testable for the first time. Four cases now cover
it, including that it is released after a run that throws — a lock left held by a crash would
block every subsequent run indefinitely.

## Migration

`AddIdentityAndTenancy` is generated but **not applied**. Applying it stays a local,
eyes-on operation.

The generated output would not have applied as-is. EF emits `UserId` as `NOT NULL` defaulting
to `Guid.Empty`, then adds the foreign key — which existing rows cannot satisfy, because no
`Users` row has that id. It is hand-edited to seed the owner, add the column nullable,
backfill it, and only then tighten. The scripted SQL confirms that order, ahead of index and
constraint creation.

Two corrections came from reading the generated output:

- `User.Status` came out `nvarchar(max)`, being the one enum-to-string column with no index to
  force a width. All three enum columns now carry an explicit 32.
- Category uniqueness was scoped to `(UserId, Name)`, which would reject an income and an
  expense category sharing a name — plausible for "Other" or "Transfer", and it would have
  failed on apply against existing rows. Now `(UserId, CategoryType, Name)`.

### Before applying

```sql
SELECT CategoryType, Name, COUNT(*) FROM Categories
GROUP BY CategoryType, Name HAVING COUNT(*) > 1;
```

An empty result means the unique index will build. Then script it and read the SQL before
anything runs:

```bash
dotnet ef migrations script --idempotent \
  --project FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj \
  --startup-project FinanceTracker/FinanceTracker.API.csproj \
  --output ./tenancy-migration.sql
```

The migration seeds a `Users` row with a hardcoded email, flagged in a comment. It must match
the Google account you sign in with, or SSO will create a second account instead of adopting
this one.

## Configuration

`appsettings.Development.json` carries only non-secret defaults — issuer, audience,
lifetimes, and SMTP pointing at a local catcher. Secrets go in user-secrets:

```bash
dotnet user-secrets set "Jwt:SigningKey" "<32+ random bytes>" \
  --project FinanceTracker/FinanceTracker.API.csproj
dotnet user-secrets set "Auth:BffSharedSecret" "<random>" \
  --project FinanceTracker/FinanceTracker.API.csproj
```

For local mail, run a catcher and messages land in a browser inbox at `localhost:8025`:

```bash
docker run -d -p 1025:1025 -p 8025:8025 axllent/mailpit
```

## What this does not do

**The UI cannot talk to the API anymore.** Every data endpoint returns 401 without a bearer
token, and nothing in `finance-tracker-ui` sends one yet. That is the next piece of work: the
Auth.js `jwt` callback calling `/auth/exchange`, `callBackend` forwarding the token,
refresh-before-expiry, and the `/register`, `/forgot-password`, `/reset-password` and
`/verify-email` pages.

Two things to carry into that work:

- **Token lifetimes disagree by design.** The Auth.js session runs seven days; the access
  token runs fifteen minutes. Without refresh logic in the `jwt` callback, every user gets
  unexplained 401s a quarter of an hour into their session.
- **`AUTH_ALLOWED_EMAILS` becomes a feature flag.** It is currently the authorization gate.
  With sign-up, new users get their own empty tenant instead — but keeping the allowlist as an
  optional closed-beta switch is a useful kill-switch during rollout.

Beyond that: the worker still runs from Windows Task Scheduler, which is fine for one machine
and a single point of failure for a product. And recurring transactions — the feature this
started as — is now unblocked, and naturally per-user.
