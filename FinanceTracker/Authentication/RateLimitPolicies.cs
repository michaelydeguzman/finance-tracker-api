namespace FinanceTracker.API.Authentication
{
    public static class RateLimitPolicies
    {
        /// <summary>
        /// Applied to every auth endpoint. Password guessing, reset-link spraying and magic-link
        /// flooding all arrive as ordinary requests, so the ceiling is per client address rather
        /// than per account — an attacker choosing the account name is exactly the case a
        /// per-account limit fails to catch.
        /// </summary>
        public const string Auth = "auth";
    }
}
