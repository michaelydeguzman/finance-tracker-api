using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Domain.Repositories;

public interface IUserRepository
{
    /// <summary>Looks a user up by normalized email, with identities and credential loaded.</summary>
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Resolves the account behind an external sign-in, or null if never linked.</summary>
    Task<User?> GetByExternalIdentityAsync(
        IdentityProvider provider,
        string providerSubject,
        CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    Task AddIdentityAsync(UserIdentity identity, CancellationToken cancellationToken = default);

    Task AddTokenAsync(UserToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an unexpired, unconsumed token by hash. Returns null for a token that is
    /// unknown, spent, or past its expiry — the caller cannot tell which, by design.
    /// </summary>
    Task<UserToken?> GetActiveTokenAsync(
        string tokenHash,
        UserTokenPurpose purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks every outstanding token of a purpose as consumed. Used when issuing a new one
    /// and after a password change, so an older emailed link cannot still be redeemed.
    /// </summary>
    Task ConsumeOutstandingTokensAsync(
        Guid userId,
        UserTokenPurpose purpose,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
