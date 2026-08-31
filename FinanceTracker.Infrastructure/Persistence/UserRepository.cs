using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Persistence;

public class UserRepository : IUserRepository
{
    private readonly FinanceTrackerContext _context;

    public UserRepository(FinanceTrackerContext context) => _context = context;

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
        _context.Users
            .Include(u => u.Identities)
            .Include(u => u.Credential)
            .SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Users
            .Include(u => u.Identities)
            .Include(u => u.Credential)
            .SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<User?> GetByExternalIdentityAsync(
        IdentityProvider provider,
        string providerSubject,
        CancellationToken cancellationToken = default)
    {
        var identity = await _context.UserIdentities
            .SingleOrDefaultAsync(
                i => i.Provider == provider && i.ProviderSubject == providerSubject,
                cancellationToken);

        return identity is null ? null : await GetByIdAsync(identity.UserId, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await _context.Users.AddAsync(user, cancellationToken);

    public async Task AddIdentityAsync(UserIdentity identity, CancellationToken cancellationToken = default) =>
        await _context.UserIdentities.AddAsync(identity, cancellationToken);

    public async Task AddTokenAsync(UserToken token, CancellationToken cancellationToken = default) =>
        await _context.UserTokens.AddAsync(token, cancellationToken);

    public Task<UserToken?> GetActiveTokenAsync(
        string tokenHash,
        UserTokenPurpose purpose,
        CancellationToken cancellationToken = default) =>
        _context.UserTokens
            .SingleOrDefaultAsync(
                t => t.TokenHash == tokenHash
                  && t.Purpose == purpose
                  && t.ConsumedAt == null
                  && t.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

    public async Task ConsumeOutstandingTokensAsync(
        Guid userId,
        UserTokenPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var outstanding = await _context.UserTokens
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in outstanding)
            token.ConsumedAt = DateTime.UtcNow;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
