using FinanceTracker.Domain.Services;
using System.Security.Claims;
using FinanceTracker.Application.Services;
using FinanceTracker.Application.Services.Auth;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FinanceTracker.API.Authentication
{
    /// <summary>
    /// Resolves the tenant from the caller's authenticated principal.
    ///
    /// Reads the <c>sub</c> claim minted by <see cref="JwtAccessTokenIssuer"/>. Inbound
    /// claim mapping is switched off in the bearer setup, so the name on the wire is the
    /// name read here; the ClaimTypes fallback covers any principal that did go through
    /// the default mapping.
    ///
    /// An unauthenticated request yields null, and every tenancy-scoped write then fails
    /// loudly via <c>RequireUserId()</c> rather than producing an unowned row.
    /// </summary>
    public sealed class HttpContextCurrentUserAccessor : ICurrentUserAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? UserId
        {
            get
            {
                var principal = _httpContextAccessor.HttpContext?.User;

                var subject = principal?.FindFirstValue(JwtAccessTokenIssuer.UserIdClaim)
                    ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                return Guid.TryParse(subject, out var userId) ? userId : null;
            }
        }

        public string? Email
        {
            get
            {
                var email = _httpContextAccessor.HttpContext?.User
                    .FindFirstValue(JwtRegisteredClaimNames.Email);

                return string.IsNullOrWhiteSpace(email) ? null : email;
            }
        }
    }
}
