# Feature Research

**Domain:** Authentication & Authorization — REST API (ASP.NET Core / JWT / Google OAuth2)
**Researched:** 2026-04-25
**Confidence:** HIGH (Microsoft Docs, Google Docs, verified 2026 sources)

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features that any authenticated REST API must have. Missing any of these means the auth system is broken or untrustworthy.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **User registration (email + password)** | Entry point — without it nothing works | LOW | ASP.NET Core Identity `UserManager.CreateAsync`; hash password via Identity default (PBKDF2). Auto-verify: set `EmailConfirmed = true` on creation to skip email step. |
| **User login returning JWT** | Authenticated callers need a token to call protected endpoints | LOW | `SignInManager.CheckPasswordSignInAsync` → build JWT with `sub`, `email`, `jti` claims → return as bearer token in response body. Never issue from raw password without Identity validation. |
| **JWT expiry** | Tokens must not last forever | LOW | Set `expires` claim. 15–60 min for access tokens is standard. For v1.1 with no refresh token, lean toward longer (e.g. 60 min) to avoid logout UX issues. |
| **JWT validation middleware** | All protected endpoints must reject unauthenticated requests | LOW | `AddJwtBearer` in DI; configure `TokenValidationParameters` with strict issuer, audience, lifetime, signing key. Set `ClockSkew = TimeSpan.Zero`. Call `UseAuthentication()` before `UseAuthorization()`. |
| **401 for unauthenticated requests** | Standard HTTP contract — callers must know to send a token | LOW | Default behavior with `[Authorize]` or `RequireAuthorization()`. Ensure challenge returns `401`, not a redirect (important: `AddGoogle()` middleware would cause redirect — use `JwtBearer` only). |
| **403 for authenticated but unauthorized** | Differentiate "you need to log in" from "you're not allowed" | LOW | ASP.NET Core default when `[Authorize(Policy = ...)]` fails for authenticated user. For v1.1's flat auth, primarily 401 matters. |
| **Per-user data isolation** | Users must only see their own data | MEDIUM | Extract `userId` from JWT claims in query handlers. All queries/commands filter by `UserId`. Must cover: list, create, update, delete. Applies to transactions (and categories if user-scoped). |
| **Secure password storage** | Users expect passwords not stored in plaintext | LOW | ASP.NET Core Identity handles this (PBKDF2 with salt by default). Never store raw passwords. |
| **Duplicate email prevention** | Registering the same email twice should fail clearly | LOW | Identity's `UserManager.CreateAsync` returns `IdentityResult` with error if email taken. Return 409 Conflict or 400 with clear message. |
| **Structured error responses on auth failure** | Clients need machine-readable failures | LOW | Return consistent error shape: `{ "error": "invalid_credentials", "message": "..." }`. Don't leak whether email exists or not (prevents user enumeration). |
| **Seed admin user** | Dev/test environments need a known login | LOW | `IHostedService` or `DatabaseSeeder` on startup: check if admin email exists, create with known password if not. Idempotent. Assign existing transactions to seed admin `UserId`. |
| **Existing data migrated to seed admin** | Data integrity — existing transactions must not become orphaned | LOW | EF Core migration sets `UserId` FK on transaction rows to seed admin's ID. Must run before `UserId` is made non-nullable. |

---

### Differentiators (Competitive Advantage)

Features beyond bare minimum — add real value for this milestone's goals.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Google OAuth2 SSO — id_token validation flow** | Zero-friction login for users with Google accounts; no password management needed | MEDIUM | **REST API pattern (not browser redirect):** Client handles Google login UI (e.g., Google Identity Services JS SDK) and obtains an `id_token`. Client POSTs `id_token` to `/api/auth/google`. Backend validates via `Google.Apis.Auth.GoogleJsonWebSignature.ValidateAsync()`. Extract `sub` (stable Google user ID) and `email`. Check `AspNetUserLogins` for existing `(provider="Google", providerKey=sub)`. If found → issue JWT. If not → create new `ApplicationUser` (set `EmailConfirmed = true`) + add login record → issue JWT. **Do not use `AddGoogle()` middleware** — that's for browser redirect flows and will return 302 instead of 401. |
| **Account linking: Google ↔ email/password** | User with existing email/password account can also log in via Google | MEDIUM | If Google `email` matches existing local account with no Google login: add `UserLogin` record linking Google `sub` to existing user. If email doesn't match any local account: create new user. Do NOT silently merge unless emails match (trust boundary). |
| **Stable Google sub as link key** | Emails can change; `sub` cannot | LOW | Store `sub` (not email) in `AspNetUserLogins.ProviderKey`. Use email only for user creation display name / lookup fallback, never as sole identifier across providers. |
| **Claims-rich JWT** | Downstream features (per-user data, future roles) rely on claims | LOW | Include: `sub` (userId), `email`, `jti` (unique token ID for future revocation), `iat`, `exp`. Keep payload lean — no sensitive data. |
| **Idempotent seeding** | Startup doesn't fail or duplicate data on repeated runs | LOW | Seed checks existence before insert. Critical for integration test environments. |

