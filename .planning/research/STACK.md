# Stack Research

**Domain:** ASP.NET Core REST API — Authentication & Authorization additions
**Researched:** 2026-04-25
**Confidence:** HIGH (all packages verified against NuGet.org; official docs consulted)

---

## Context: What Exists vs. What Is Being Added

This milestone adds auth to an **existing** .NET 8 API that already has:
- EF Core 8 + SQL Server (`FinanceTrackerContext : DbContext` in `FinanceTracker.Infrastructure`)
- MediatR CQRS
- API versioning + Swagger

The packages below are **additions only**. However, the existing EF Core packages are pinned at `8.0.0` in `FinanceTracker.API.csproj` and must be bumped to `8.0.25` to match the Identity package's transitive dependency (`Identity.EntityFrameworkCore 8.0.25` requires `EntityFrameworkCore.Relational >= 8.0.25`).

---

## Recommended Stack

### Core NuGet Packages to Add

| Package | Version | Purpose | Why |
|---------|---------|---------|-----|
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | `8.0.25` | Identity user/role management backed by EF Core | The canonical .NET identity system; adds `AspNetUsers`, `AspNetUserTokens`, etc. tables via EF migrations. Integrates directly into the existing `DbContext`. Required for `UserManager<T>`, `SignInManager<T>`, and the external login pipeline. |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | `8.0.15` | JWT bearer middleware that validates `Authorization: Bearer` headers | Ships with ASP.NET Core; zero-config DI integration. Validates tokens on every request — no custom middleware needed. Pinning to `8.0.x` keeps parity with the rest of the ASP.NET Core runtime packages. |
| `Microsoft.AspNetCore.Authentication.Google` | `8.0.15` | Google OAuth2 / OpenID Connect external login handler | Official Microsoft package implementing the OAuth2 authorization-code flow for Google. Integrates with ASP.NET Core Identity's external login pipeline out of the box. Same `8.0.x` version train as JwtBearer and Identity. |
| `System.IdentityModel.Tokens.Jwt` | `8.17.0` | JWT token creation (`JwtSecurityTokenHandler` / `SecurityTokenDescriptor`) | Pulled in as a transitive dependency of `JwtBearer`, but should be referenced **explicitly** because you will call `JwtSecurityTokenHandler.CreateEncodedJwt()` (or `JsonWebTokenHandler`) in your token-generation service. Version `8.x` is recommended over `7.x` even on .NET 8 — `7.x` reaches EOL Nov 10, 2026. |

> **Version note:** All three ASP.NET Core packages (`JwtBearer`, `Identity.EntityFrameworkCore`, `Authentication.Google`) are at `8.0.15` as of April 2026 — the latest patch on the .NET 8 LTS train. Verified on NuGet.org.

---

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.IdentityModel.JsonWebTokens` | `8.17.0` | Modern, async-first JWT handler (transitive dep of `System.IdentityModel.Tokens.Jwt 8.x`) | Optional explicit reference if you want to use `JsonWebTokenHandler` (the new API) instead of `JwtSecurityTokenHandler` (the legacy API). The new handler is ~30% faster and preferred for new code. Transitive otherwise. |

---

## Installation

```bash
# Core auth packages (target project where DbContext lives)
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 8.0.15
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 8.0.15
dotnet add package Microsoft.AspNetCore.Authentication.Google --version 8.0.15
dotnet add package System.IdentityModel.Tokens.Jwt --version 8.17.0
```

---

## Integration Points with Existing Stack

### 1. EF Core DbContext — Identity Tables

`ApplicationDbContext` must switch base class from `DbContext` to `IdentityDbContext<ApplicationUser>`:

```csharp
// Before
public class ApplicationDbContext : DbContext { ... }

