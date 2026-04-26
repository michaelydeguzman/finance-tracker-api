# Pitfalls Research

**Domain:** Authentication & Authorization — Adding ASP.NET Core Identity + JWT + Google OAuth2 to an existing .NET 8 REST API
**Researched:** 2026-04-25
**Confidence:** HIGH (verified against official Microsoft docs, ASP.NET Core GitHub issues, and community post-mortems)

---

## Critical Pitfalls

### Pitfall 1: Wrong Middleware Registration Order

**What goes wrong:**
`UseAuthentication()` or `UseAuthorization()` are added in the wrong position in the middleware pipeline. Authentication silently fails — the endpoint is hit but `HttpContext.User` is anonymous. No exception is thrown; requests just 401 without a useful diagnostic trace.

**Why it happens:**
Developers adding auth to an existing `Program.cs` append `app.UseAuthentication()` at the end near existing `app.MapControllers()` or insert it after `app.UseAuthorization()`. ASP.NET Core middleware is order-sensitive, so identity is never populated before authorization checks run.

**How to avoid:**
Enforce this exact order — no exceptions:
```csharp
app.UseRouting();          // 1
app.UseAuthentication();   // 2 — must come before UseAuthorization
app.UseAuthorization();    // 3
app.MapControllers();      // 4
```
Add a startup assertion test that hits a protected endpoint and asserts 401 before adding a token, then 200 after — this will catch ordering issues in CI.

**Warning signs:**
- Protected endpoints return 200 for unauthenticated requests
- `HttpContext.User.Identity.IsAuthenticated` is always `false` even with a valid token
- No `WWW-Authenticate` header in 401 responses

**Phase to address:** Identity Setup phase (first auth phase)

---

### Pitfall 2: Weak or Hardcoded JWT Signing Key

**What goes wrong:**
A short, predictable, or hardcoded symmetric signing key is used (e.g., `"mysecretkey"` or `"your-secret-key-here"` from a tutorial). Tokens can be forged via brute-force or dictionary attacks. The key is checked into source control, exposed in `appsettings.json`.

**Why it happens:**
Tutorial code uses placeholder keys. Developers copy-paste and forget to replace them. `appsettings.json` is not in `.gitignore` for API projects. The app "just works" locally so no alarm fires.

**How to avoid:**
- Minimum 256-bit (32-byte) key for HS256. Generate via: `openssl rand -base64 32`
- Store in `appsettings.Development.json` (gitignored) for dev, and in environment variable / Azure Key Vault for production
- Never read from `appsettings.json` directly — always from `IConfiguration` with a fallback assertion:
  ```csharp
  var key = config["Jwt:SecretKey"] ?? throw new InvalidOperationException("Jwt:SecretKey not configured");
  ```
- Add `appsettings.*.json` to `.gitignore` for any file containing secrets

**Warning signs:**
- `appsettings.json` contains a `Jwt:SecretKey` field committed to git
- Key is shorter than 32 characters
- Key is the same across dev, staging, and production environments

**Phase to address:** JWT Authentication phase

---

### Pitfall 3: Permissive TokenValidationParameters Disabling Critical Checks

**What goes wrong:**
`ValidateIssuer = false`, `ValidateAudience = false`, or `ValidateLifetime = false` are set to "make it work" during development and are never reverted. Expired tokens are accepted indefinitely. Tokens from other issuers/audiences (or completely different services) are accepted.

**Why it happens:**
These flags are disabled to eliminate `SecurityTokenInvalidIssuerException` or `SecurityTokenInvalidAudienceException` errors during local development when issuer/audience values aren't configured yet. The fix stays in because it silences the error.

**How to avoid:**
Always configure all three validation parameters explicitly. The correct production defaults:
```csharp
new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
    ValidateIssuer = true,
    ValidIssuer = config["Jwt:Issuer"],
    ValidateAudience = true,
    ValidAudience = config["Jwt:Audience"],
    ValidateLifetime = true,
    ClockSkew = TimeSpan.Zero  // strict expiry; no 5-min grace period
}
```
Write an integration test asserting that an expired token (e.g., `exp = now - 1 second`) returns 401.

