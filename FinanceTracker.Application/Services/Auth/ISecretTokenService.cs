using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Services.Auth;

/// <summary>The plaintext handed to the user, plus the row recording its hash.</summary>
public sealed record IssuedSecretToken(string PlainText, UserToken Record);

public interface ISecretTokenService
{
    /// <summary>
    /// Mints a token for one purpose. The returned plaintext is the only time it exists in
    /// readable form — only its hash is persisted.
    /// </summary>
    IssuedSecretToken Issue(Guid userId, UserTokenPurpose purpose, TimeSpan lifetime);

    /// <summary>Hash used for lookup, so a stored token cannot be replayed from a backup.</summary>
    string HashFor(string plainText);
}