// After
public class ApplicationUser : IdentityUser { }  // extend if you need custom columns

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Transaction> Transactions { get; set; }
    // ... existing DbSets unchanged
}
```

This triggers a migration that adds 6 Identity tables to the existing SQL Server database:
`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins`, `AspNetUserTokens`

The existing `Transactions` table and all prior migrations are unaffected.

### 2. Transaction → User FK

Add `UserId` (string FK → `AspNetUsers.Id`) to the `Transaction` entity. This is a separate migration after the Identity migration. Seed admin user is created in `HasData` or via a migration with a fixed GUID.

### 3. MediatR — Where Auth Logic Lives

For consistency with the existing CQRS pattern, auth operations should be MediatR commands:
- `RegisterCommand` → creates Identity user, returns JWT
- `LoginCommand` → validates credentials, returns JWT
- `GoogleCallbackCommand` → processes external login callback, returns JWT

Alternatively, auth logic can live in a dedicated `ITokenService` / `IAuthService` and be called directly from a non-MediatR controller. Either approach is valid; MediatR is recommended for uniformity.

### 4. `Program.cs` Registration Order

Order matters. Add after `AddDbContext`, before `AddControllers`:

```csharp
// Identity (after DbContext)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    // Email confirmation NOT required (auto-verified this milestone)
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// JWT Bearer
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ClockSkew = TimeSpan.FromSeconds(30) // tighten from default 5 min
    };
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
});

// Middleware pipeline (after app.UseRouting())
app.UseAuthentication(); // must come before UseAuthorization
app.UseAuthorization();
```

---

## JWT Configuration

### appsettings.json structure

```json
{
  "Jwt": {
    "Key": "<SECRET — minimum 32 characters for HMAC-SHA256>",
    "Issuer": "finance-tracker-api",
    "Audience": "finance-tracker-client",
    "ExpiryMinutes": 60
  },
  "Authentication": {
    "Google": {
      "ClientId": "<from Google Cloud Console>",
      "ClientSecret": "<from Google Cloud Console>"
    }
  }
}
```

Store real values in `dotnet user-secrets` for local dev; environment variables / Azure Key Vault for production.

### Signing Key Choice

**Use symmetric (HMAC-SHA256) for this project.** The key must be ≥ 256 bits (32 ASCII chars):

- Symmetric is appropriate because a single API both issues and validates its own tokens — asymmetric (RSA/EC) is only needed when multiple independent services need to validate tokens without sharing a secret.
- Keep the key at least 32 characters; `JwtBearerDefaults.AuthenticationScheme` will throw at startup with a shorter key.

### Access Token Expiry

Recommend **60 minutes** for a personal finance app. Rationale: short enough to limit exposure if stolen; long enough that refresh is infrequent on a personal tool. Adjust to 15 min if you implement automatic silent refresh on the client.

### Refresh Token Strategy

**Use Identity's `AspNetUserTokens` table** — no additional packages required:

```csharp
// Store (on login / refresh)
await userManager.SetAuthenticationTokenAsync(user, "FinanceTracker", "RefreshToken", newToken);

// Retrieve
var stored = await userManager.GetAuthenticationTokenAsync(user, "FinanceTracker", "RefreshToken");

// Revoke (on logout)
await userManager.RemoveAuthenticationTokenAsync(user, "FinanceTracker", "RefreshToken");
```

Generate the refresh token value with `RandomNumberGenerator.GetHexString(64)` (BCL, no extra package). Store a SHA-256 hash if you want to prevent DB-level theft. Refresh token lifetime: **30 days**.

**Rotation:** Issue a new refresh token every time the old one is exchanged. Remove the old one atomically.

---

## Google OAuth2 Setup

### Flow for a REST API (Redirect-Based)

The `Microsoft.AspNetCore.Authentication.Google` middleware uses the OAuth2 authorization-code flow:

1. Client calls `GET /api/v1/auth/external/google` → controller calls `HttpContext.ChallengeAsync("Google", props)` with a `RedirectUri` pointing to the callback endpoint.
2. User is redirected to Google's consent screen.
3. Google redirects back to `/signin-google` (built-in default path).
4. The app's `/api/v1/auth/external/callback` endpoint reads the external login info via `SignInManager.GetExternalLoginInfoAsync()`, finds or creates the Identity user, and returns a JWT.

### Google Cloud Console Steps

1. Create a project at [console.cloud.google.com](https://console.cloud.google.com)
2. Configure OAuth Consent Screen → External → add your app name and support email
3. Credentials → Create OAuth 2.0 Client ID → Web application
4. Authorized redirect URIs:
   - Local: `https://localhost:{PORT}/signin-google`
   - Production: `https://your-domain.com/signin-google`
5. Copy **Client ID** and **Client Secret** into `user-secrets` / environment variables

---

## Alternatives Considered