**Warning signs:**
- Any `Validate* = false` in `TokenValidationParameters`
- `ValidIssuer` or `ValidAudience` are empty strings
- `ClockSkew` is not set (defaults to 5 minutes — tokens appear "valid" 5 minutes after expiry)

**Phase to address:** JWT Authentication phase

---

### Pitfall 4: Inbound Claim Type Map Remapping Claims

**What goes wrong:**
`HttpContext.User.FindFirst("sub")` returns `null` even though the JWT clearly contains a `sub` claim. Authorization policies based on `ClaimTypes.NameIdentifier` or custom claim names silently fail. This is because `JwtSecurityTokenHandler` maps `sub` → `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier` by default.

**Why it happens:**
ASP.NET Core's default JWT handler rewrites standard claim names to legacy WS-Federation URIs. Developers write `user.FindFirst("sub")` expecting the raw JWT claim name and get nothing back.

**How to avoid:**
Clear the inbound claim type map before configuring JWT bearer:
```csharp
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
// OR in AddJwtBearer options:
options.MapInboundClaims = false;
```
Then use raw JWT claim names (`"sub"`, `"email"`, `"name"`) consistently everywhere in the application.

**Warning signs:**
- `User.FindFirst("sub")` returns null but `User.FindFirst(ClaimTypes.NameIdentifier)` returns a value
- Custom claim names in JWT don't appear in `User.Claims`
- `[Authorize(Policy = "...")]` policies that check claims silently deny authenticated users

**Phase to address:** JWT Authentication phase

---

### Pitfall 5: Existing DbContext Not Inheriting from IdentityDbContext — Schema Conflicts

**What goes wrong:**
The existing `AppDbContext` is changed to inherit from `IdentityDbContext<AppUser>` but already has entity configurations that conflict with Identity table names (e.g., a `Users` table that collides with `AspNetUsers`). Migrations fail to apply, or worse — they apply and drop existing data.

**Why it happens:**
Adding `IdentityDbContext` inheritance is the quickest path suggested by docs, but it assumes a greenfield schema. An existing API with its own `Users` or `Roles` table will have collisions. EF migration generation may silently include `DropTable` or `RenameTable` operations.

**How to avoid:**
Options ranked safest first:
1. **Rename existing conflicting tables** before adding Identity (migration: rename `Users` → `AppUsers`)
2. **Custom table names**: Override Identity table names in `OnModelCreating`:
   ```csharp
   modelBuilder.Entity<AppUser>().ToTable("AuthUsers");
   ```
3. **Separate DbContext**: Keep domain entities in `AppDbContext`, add a separate `IdentityDbContext` if coupling is problematic

Always inspect the generated migration SQL (`Script-Migration`) before running `Update-Database` against any environment with existing data.

**Warning signs:**
- Migration SQL contains `DROP TABLE` or `ALTER TABLE ... DROP COLUMN` for tables you didn't touch
- EF diff shows phantom changes to existing entity columns
- `dotnet ef migrations script` output includes modifications to pre-existing tables

**Phase to address:** EF Core / Identity Setup phase

---

### Pitfall 6: Adding UserId FK with NOT NULL to Existing Rows

**What goes wrong:**
A `UserId` foreign key column is added to `Transactions` (or other existing tables) as non-nullable, but the migration runs `ALTER TABLE ADD COLUMN UserId NOT NULL` on a table that already has rows. SQL Server rejects the operation unless a DEFAULT value is provided. The migration fails in production; the database is left in a broken state.

