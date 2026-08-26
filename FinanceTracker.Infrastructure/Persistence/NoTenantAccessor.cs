using FinanceTracker.Domain.Services;

namespace FinanceTracker.Infrastructure.Persistence;

/// <summary>
/// The absence of a tenant, stated explicitly. Used by the worker, which sweeps every
/// user's templates and belongs to none of them.
///
/// With this in place the tenancy query filters match nothing, so any worker query that
/// forgets <c>IgnoreQueryFilters()</c> returns an empty set — loud in the worker's own log
/// line, and never a cross-tenant read.
/// </summary>
public sealed class NoTenantAccessor : ICurrentUserAccessor
{
    public Guid? UserId => null;
}