| Recommended | Alternative | Why Not |
|-------------|-------------|---------|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Duende IdentityServer / OpenIddict | Full OAuth2/OIDC server — massively overengineered for a single first-party API. Adds an identity provider service, discovery endpoints, consent flows. Use when multiple client apps / resource servers need a central auth authority. |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Cookie authentication | Cookies are browser-first and require CSRF protection. JWT bearer is stateless and idiomatic for REST APIs consumed by mobile / SPA clients. PROJECT.md explicitly chose JWT bearer for this reason. |
| `Microsoft.AspNetCore.Authentication.Google` | `Google.Apis.Auth.AspNetCore3` | The alternative uses Google's own client library designed for server-to-server Google API calls with offline access. For login-only SSO without needing Google API access, the standard `Authentication.Google` middleware is the right tool — simpler, no extra dependencies. |
| `SymmetricSecurityKey` (HMAC-SHA256) | RSA asymmetric signing | RSA is needed when external services must validate tokens without sharing a secret. A single API issuing and validating its own tokens doesn't need this complexity. |
| Identity `AspNetUserTokens` for refresh tokens | Redis / in-memory revocation cache | Redis adds infrastructure complexity. For a personal app with low traffic, the DB is sufficient. Use Redis if you need sub-millisecond revocation checks at scale. |

---

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `Microsoft.IdentityModel.Tokens` version `6.x` | Deprecated — support ended May 2024 (.NET 7 lifetime). May contain unpatched CVEs. | `System.IdentityModel.Tokens.Jwt` 8.17.0 |
| `AddDefaultIdentity<T>()` | Includes Razor Pages UI scaffolding (login pages, etc.) — irrelevant and adds dead code to a pure API project | `AddIdentity<TUser, TRole>()` — the full service without UI; gives you `RoleManager<T>` too |
| Hardcoding `Jwt:Key` in `appsettings.json` in source control | Secret exposure in git history | `dotnet user-secrets` for dev; environment variable `JWT__KEY` for production |
| `ClockSkew = TimeSpan.Zero` | Causes token rejection due to clock drift between servers | `TimeSpan.FromSeconds(30)` — tight but tolerant of minor drift |
| `[AllowAnonymous]` as the default (no global policy) | Forgetting to add `[Authorize]` to a new endpoint leaks data | Apply a global `AuthorizationPolicy` requiring authenticated users and opt-out specific endpoints with `[AllowAnonymous]` |

---

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore 8.0.15` | `Microsoft.EntityFrameworkCore 8.x` | Requires the same major EF Core version. If existing project uses EF Core 8.x (which it does per PROJECT.md), no conflict. |
| `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.15` | `System.IdentityModel.Tokens.Jwt 7.x` or `8.x` | JwtBearer 8.0.x ships with a transitive `System.IdentityModel.Tokens.Jwt 7.x`. Explicitly adding `8.17.0` upgrades the transitive dep; this is safe and recommended per the IdentityModel maintainers. |
| `System.IdentityModel.Tokens.Jwt 8.x` | .NET 8 LTS | Fully supported. `7.x` is also LTS through Nov 2026 on .NET 8, but `8.x` is the forward path. |
| All three `Microsoft.AspNetCore.*` packages | .NET 8 | Must all be `8.0.x` — mixing major versions (e.g., one at `9.x`) causes assembly binding failures. |

---

## Sources

- NuGet.org — `Microsoft.AspNetCore.Authentication.JwtBearer 8.0.15` — version confirmed HIGH confidence
- NuGet.org — `Microsoft.AspNetCore.Identity.EntityFrameworkCore 8.0.15` — version confirmed HIGH confidence
- NuGet.org — `Microsoft.AspNetCore.Authentication.Google 8.0.15` — version confirmed HIGH confidence
- NuGet.org — `System.IdentityModel.Tokens.Jwt 8.17.0` — version confirmed, lifecycle matrix confirmed HIGH confidence
- [Microsoft Learn — Google external login setup in ASP.NET Core 8.0](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins?view=aspnetcore-8.0) — OAuth2 flow steps, redirect URI, package choice HIGH confidence
- [Microsoft Learn — Configure JWT bearer authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-8.0) — `TokenValidationParameters` configuration HIGH confidence
- [Red Gate Simple Talk — How to use refresh tokens in ASP.NET Core (Mar 2026)](https://www.red-gate.com/simple-talk/development/dotnet-development/how-to-use-refresh-tokens-in-asp-net-core-a-complete-guide/) — refresh token strategy MEDIUM confidence (community source, corroborated by official docs)

---

*Stack research for: Finance Tracker API — v1.1 Authentication & Authorization*
*Researched: 2026-04-25*
