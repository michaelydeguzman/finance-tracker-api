using System.Security.Claims;
using FinanceTracker.Application.Services.Auth;
using FinanceTracker.Domain.Repositories;

namespace FinanceTracker.API.Authentication
{
    /// <summary>
    /// Resolves the caller's household once per request, before any endpoint runs.
    ///
    /// Must sit after <c>UseAuthentication</c>, which is what puts a principal on the context,
    /// and before the endpoint, which is where the first tenancy-scoped query happens. A
    /// request that reaches an endpoint without this having run reads as "no household" —
    /// so the failure mode is a member seeing only their own records, never someone seeing
    /// records they should not.
    ///
    /// A database round trip rather than a token claim, because membership changes the moment
    /// an invitation is accepted and a claim would keep saying otherwise until the access
    /// token expired.
    /// </summary>
    public sealed class HouseholdScopeMiddleware
    {
        private readonly RequestDelegate _next;

        public HouseholdScopeMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(
            HttpContext context,
            HouseholdScope scope,
            IHouseholdRepository households)
        {
            var userId = ResolveUserId(context.User);

            if (userId is not null)
                scope.Set(await households.GetHouseholdIdForUserAsync(userId.Value, context.RequestAborted));

            await _next(context);
        }

        /// <summary>
        /// Reads the same claim <see cref="HttpContextCurrentUserAccessor"/> does. Duplicated
        /// rather than routed through the accessor because the accessor's household property
        /// is what this exists to populate.
        /// </summary>
        private static Guid? ResolveUserId(ClaimsPrincipal? principal)
        {
            var subject = principal?.FindFirstValue(JwtAccessTokenIssuer.UserIdClaim)
                ?? principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(subject, out var userId) ? userId : null;
        }
    }

    public static class HouseholdScopeMiddlewareExtensions
    {
        public static IApplicationBuilder UseHouseholdScope(this IApplicationBuilder app) =>
            app.UseMiddleware<HouseholdScopeMiddleware>();
    }
}
