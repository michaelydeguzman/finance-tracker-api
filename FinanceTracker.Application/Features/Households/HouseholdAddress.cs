namespace FinanceTracker.Application.Features.Households;

/// <summary>
/// Email handling for invitations, matching what <c>AuthService</c> does to the addresses it
/// stores. An invitation is matched against <c>User.Email</c>, which is normalized on write,
/// so an invitation normalized any differently would simply never be found by its recipient.
/// </summary>
internal static class HouseholdAddress
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// The same shallow shape check the auth controller applies. Deliberately not a full
    /// RFC validation: the address is proved by whether the invitation ever reaches someone
    /// signed in as it, not by a regex.
    /// </summary>
    public static bool LooksLikeAnEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var at = value.IndexOf('@');
        return at > 0 && at < value.Length - 1 && value.IndexOf('@', at + 1) < 0;
    }
}
