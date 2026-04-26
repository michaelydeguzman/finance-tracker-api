# Architecture Research — Auth & Authorization Integration

**Domain:** ASP.NET Core Identity + JWT Bearer + Google OAuth2 on existing .NET 8 REST API
**Researched:** 2026-04-25
**Confidence:** HIGH — patterns verified against official Microsoft docs and community consensus

---

## System Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Presentation Layer                                │
│  ┌───────────────────┐  ┌────────────────────────────────────────────┐  │
│  │  AuthController   │  │  TransactionsV1Controller (+ other *V1)    │  │
│  │  /register        │  │  [Authorize] on all actions                │  │
│  │  /login           │  │  → ISender.Send(query/command)             │  │
│  │  /auth/google     │  └────────────────────────────────────────────┘  │
│  │  /auth/callback   │                                                   │
│  └────────┬──────────┘                                                   │
├───────────┴─────────────────────────────────────────────────────────────┤
│                     ASP.NET Core Middleware Pipeline                     │
│         UseAuthentication() → UseAuthorization() → MapControllers()     │
│  ┌──────────────────────────┐  ┌───────────────────────────────────┐    │
│  │  JWT Bearer Middleware   │  │  Google OAuth2 Handler            │    │
│  │  (validates Bearer token)│  │  (external challenge/callback)    │    │
│  └──────────────────────────┘  └───────────────────────────────────┘    │
├─────────────────────────────────────────────────────────────────────────┤
│                        Application Layer                                 │
│  ┌──────────────────┐  ┌───────────────────────────────────────────┐    │
│  │  ICurrentUser    │  │  MediatR Handlers                         │    │
│  │  Service         │  │  GetTransactionsListQueryHandler          │    │
│  │  (reads JWT sub  │  │  → injects ICurrentUserService            │    │
│  │   claim via      │  │  → filters .Where(t => t.UserId == uid)   │    │
│  │   IHttpContext   │  └───────────────────────────────────────────┘    │
│  │   Accessor)      │                                                   │
│  └──────────────────┘                                                   │
│  ┌──────────────────┐                                                   │
│  │  IJwtTokenService│  (interface — implemented in Infrastructure)      │
│  └──────────────────┘                                                   │
├─────────────────────────────────────────────────────────────────────────┤
│                       Infrastructure Layer                               │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │  FinanceTrackerContext : IdentityDbContext<ApplicationUser>       │   │
│  │  (extends existing DbContext — one context, one connection)       │   │
│  │  + Identity tables: AspNetUsers, AspNetRoles, AspNetUserRoles ... │   │
│  │  + Transactions.UserId FK → AspNetUsers.Id                        │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│  ┌────────────────────┐  ┌─────────────────────────────────────────┐    │
│  │  JwtTokenService   │  │  DataSeeder (admin user + transactions) │    │
│  │  (issues HS256 JWT │  │  runs on app startup                    │    │
│  │   from user claims)│  └─────────────────────────────────────────┘    │
│  └────────────────────┘                                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                         Domain Layer                                     │
│  ┌─────────────────────┐  ┌────────────────────────────────────────┐    │
│  │  ApplicationUser    │  │  Transaction (adds UserId Guid)        │    │
│  │  : IdentityUser     │  │  FK to ApplicationUser.Id              │    │
│  │  (custom user type) │  └────────────────────────────────────────┘    │
│  └─────────────────────┘                                                 │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Component Boundaries