**Why it happens:**
EF Core generates the `UserId` property as `string UserId` (non-nullable reference type in C# 8+ nullable context), which maps to `NOT NULL` in SQL. The migration is tested only against an empty local database where the column addition succeeds trivially.

**How to avoid:**
Treat the migration as a two-step operation:
1. **Migration A**: Add `UserId` as `NULL`-able. Run against production with existing rows.
2. **Data migration**: Populate `UserId` for all existing rows with the seed admin user's ID.
3. **Migration B**: `ALTER COLUMN UserId` to `NOT NULL`.

In the C# model, temporarily mark as `string? UserId` and add `[Required]` only after step 3.

Test migrations against a database snapshot with existing rows, not an empty dev database.

**Warning signs:**
- `Add-Migration` generates `AddColumn` with `nullable: false` for a column being added to a table with existing data
- Migration only tested against `dotnet ef database drop && Update-Database`
- No data seeding step between the two schema migrations

**Phase to address:** EF Core / Data Migration phase

---

### Pitfall 7: Google OAuth2 Redirect URI Mismatch

**What goes wrong:**
The callback URL registered in Google Cloud Console doesn't exactly match what ASP.NET Core sends during the OAuth flow. Google returns `redirect_uri_mismatch` error. The mismatch is often invisible — same URL visually, but HTTP vs HTTPS, trailing slash difference, or localhost port mismatch.

**Why it happens:**
Developers register `https://localhost:5001/signin-google` but the app actually sends `http://localhost:5000/signin-google` in development. Or they register the dev URI and forget to add the production URI before deployment.

**How to avoid:**
- In Google Cloud Console, register **all** environments: development, staging, production
- For development behind a reverse proxy or in Docker, ensure `X-Forwarded-Proto` is handled: `app.UseForwardedHeaders()`
- The default callback path in ASP.NET Core is `/signin-google` — confirm this matches exactly what's registered
- Use HTTPS in development via `dotnet dev-certs https --trust` rather than switching to HTTP

**Warning signs:**
- `redirect_uri_mismatch` error from Google after successful consent screen
- Error appears only in one environment but not another (dev vs. prod URL registered)
- Port number in the registered URI differs from the actual running port

**Phase to address:** Google OAuth2 phase

---

### Pitfall 8: External Login Without Account Linking — Duplicate User Creation

**What goes wrong:**
A user registers with `alice@example.com` via email/password. Later they sign in with Google using the same email. Instead of linking the accounts, a second `AppUser` record is created. Alice now has two accounts with separate data. Her transactions from the password-login account are invisible when she uses Google login.

**Why it happens:**
ASP.NET Core Identity's `ExternalLoginSignInAsync` checks for an existing external login record (`AspNetUserLogins`) but does NOT automatically link by email. If no external login record exists, a new user is created even if the email already exists in `AspNetUsers`.

**How to avoid:**
After `GetExternalLoginInfoAsync()`, check if a local user with the same email already exists and link rather than create:
```csharp
var info = await _signInManager.GetExternalLoginInfoAsync();
var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false);

if (!result.Succeeded)
{
    var email = info.Principal.FindFirstValue(ClaimTypes.Email);
    var existingUser = await _userManager.FindByEmailAsync(email);
    if (existingUser != null)
    {
        // Link the external login to existing account
        await _userManager.AddLoginAsync(existingUser, info);
        await _signInManager.SignInAsync(existingUser, isPersistent: false);
    }
    else
    {
        // Create new user
    }
}
```

**Warning signs:**
- `AspNetUsers` has duplicate email addresses
- User reports "my data is gone" after switching between login methods
- `AspNetUserLogins` has entries but `AspNetUsers` has no matching user for the email

**Phase to address:** Google OAuth2 phase

---

### Pitfall 9: Data Isolation via Global Query Filter — UserId Is Null at Query Time

**What goes wrong:**
The global query filter `HasQueryFilter(e => e.UserId == _currentUserId)` is configured in `OnModelCreating`, but `_currentUserId` is resolved at model-build time (once per application lifetime), not per-request. All users see an empty result set or each other's data.

**Why it happens:**
`OnModelCreating` runs once when the DbContext model is first built. If `_currentUserId` is captured from a constructor parameter or a field set during construction, it's frozen at that value. The proper pattern requires the filter to reference a `DbContext` property that is re-evaluated on each query.

**How to avoid:**
Reference a `DbContext` property (re-evaluated per-query), not a captured field:
```csharp
public class AppDbContext : IdentityDbContext<AppUser>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    // Evaluated fresh on each query — not captured once
    private string? CurrentUserId => 
        _httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>()
            .HasQueryFilter(t => t.UserId == CurrentUserId);
    }
}
```
Verify with an integration test: log in as User A, create a transaction, log in as User B, assert the transaction list is empty.

**Warning signs:**
- All users see the same (empty or full) transaction list
- The filter works correctly in unit tests but not in integration tests
- Queries contain a hardcoded or empty UserId in the generated SQL (visible via EF logging)

**Phase to address:** Data Isolation phase

---

### Pitfall 10: Missing [Authorize] on Endpoints — Relying Solely on Global Query Filter

**What goes wrong:**
The global query filter scopes data to the current user, which gives a false sense of security. But if `[Authorize]` is missing from a controller or action, an anonymous caller can still hit the endpoint. The filter then evaluates with a null `CurrentUserId`, returning an empty list — which looks "safe" but may also return all rows if the filter is `UserId == null` instead of filtering them out.

**Why it happens:**
The attitude of "the filter handles security" leads to omitting `[Authorize]` attributes. The existing API was built without auth, so no controller has `[Authorize]`. When auth is added, each controller/action must be audited — it's easy to miss one.

**How to avoid:**
Set the default policy to require authentication globally, then opt-out for public endpoints:
```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
```
Use `[AllowAnonymous]` only on registration/login endpoints. This inverts the default — secure by default, explicitly open where needed.

**Warning signs:**
- Any controller or action that doesn't have `[Authorize]` and isn't explicitly `[AllowAnonymous]`
- 200 response (even with empty data) from a data endpoint without `Authorization` header
- Integration tests pass without providing a JWT token

**Phase to address:** Data Isolation phase

---

### Pitfall 11: Seeding Admin User with Hardcoded Password or Via HasData

**What goes wrong:**
Admin credentials are committed to source control in `appsettings.json` or hardcoded in a `HasData()` seeding call. In production, the same predictable password is used. Alternatively, `DbContext.Database.EnsureCreated()` is used in production startup — it bypasses migrations and can fail silently or corrupt schema state.

**Why it happens:**
`HasData()` is the "easy" EF Core seeding path and tutorials demonstrate it for Identity too. It requires all values at design time, so the password hash is generated once and baked into the migration file — which is committed to git.

**How to avoid:**
- Use a **startup seeder service** that runs `IsDevelopment()` checks and reads credentials from `IConfiguration` (environment variable or secrets file):
  ```csharp
  if (env.IsDevelopment())
  {
      await seeder.SeedAdminUserAsync(config["Seed:AdminEmail"], config["Seed:AdminPassword"]);
  }
  ```
- The seeder must be **idempotent**: check `await userManager.FindByEmailAsync(email)` before creating
- Production admin credentials must be injected via environment variables or a secrets vault — never committed
- Use `context.Database.MigrateAsync()` in startup, never `EnsureCreated()`
- Ensure `NormalizedEmail` and `NormalizedUserName` are uppercase — Identity lookup will silently fail without them

**Warning signs:**
- `appsettings.json` or a migration file contains a password hash for a named user
- Seed code creates admin user unconditionally (no existence check)
- `Database.EnsureCreated()` appears in production startup code

**Phase to address:** Seed Data phase

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| `ValidateAudience = false` | Eliminates config setup during dev | Tokens from other services accepted in prod | Never |
| Symmetric key in `appsettings.json` | Zero config setup | Key committed to git, brute-forceable | Never |
| Skip `UseAuthentication()` in middleware | Faster to bootstrap | Auth silently ignored for all requests | Never |
| `[Authorize]` only on some controllers | Less boilerplate | Unauthenticated access to missed endpoints | Never |
| `EnsureCreated()` for seeding | Simple startup code | Bypasses migrations; corrupts history in prod | Dev-only test environments |
| `IgnoreQueryFilters()` in business code | Simplifies admin queries | Cross-user data leaks if misapplied | Test code only |
| Single DbContext inheriting IdentityDbContext | Simple setup | Schema coupling; naming collisions risk | Acceptable if no name conflicts |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Google OAuth2 | Register only `localhost` redirect URI in Google Console | Register dev AND prod URIs; use HTTPS for both |
| Google OAuth2 | Assume Identity auto-links by email | Explicitly check for existing email and call `AddLoginAsync` |
| Google OAuth2 | Forget `UseForwardedHeaders()` behind reverse proxy | Add `ForwardedHeadersOptions` so HTTPS scheme is preserved |
| JWT Bearer | Copy `TokenValidationParameters` from tutorial with `false` flags | Define all validation params explicitly; validate via tests |
| EF Core Identity | Run `Update-Database` against prod with existing data | Preview migration SQL with `Script-Migration` first |
| EF Core Identity | Add non-nullable FK to table with existing rows | Two-step migration: nullable first, backfill, then NOT NULL |
| ASP.NET Core Identity | Trust claim name `"sub"` exists after JWT validation | Clear `DefaultInboundClaimTypeMap` or set `MapInboundClaims = false` |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Global query filter with no index on `UserId` | Full table scans on every authenticated request | Add `HasIndex(t => t.UserId)` in `OnModelCreating` | ~1,000+ rows in `Transactions` |
| Eager-loading navigation properties through the global filter | N+1 or cartesian explosion on filtered queries | Profile with `EnableSensitiveDataLogging`; test with realistic data volume | Moderate data sizes |
| JWT validation on every request without claim caching | Minimal at current scale, but redundant parsing | Accepted trade-off for stateless API; no action needed at this scale | Not a concern at personal-use scale |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| JWT secret key in `appsettings.json` committed to git | Token forgery; full account takeover | Use User Secrets for dev, env vars / Key Vault for prod |
| No expiry on JWT tokens (`ValidateLifetime = false`) | Stolen token valid forever | Always set `exp`; set `ClockSkew = TimeSpan.Zero` |
| `[AllowAnonymous]` applied broadly "to fix 401 errors" | Silently opens protected endpoints | Use global `FallbackPolicy`; `[AllowAnonymous]` only on auth endpoints |
| Returning detailed error messages in 401/403 responses | Leaks auth configuration to attackers | Return generic messages in non-dev environments |
| Admin seed user with a predictable password | Trivial account compromise | Environment-variable password; rotate before any production use |
| No `HTTPS` requirement enforced | Bearer tokens transmitted in plaintext | `app.UseHttpsRedirection()` + HSTS in production |
| `IgnoreQueryFilters()` used in production query path | Cross-user data exposure | Strictly limit to test/admin code with code review gate |

---

## "Looks Done But Isn't" Checklist

- [ ] **Middleware order**: `UseAuthentication()` present AND before `UseAuthorization()` — verify by hitting a protected endpoint with no token
- [ ] **JWT validation**: All four validation flags are `true` — check `TokenValidationParameters` in `Program.cs`
- [ ] **Claim type map**: `MapInboundClaims = false` or `DefaultInboundClaimTypeMap.Clear()` is set — verify `User.FindFirst("sub")` returns the expected value
- [ ] **Global query filter**: Filter references a `DbContext` property, not a captured closure — verify with multi-user integration test
- [ ] **FallbackPolicy**: Global `RequireAuthenticatedUser()` fallback policy set — unauthenticated GET to any endpoint returns 401
- [ ] **Migration safety**: `Script-Migration` reviewed before applying to any DB with existing data
- [ ] **Nullable UserId migration**: Two-step migration used if `Transactions` table has existing rows
- [ ] **Google redirect URIs**: Both dev and prod URIs registered in Google Cloud Console
- [ ] **Account linking**: Existing email user is linked, not duplicated, on first Google login
- [ ] **Seed idempotency**: Admin seeder checks for existence before creating; runs only in dev or via explicit env flag
- [ ] **No secrets in git**: `appsettings.*.json` in `.gitignore`; `git log --all -p | Select-String "SecretKey"` returns nothing

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Weak signing key committed to git | MEDIUM | Rotate key immediately; invalidate all existing tokens (key change); force re-login; revoke git history with `git filter-repo` |
| Non-nullable UserId migration failed on prod | HIGH | Restore from backup; re-run as nullable + backfill + NOT NULL three-step migration |
| Duplicate users from Google login | MEDIUM | Write a one-off migration script: merge `AspNetUserLogins` onto the email/password account; delete duplicate; verify FK references |
| Wrong middleware order (auth bypassed in prod) | HIGH | Emergency patch: fix order, deploy, invalidate all sessions | 
| Admin password hardcoded in git | HIGH | Rotate password; force re-login; consider full key rotation if same secret used for JWT |
| Global query filter not filtering (all users see all data) | HIGH | Hotfix deploy with filter fix; audit access logs for potential data exposure; notify affected users if required |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Wrong middleware order | Identity Setup | Integration test: unauthenticated request returns 401; authenticated returns 200 |
| Weak/hardcoded JWT signing key | JWT Authentication | `git grep "SecretKey"` returns nothing; key length assertion in startup |
| Permissive TokenValidationParameters | JWT Authentication | Test: expired token returns 401; wrong issuer returns 401 |
| Inbound claim type map remapping | JWT Authentication | `User.FindFirst("sub")` returns user ID in controller action |
| DbContext / IdentityDbContext schema conflict | EF Core / Identity Setup | `Script-Migration` reviewed; no unexpected DROP/ALTER operations |
| Nullable UserId migration on existing rows | EF Core / Data Migration | Tested against DB snapshot with existing rows; two-step migration applied |
| Google redirect URI mismatch | Google OAuth2 | Test flow works on dev URL; prod URL registered in Google Console |
| External login duplicate user | Google OAuth2 | Integration test: register with email, then Google-login with same email — same user ID in both cases |
| Global query filter per-request UserId | Data Isolation | Multi-user integration test: User A cannot see User B's transactions |
| Missing [Authorize] on endpoints | Data Isolation | Request without token returns 401 on every controller (automated scan) |
| Hardcoded seed credentials | Seed Data | No password hashes in migration files or `appsettings.json`; seed requires env var |

---

## Sources

- [ASP.NET Core JWT Bearer Authentication - Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication)
- [Google External Login Setup - Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins)
- [EF Core Global Query Filters - Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/querying/filters)
- [JWT Token Validation Fails after .NET 6 → .NET 8 Upgrade - ASP.NET Core GitHub Issue #54321](https://github.com/dotnet/aspnetcore/issues/54321)
- [AlterColumn identity always added to migrations - EF Core GitHub Issue #20971](https://github.com/dotnet/efcore/issues/20971)
- [7 Common OAuth 2.0 Security Pitfalls - Duende Software](https://duendesoftware.com/learn/7-common-security-pitfalls-oauth-2-0-implementations)
- [Diagnosing JWT Failures in ASP.NET Core - Julio Casal](https://juliocasal.com/blog/diagnosing-jwt-failures-in-asp-net-core-the-right-way)
- [Migrations with IdentityDbContext conflicts - Stack Overflow](https://stackoverflow.com/questions/55306860/entity-framework-core-migrations-with-identity-db-context)
- [Data Seeding in ASP.NET Core the Right Way - Medium](https://medium.com/@samsondavidoff/data-seeding-in-asp-net-core-the-right-way-4c7c1f4b1773)
- [EF Core Identity add to existing project - Stack Overflow](https://stackoverflow.com/questions/78363121/asp-net-core-8-0-mvc-database-first-c-trying-to-add-identity-with-migration)

---
*Pitfalls research for: ASP.NET Core Identity + JWT + Google OAuth2 on existing .NET 8 API*
*Researched: 2026-04-25*
