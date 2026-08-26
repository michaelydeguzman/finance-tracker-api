using System.Security.Cryptography;
using System.Text;
using FinanceTracker.Application.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace FinanceTracker.API.Authentication
{
    /// <summary>
    /// Restricts an endpoint to the trusted front end, by way of a shared secret header.
    ///
    /// Applied to the external-login exchange, which mints a session from a provider subject
    /// the front end has already verified. Any caller able to reach it can impersonate any
    /// user, so it must never be reachable from a browser.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class BffOnlyAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public const string HeaderName = "X-Bff-Secret";

        public Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var configured = context.HttpContext.RequestServices
                .GetRequiredService<IOptions<AuthOptions>>().Value.BffSharedSecret;

            // Fail closed: an unset secret denies rather than admits everyone.
            if (string.IsNullOrWhiteSpace(configured))
            {
                context.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
                return Task.CompletedTask;
            }

            var presented = context.HttpContext.Request.Headers[HeaderName].ToString();

            if (!FixedTimeEquals(presented, configured))
                context.Result = new UnauthorizedResult();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Compares in time independent of how many leading characters match, so the secret
        /// cannot be recovered a byte at a time by measuring responses.
        /// </summary>
        private static bool FixedTimeEquals(string presented, string configured) =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(presented),
                Encoding.UTF8.GetBytes(configured));
    }
}
