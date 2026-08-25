using System.Security.Cryptography;
using System.Text;
using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Services.Auth;

public sealed class SecretTokenService : ISecretTokenService
{
    /// <summary>
    /// 256 bits. These tokens are bearer credentials that arrive over a URL with no second
    /// factor, so the value has to be far beyond guessable rather than merely unique.
    /// </summary>
    private const int TokenBytes = 32;

    public IssuedSecretToken Issue(Guid userId, UserTokenPurpose purpose, TimeSpan lifetime)
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);

        // Base64url so the value survives being pasted into a query string unescaped.
        var plainText = Base64UrlEncode(bytes);

        var record = new UserToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = null!,
            Purpose = purpose,
            TokenHash = HashFor(plainText),
            ExpiresAt = DateTime.UtcNow.Add(lifetime),
            CreatedAt = DateTime.UtcNow
        };

        return new IssuedSecretToken(plainText, record);
    }

    // Plain SHA-256, deliberately: unlike a password this is 256 bits of full-entropy
    // random, so there is no dictionary to slow down and a work factor would only add
    // latency to every verification.
    public string HashFor(string plainText) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(plainText)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
