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

        /// <summary>
        /// Applied to household invitations, the only endpoint outside auth that mails an
        /// address the caller names — with a household name the caller also chose in the
        /// subject line. Its own policy rather than <see cref="Auth"/> because the ceiling
        /// is configurable: a household invites a handful of people in its lifetime, while
        /// the integration suite issues twenty in a few seconds, and a limit tuned for one
        /// is wrong for the other.
        /// </summary>
        public const string HouseholdInvitations = "household-invitations";
    }
}
