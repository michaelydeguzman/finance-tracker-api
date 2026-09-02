namespace FinanceTracker.Application.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Shared secret proving a caller is the trusted front end. Guards only the external
    /// login exchange, which mints a token from a provider subject the front end has
    /// already verified — an endpoint no browser may ever reach directly.
    /// </summary>
    public string BffSharedSecret { get; set; } = string.Empty;

    /// <summary>Base URL used to build the links sent by email.</summary>
    public string AppBaseUrl { get; set; } = "http://localhost:3000";

    public int EmailVerificationHours { get; set; } = 24;

    /// <summary>
    /// Short by design: a magic link is both a login and a password-reset bypass, so its
    /// window should be minutes, not hours.
    /// </summary>
    public int MagicLinkMinutes { get; set; } = 15;

    public int PasswordResetMinutes { get; set; } = 60;

    public int MinimumPasswordLength { get; set; } = 12;

    /// <summary>
    /// How long a household invitation stays open. Lives in this section because it is the
    /// same kind of setting as the ones above — the lifetime of an offer that grants access
    /// to somebody's data — and days rather than minutes because, unlike a sign-in link, it
    /// is waiting on a person who may not have an account yet.
    /// </summary>
    public int HouseholdInvitationDays { get; set; } = 14;
}
