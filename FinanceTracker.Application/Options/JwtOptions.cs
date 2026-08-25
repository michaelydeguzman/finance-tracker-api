namespace FinanceTracker.Application.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "finance-tracker-api";

    public string Audience { get; set; } = "finance-tracker-ui";

    /// <summary>
    /// Symmetric signing key, from user-secrets or the environment — never appsettings.
    /// Must be at least 32 bytes; HMAC-SHA256 keys shorter than the digest weaken the
    /// signature, and the token handler rejects them outright.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Deliberately short. The access token carries no revocation check — invalidation
    /// happens by expiry, so the window between a password change and every outstanding
    /// token dying is exactly this long.
    /// </summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Matches the sign-in session length in the front end.</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
