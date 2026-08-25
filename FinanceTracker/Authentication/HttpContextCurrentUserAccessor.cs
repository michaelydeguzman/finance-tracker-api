using System.Security.Claims;
using FinanceTracker.Application.Services;

namespace FinanceTracker.API.Authentication
{
    /// <summary>
    /// Resolves the tenant from the caller's authenticated principal.
    ///
    /// Phase 2 adds JWT bearer authentication and the <c>sub</c> claim this reads. Until
    /// then no principal carries it, so <see cref="UserId"/> is null and every
    /// tenancy-scoped write fails loudly via <c>RequireUserId()</c> — the intended
    /// fail-closed behaviour while the auth stack is being built out. Reads are
    /// unaffected until the query filters land in Phase 3.
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
                var subject = _httpContextAccessor.HttpContext?.User
                    .FindFirstValue(ClaimTypes.NameIdentifier);

                return Guid.TryParse(subject, out var userId) ? userId : null;
            }
        }
    }
}