| Component | Layer | Responsibility | Communicates With |
|-----------|-------|----------------|-------------------|
| `AuthController` | Presentation | Handles `/register`, `/login`, `/auth/google`, `/auth/google/callback`; delegates to `UserManager` and `IJwtTokenService` | `UserManager<ApplicationUser>`, `SignInManager`, `IJwtTokenService`, Google middleware |
| `[Authorize]` on `*V1Controller` | Presentation | Rejects unauthenticated requests with 401 before handler runs | JWT Bearer Middleware |
| JWT Bearer Middleware | Middleware | Validates `Authorization: Bearer <token>`, populates `HttpContext.User` | `FinanceTrackerContext` (key validation only via config) |
| Google OAuth2 Handler | Middleware | Manages OAuth2 challenge/callback with Google; surfaces external claims to `AuthController` callback | Google OAuth2 servers, `HttpContext` |
| `ICurrentUserService` | Application | Abstracts current user ID extraction from JWT claims | `IHttpContextAccessor` |
| `CurrentUserService` | Infrastructure | Implements `ICurrentUserService` by reading `ClaimTypes.NameIdentifier` | `IHttpContextAccessor` |
| `IJwtTokenService` | Application | Interface for JWT generation — decouples Application from crypto | `JwtTokenService` (Infrastructure) |
| `JwtTokenService` | Infrastructure | Generates signed HS256 JWT with `sub`, `email`, `jti`, `iat`, `exp` claims | `IConfiguration` (for signing key + issuer + audience) |
| `FinanceTrackerContext` | Infrastructure | Single EF Core DbContext; inherits `IdentityDbContext<ApplicationUser>` | SQL Server, all repositories |
| `ApplicationUser` | Domain | Custom Identity user entity; allows adding profile fields later | `IdentityUser<Guid>` base |
| `DataSeeder` | Infrastructure | Creates admin user via `UserManager` and assigns existing transactions on startup | `UserManager<ApplicationUser>`, `FinanceTrackerContext` |
| Query/Command Handlers | Application | Filter data by `ICurrentUserService.UserId`; never access `HttpContext` directly | `ICurrentUserService`, `ITransactionRepository` |

---

## EF Core Migration Strategy

### Decision: Extend Existing DbContext (not a separate context)

**Rationale:** The project has one SQL Server database, one set of migrations, and one DI registration. Splitting into two contexts would require a second connection string, cross-context join workarounds, and migration complexity. The standard approach for API-only projects is to change `DbContext` inheritance to `IdentityDbContext<TUser>`.

**Change required:**

```csharp
// Before
public class FinanceTrackerContext : DbContext

// After
public class FinanceTrackerContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
```

`OnModelCreating` must call `base.OnModelCreating(modelBuilder)` **before** `ApplyConfigurationsFromAssembly(...)` — this is already present and correct.

### Migration sequence