---

### Anti-Features (Deliberately NOT Building in v1.1)

Features that seem natural to add but should be deferred or avoided.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| **Refresh tokens** | Access tokens expire; users shouldn't have to re-login constantly | Adds significant complexity: secure storage, rotation, revocation, replay detection, database table, rotation endpoint. For a personal finance app in v1.1 with no mobile client yet, operational cost outweighs benefit. | Longer-lived access tokens (60 min) in v1.1. Revisit when mobile clients are added or users report session friction. |
| **Email verification on registration** | Security best practice | Requires email sending infrastructure (SMTP/SendGrid), tokenized verification link, expiry, re-send flow. Over-engineering for a personal app v1.1 with a known user base. | Auto-verify (`EmailConfirmed = true`) on registration. Defer email flows to future milestone. |
| **Password reset via email** | Users forget passwords | Same email infrastructure problem as above. | Seed admin can reset manually in dev. Defer to future milestone. |
| **Role-based authorization (admin vs user)** | Admin panel, data management tools | Adds per-endpoint policy complexity. Flat "authenticated = authorized" is sufficient for v1.1. | Single `[Authorize]` everywhere. Seed admin is just the test user, not a privileged role. Defer RBAC to a future milestone. |
| **Account lockout policy (brute force)** | Security best practice for login endpoint | ASP.NET Core Identity has lockout built-in, but tuning requires decisions around lockout duration, unlock policy, admin unlock. For a personal app with one user this is noise. | Rate limiting on `/login` endpoint (simple, no state). Defer lockout config. |
| **Token blacklist / revocation** | Logout should immediately invalidate the token | Requires distributed cache (Redis) or DB table, TTL management. Personal app with known single user — session expiry on logout is acceptable. | Client discards token on logout. Accept that token remains technically valid until expiry. Document the decision. |
| **Household / shared access** | Multiple users viewing same transactions | Different data model (shared ownership), not just auth scoping. | Out of scope per PROJECT.md. Keep `UserId` ownership 1:1 per transaction for now. |
| **OAuth2 for Apple / GitHub / Facebook** | More SSO options | Each provider requires separate client registration, credential management, and subtle flow differences. Google covers the primary use case. | Google only in v1.1. Abstract provider logic so additional providers are addable later. |

---

## Feature Dependencies

```
[User Registration]
    └──enables──> [JWT Login]
    └──enables──> [Seed Admin User]

[JWT Login]
    └──requires──> [User Registration]
    └──enables──> [JWT Middleware Protection]
    └──enables──> [Per-User Data Isolation]

[JWT Middleware Protection]
    └──requires──> [JWT Login] (token must exist to validate)
    └──enables──> [Per-User Data Isolation] (userId claim available in handlers)

[Per-User Data Isolation]
    └──requires──> [JWT Middleware Protection]
    └──requires──> [Seed Admin User] (existing data must have an owner)
    └──requires──> [Existing Data Migration] (transactions need UserId FK before isolation enforced)

[Seed Admin User]
    └──enables──> [Existing Data Migration]
    └──enables──> [Integration Testing] (known credentials for test auth)

[Google OAuth2 SSO]
    └──requires──> [User Registration] (shares same ApplicationUser model)
    └──requires──> [JWT Login] (issues same JWT as password login)
    └──optional──> [Account Linking] (enhances SSO for users with both auth methods)
```

### Dependency Notes

- **JWT Middleware requires JWT Login:** The middleware validates tokens issued by login; both must use the same signing key and claims shape.
- **Per-User Data Isolation requires Seed Admin + Migration:** Without assigning existing transactions to a known user, any isolation query will produce empty results or FK violations.
- **Google SSO reuses the JWT issuance path:** After validating the Google `id_token`, the same JWT generation code used for password login should issue the app JWT — single code path, not two divergent ones.
- **Account Linking is optional at v1.1:** It can be implemented as a bonus within the Google SSO phase since the detection (email lookup) happens anyway. Add `UserManager.AddLoginAsync()` if email match found.

---

## Expected Behaviors (Detailed)

### Registration: `POST /api/auth/register`

