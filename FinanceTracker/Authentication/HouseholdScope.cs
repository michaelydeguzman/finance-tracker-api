namespace FinanceTracker.API.Authentication
{
    /// <summary>
    /// The current request's household membership, resolved once and read many times.
    ///
    /// Scoped state rather than a lazy lookup inside <see cref="HttpContextCurrentUserAccessor"/>
    /// on purpose. The tenancy query filters read the accessor while EF is building a query,
    /// so a lookup hidden behind that property would have to hit the database from inside the
    /// context that is mid-query — synchronously, and re-entrantly. Resolving it up front in
    /// <see cref="HouseholdScopeMiddleware"/> costs one scalar query per authenticated request
    /// and leaves the accessor a pure read.
    /// </summary>
    public sealed class HouseholdScope
    {
        public Guid? HouseholdId { get; private set; }

        /// <summary>
        /// Whether the lookup has run. Distinguishes "not in a household" from "never asked" —
        /// which matters only for diagnosing a pipeline where the middleware did not run, since
        /// both read as null and both fail closed.
        /// </summary>
        public bool Resolved { get; private set; }

        public void Set(Guid? householdId)
        {
            HouseholdId = householdId;
            Resolved = true;
        }
    }
}