| Migration | What it adds | Command |
|-----------|-------------|---------|
| `AddIdentityTables` | All 7 `AspNet*` tables (`AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `AspNetRoleClaims`) | `dotnet ef migrations add AddIdentityTables` |
| `AddUserIdToTransactions` | `Transactions.UserId` column (nullable initially, then backfilled via seeder before making required) | `dotnet ef migrations add AddUserIdToTransactions` |

**Nullable-first migration approach for `UserId`:**
Add `UserId` as `Guid?` in the first migration. The `DataSeeder` assigns all existing rows to the admin user on startup. A subsequent migration (or the same one with a default constraint) can make it non-nullable. This avoids the "existing rows have no FK value" constraint violation.

### `ApplicationUser` placement

Place `ApplicationUser` in `FinanceTracker.Domain` — it is a domain entity. Use `IdentityUser<Guid>` as base to get Guid primary keys consistent with the existing `Transaction.Id` (Guid).

```csharp
// Finance.Tracker.Domain/Entities/ApplicationUser.cs
public class ApplicationUser : IdentityUser<Guid>
{
    // Extension point for profile fields in future milestones
}
```

---

## Data Flow

### Registration (email/password)

```
POST /api/v1/auth/register  { email, password }
    ↓
AuthController.Register(RegisterRequest)
    ↓
UserManager<ApplicationUser>.CreateAsync(user, password)
    → Identity validates password policy, hashes password (PBKDF2)
    → Writes row to AspNetUsers
    ↓
JwtTokenService.GenerateToken(user)
    → Builds ClaimsIdentity { sub: user.Id, email: user.Email, jti, iat, exp }
    → Signs with HS256 using key from IConfiguration["Jwt:Key"]
    → Returns token string
    ↓
HTTP 200  { token: "eyJ..." }
```

### Login (email/password)

```
POST /api/v1/auth/login  { email, password }
    ↓
AuthController.Login(LoginRequest)
    ↓
UserManager.FindByEmailAsync(email)
UserManager.CheckPasswordAsync(user, password)
    → Compares PBKDF2 hash; returns bool
    ↓ (success)
JwtTokenService.GenerateToken(user)
    ↓
HTTP 200  { token: "eyJ..." }

(failure) → HTTP 401 Unauthorized
```

### Google SSO — Challenge

```
GET /api/v1/auth/google
    ↓
AuthController.GoogleLogin()
    → ChallengeResult("Google", { RedirectUri: "/api/v1/auth/google/callback" })
    ↓
Google OAuth2 Handler constructs redirect to accounts.google.com
    ↓
Browser → accounts.google.com → user consents → redirect back
```

### Google SSO — Callback (issues JWT)

```
GET /api/v1/auth/google/callback  (Google redirects here with code)
    ↓
AuthController.GoogleCallback()
    ↓
HttpContext.AuthenticateAsync("Google")
    → Google handler exchanges code for tokens, returns ClaimsPrincipal
    ↓
Extract: email = principal.FindFirst(ClaimTypes.Email)
         name  = principal.FindFirst(ClaimTypes.Name)
    ↓
UserManager.FindByEmailAsync(email)
    ├─ found  → use existing user
    └─ not found → UserManager.CreateAsync(new ApplicationUser { Email, UserName })
                   UserManager.AddLoginAsync(user, ExternalLoginInfo)  ← links Google provider
    ↓
JwtTokenService.GenerateToken(user)
    ↓
HTTP 200  { token: "eyJ..." }
  (or redirect with ?token=... if supporting a browser-based callback flow)
```

### Authenticated API Request (per-user data isolation)

```
GET /api/v1/transactions  [Authorization: Bearer eyJ...]
    ↓
JWT Bearer Middleware
    → Validates signature, issuer, audience, expiry
    → Populates HttpContext.User (ClaimsPrincipal)
    ↓
[Authorize] attribute  → passes (401 if missing/invalid)
    ↓
TransactionsV1Controller.GetTransactions(query params)
    → _sender.Send(new GetTransactionsListQuery(...))
    ↓
GetTransactionsListQueryHandler.Handle(request, ct)
    → _currentUserService.UserId  (reads sub claim from HttpContext.User)
    → _transactionRepository.GetTransactionsQueryable()
         .Where(t => t.UserId == userId)   ← isolation filter
         [+ existing date/category/paging filters]
    ↓
HTTP 200  { items: [...] }
```

---

## How MediatR Handlers Resolve the Current User

### Pattern: `ICurrentUserService` injected into handlers

**Do not** inject `IHttpContextAccessor` directly into MediatR handlers. Handlers are application logic and should not depend on HTTP infrastructure. Instead, define an interface in the Application layer:

```csharp
// FinanceTracker.Application/Services/ICurrentUserService.cs
public interface ICurrentUserService
{
    Guid UserId { get; }
    bool IsAuthenticated { get; }
}
```

Implement in Infrastructure (which is allowed to reference HTTP concerns):

```csharp
// FinanceTracker.Infrastructure/Services/CurrentUserService.cs
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public Guid UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?
                .User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(value, out var id)
                ? id
                : throw new UnauthorizedAccessException("No authenticated user.");
        }
    }

    public bool IsAuthenticated
        => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;
}
```

Register as `Scoped` (one per request, matching HttpContext lifetime):

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
```

Inject into handlers that need user scoping:

```csharp
public sealed class GetTransactionsListQueryHandler
    : IRequestHandler<GetTransactionsListQuery, GetTransactionsListResult>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetTransactionsListQueryHandler(
        ITransactionRepository transactionRepository,
        ICurrentUserService currentUserService)
    {
        _transactionRepository = transactionRepository;
        _currentUserService = currentUserService;
    }

    public async Task<GetTransactionsListResult> Handle(
        GetTransactionsListQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var query = _transactionRepository.GetTransactionsQueryable()
            .Where(t => t.UserId == userId);
        // ... existing filter/paging logic unchanged
    }
}
```

**Why not a MediatR pipeline behavior for this?**
A global behavior that sets `UserId` on the request object is appealing but fragile — it makes all requests implicitly depend on auth context, complicates testing, and breaks for the `AuthController` actions that run before a user exists. Per-handler injection is explicit and testable.

---

## Architectural Patterns

### Pattern 1: AddIdentityCore (not AddIdentity) for JWT APIs

**What:** `AddIdentityCore` registers only user management services (UserManager, password hasher, validators). `AddIdentity` additionally registers cookie authentication schemes which override JWT bearer as the default scheme.

**When to use:** Always for pure API projects using JWT.

**Trade-offs:** Must manually chain `.AddSignInManager()` and `.AddDefaultTokenProviders()` if needed. Slightly more verbose than `AddIdentity`, but avoids silent scheme override bugs.

```csharp
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<FinanceTrackerContext>()
.AddSignInManager<SignInManager<ApplicationUser>>()
.AddDefaultTokenProviders();

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
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
    options.CallbackPath = "/api/v1/auth/google/callback";
});
```