| Scenario | Expected Response |
|----------|------------------|
| Valid email + password | 201 Created — user created, `EmailConfirmed = true` |
| Email already taken | 409 Conflict — generic message (don't leak existence) |
| Weak password (fails Identity rules) | 400 Bad Request — list of Identity password errors |
| Missing fields | 400 Bad Request — model validation errors |

Password rules: ASP.NET Core Identity defaults (8+ chars, uppercase, lowercase, digit, special char). Consider relaxing for dev ergonomics — document the decision.

---

### Login: `POST /api/auth/login`

| Scenario | Expected Response |
|----------|------------------|
| Valid credentials | 200 OK — `{ "token": "eyJ...", "expiresAt": "ISO8601" }` |
| Wrong password | 401 Unauthorized — generic "invalid credentials" (no distinction) |
| Unknown email | 401 Unauthorized — same generic message (prevent user enumeration) |
| Account not found | 401 Unauthorized |

JWT payload claims: `sub` (userId as string), `email`, `jti` (GUID), `iat`, `exp`.

---

### Google SSO: `POST /api/auth/google`

| Scenario | Expected Response |
|----------|------------------|
| Valid `id_token`, new Google user | 200 OK — new user created + JWT issued |
| Valid `id_token`, returning Google user | 200 OK — JWT issued for existing user |
| Valid `id_token`, email matches existing email/password user | 200 OK — Google login linked to existing user + JWT issued |
| Invalid / expired `id_token` | 401 Unauthorized |
| Missing `id_token` in body | 400 Bad Request |

Request body: `{ "idToken": "eyJ..." }` (the Google-issued `id_token`, not the app JWT).

Validation: `GoogleJsonWebSignature.ValidateAsync(idToken, new ValidationSettings { Audience = new[] { clientId } })`. Validate `Audience` matches configured Google `clientId` — critical security check.

---

### Protected Endpoints

| Scenario | Expected Response |
|----------|------------------|
| Request with valid JWT | 200 OK (or appropriate success) |
| Request without `Authorization` header | 401 Unauthorized |
| Request with expired JWT | 401 Unauthorized |
| Request with tampered JWT | 401 Unauthorized |
| Request with valid JWT but accessing another user's data | 404 Not Found (preferred over 403 — don't confirm existence) |

All existing `GET /api/v{version}/transactions` and related endpoints must require `[Authorize]`. Unauthenticated requests must return 401, not redirect.

---

### Per-User Data Isolation

| Scope | Behavior |
|-------|----------|
| Transaction list | Only return transactions where `UserId == currentUserId` |
| Transaction create | Set `UserId = currentUserId` on new transaction |
| Transaction update/delete | Only allow if `UserId == currentUserId`; 404 if not found or wrong owner |
| Categories / Frequencies | If user-scoped: same pattern. If global lookup tables: no scoping needed |

`currentUserId` extracted from JWT claim: `User.FindFirstValue(ClaimTypes.NameIdentifier)` or `User.FindFirstValue("sub")` — whichever is set during token generation. Must be consistent.

---

## MVP Definition

### Launch With (v1.1)

- [x] User registration (email/password, auto-verified) — core entry point
- [x] User login returning JWT — enables all API access
- [x] JWT middleware protecting all endpoints — enforces auth everywhere
- [x] Per-user data isolation on transactions — core security requirement
- [x] Seed admin user with existing transactions — data integrity, test enablement
- [x] Google OAuth2 SSO (id_token validation) — per milestone scope

### Add After Validation (v1.x)

- [ ] Refresh tokens — when mobile client demand materializes or session complaints emerge
- [ ] Email verification + password reset — when user base grows beyond single-person use
- [ ] Account lockout policy — when public exposure warrants brute force protection

### Future Consideration (v2+)

- [ ] Role-based authorization — when admin panel or multi-tier access is needed
- [ ] Household / shared transaction access — different ownership model required
- [ ] Additional OAuth providers (Apple, GitHub) — when user research shows demand

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| User registration | HIGH | LOW | P1 |
| User login + JWT | HIGH | LOW | P1 |
| JWT middleware protection | HIGH | LOW | P1 |
| Per-user data isolation | HIGH | MEDIUM | P1 |
| Seed admin + data migration | HIGH | LOW | P1 |
| Google OAuth2 SSO | HIGH | MEDIUM | P1 |
| Account linking (Google ↔ local) | MEDIUM | LOW (bonus if detecting anyway) | P1 (fold into SSO) |
| Refresh tokens | MEDIUM | HIGH | P3 (defer) |
| Email verification | LOW | HIGH | P3 (defer) |
| Token revocation / blacklist | LOW | HIGH | P3 (defer) |
| Role-based authorization | LOW | MEDIUM | P3 (defer) |

---

## Sources

- [Microsoft Docs: Configure JWT bearer authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0) — HIGH confidence
- [Microsoft Docs: External login providers with Identity in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/?view=aspnetcore-10.0) — HIGH confidence
- [Google Developers: OAuth 2.0 Protocols](https://developers.google.com/identity/protocols/oauth2) — HIGH confidence
- [ASP.NET Core + JWT + OAuth 2.0 — Authentication Done Right in 2026](https://medium.com/@123ajaybisht/asp-net-core-jwt-oauth-2-0-authentication-done-right-in-2026-59ba0be1397f) — MEDIUM confidence (community article, 2026)
- [JWT Generation, Key Management, and Token Lifecycle in ASP.NET Core (Mar 2026)](https://medium.com/@quentinsims89/inside-the-jwt-generation-key-management-and-token-lifecycle-in-asp-net-core-af3902e83404) — MEDIUM confidence (community article, 2026)
- [Google OAuth2 code-flow approach for SPA + .NET Core backend — StackOverflow](https://stackoverflow.com/questions/77672733/google-oauth2-code-flow-approach-for-spa-net-core-backend-implementation) — MEDIUM confidence

---
*Feature research for: Authentication & Authorization — ASP.NET Core REST API*
*Researched: 2026-04-25*