### Pattern 2: Google OAuth2 → JWT bridge (no cookies in final response)

**What:** Google's OAuth2 handler is cookie-based by default. For an API, the controller callback must explicitly call `AuthenticateAsync("Google")` to retrieve external claims, then issue its own JWT — never relying on Google's cookie persisting to the client.

**When to use:** Whenever combining `AddGoogle` with a JWT-only API.

**Trade-offs:** Requires a dedicated callback endpoint that exchanges the Google result for a JWT. The redirect-to-frontend pattern (return `?token=...`) works but exposes the token in browser history — prefer a short-lived code exchange if security is a concern in later milestones.

### Pattern 3: Nullable-first migration for foreign key backfill

**What:** When adding a non-nullable FK to an existing table with data, add the column as nullable first, seed the FK values via a `DataSeeder` (running before any constraints are checked), then optionally tighten to non-nullable in a follow-up migration.

**When to use:** Any time an existing populated table needs a new required FK.

**Trade-offs:** Two migrations instead of one; seeder must run before API serves requests. Using `IHostedService` or `Program.cs` startup code (calling `DataSeeder.SeedAsync()` before `app.Run()`) keeps this deterministic.

---

## Recommended Project Structure (new files only)

```
Finance.Tracker.Domain/Entities/
└── ApplicationUser.cs              # : IdentityUser<Guid>

FinanceTracker.Application/Services/
└── ICurrentUserService.cs          # interface (UserId, IsAuthenticated)
└── IJwtTokenService.cs             # interface (GenerateToken)

FinanceTracker.Infrastructure/
├── Persistence/
│   ├── FinanceTrackerContext.cs    # change base: IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
│   ├── DataSeeder.cs              # seeds admin user, assigns transactions.UserId
│   └── Migrations/
│       ├── *_AddIdentityTables.cs
│       └── *_AddUserIdToTransactions.cs
└── Services/
    ├── CurrentUserService.cs       # implements ICurrentUserService
    └── JwtTokenService.cs          # implements IJwtTokenService

FinanceTracker/ (API host)
└── Controllers/
    └── AuthController.cs           # /register, /login, /auth/google, /auth/google/callback
```

---

## Build Order (Phase Dependencies)

The components have hard dependencies that dictate build order. Building out of order causes compilation or runtime failures.

| Phase | What to Build | Depends On | Why First |
|-------|--------------|------------|-----------|
| **1 — Identity Foundation** | `ApplicationUser`, change `FinanceTrackerContext` base to `IdentityDbContext`, migration for Identity tables, `AddIdentityCore` in Program.cs | Nothing (additive) | All subsequent phases need `UserManager`, `AspNetUsers` table, and `FinanceTrackerContext` to know about Identity |
| **2 — JWT Token Service** | `IJwtTokenService`, `JwtTokenService`, `AddAuthentication().AddJwtBearer()` in Program.cs, `UseAuthentication()` in pipeline | Phase 1 (needs `ApplicationUser` to build claims) | Auth endpoints (Phase 3) issue tokens; middleware (Phase 4) validates them |
| **3 — Auth Endpoints** | `AuthController` with `/register` and `/login` | Phase 1 (UserManager), Phase 2 (JwtTokenService) | Must work before protecting other endpoints or adding Google |
| **4 — Protect Existing Endpoints** | Add `[Authorize]` to all `*V1Controller` classes, verify `UseAuthentication()` before `UseAuthorization()` in pipeline | Phase 2 (JWT middleware must exist) | Per-user data isolation (Phase 6) requires requests to be authenticated first |
| **5 — Google OAuth2** | `AddGoogle(...)` config, callback handler in `AuthController`, `UserManager.AddLoginAsync` for external login linking | Phase 1 (UserManager), Phase 2 (JwtTokenService), Phase 3 (same AuthController) | Google SSO builds on the same JWT issuance path as email login |
| **6 — Per-User Data Isolation** | `UserId` on `Transaction` entity, migration, `DataSeeder` (assign existing rows to admin), `ICurrentUserService`, `CurrentUserService`, inject into query/command handlers | Phase 1 (ApplicationUser.Id as FK target), Phase 4 (endpoints must be protected) | Isolation only makes sense after endpoints require authentication |

---

## Anti-Patterns

### Anti-Pattern 1: Using `AddIdentity` instead of `AddIdentityCore` in a JWT API

**What people do:** Call `services.AddIdentity<ApplicationUser, IdentityRole>()` because it's the "standard" Identity setup.

**Why it's wrong:** `AddIdentity` registers cookie authentication as the default scheme, silently overriding JWT bearer. Authenticated endpoints stop returning 401 and instead return 302 redirects to a login page that doesn't exist in an API.

**Do this instead:** Use `AddIdentityCore` and explicitly set `DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme`.

### Anti-Pattern 2: Injecting `IHttpContextAccessor` directly into MediatR handlers

**What people do:** Add `IHttpContextAccessor` as a constructor parameter on query/command handlers to access `HttpContext.User`.

**Why it's wrong:** Couples application logic to HTTP infrastructure. Handlers become untestable without mocking HTTP context. Violates separation of concerns.

**Do this instead:** Define `ICurrentUserService` in Application and inject it. The infrastructure implementation can use `IHttpContextAccessor` but handlers stay clean.

### Anti-Pattern 3: Creating a separate `IdentityDbContext` alongside the existing `DbContext`

**What people do:** Keep `FinanceTrackerContext : DbContext` untouched and add a second `ApplicationDbContext : IdentityDbContext<ApplicationUser>`.

**Why it's wrong:** Two separate contexts mean two separate connection pools, no cross-context foreign key enforcement, and doubled migration complexity. The `Transaction.UserId` FK cannot reference `AspNetUsers` if they live in different contexts.

**Do this instead:** Change `FinanceTrackerContext` to inherit from `IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>`. One context, one migration history, one connection string.

### Anti-Pattern 4: Storing JWT signing key in `appsettings.json` committed to source control

**What people do:** Paste the raw HMAC key into `appsettings.json` for convenience.

**Why it's wrong:** The key is now in version history forever. Anyone with repo access can forge tokens.

**Do this instead:** Store in `appsettings.Development.json` (gitignored), .NET User Secrets (`dotnet user-secrets set "Jwt:Key" "..."`), or environment variables. Production key should come from Azure Key Vault or equivalent.

---

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| Google OAuth2 | `AddGoogle(clientId, clientSecret)` via `Microsoft.AspNetCore.Authentication.Google` NuGet | Callback URL must match Google Console redirect URI exactly; default path `/signin-google` can be overridden via `options.CallbackPath` |
| SQL Server (Identity tables) | EF Core migrations via `IdentityDbContext` | Identity tables created automatically by running migrations — no manual SQL needed |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| `AuthController` ↔ `UserManager<ApplicationUser>` | Direct DI injection — `UserManager` is registered by `AddIdentityCore` | `UserManager` is thread-safe and `Scoped` by default |
| `AuthController` ↔ `IJwtTokenService` | Interface injection | Keeps controller testable; mock `IJwtTokenService` in unit tests |
| MediatR Handlers ↔ `ICurrentUserService` | Interface injection | Handler tests inject a stub `ICurrentUserService` with a fixed UserId |
| `FinanceTrackerContext` ↔ Identity tables | `IdentityDbContext` base class manages Identity entity sets | EF Fluent config for `Transaction` and `Category` continues via `ApplyConfigurationsFromAssembly` |
| `DataSeeder` ↔ `UserManager` | Direct injection in startup; runs before `app.Run()` | Must use `CreateScope()` pattern since `UserManager` is Scoped and seeder runs at host startup |

---

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| 0–1k users (current) | Single process, HS256 JWT, SQL Server — no changes needed |
| 1k–100k users | JWT signing key rotation via refresh tokens; consider RS256 (asymmetric) for multi-service scenarios |
| 100k+ users | Dedicated auth service (IdentityServer / Duende / Keycloak); current in-process JWT issuance becomes a liability at high load |

**First bottleneck:** Token validation on every request is in-memory (no DB hit for JWT validation) — this is fine at scale. First bottleneck will be SQL Server transaction queries, not auth.

---

## Sources

- Microsoft Learn — Configure JWT Bearer Authentication (ASP.NET Core): https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication
- Microsoft Learn — Identity model customization (IdentityDbContext): https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model
- Microsoft Learn — Google external login setup in ASP.NET Core: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/google-logins
- Stack Overflow — AddIdentity breaks JWT authentication (AddIdentityCore recommendation): https://stackoverflow.com/questions/46323844
- Stack Overflow — Extend scaffolded DbContext to use IdentityDbContext: https://stackoverflow.com/questions/77471433

---
*Architecture research for: Finance Tracker API — v1.1 Authentication & Authorization*
*Researched: 2026-04-25*
