using FinanceTracker.Domain.Entities;

namespace FinanceTracker.Application.Dtos.Auth;

public sealed class RegisterRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

public sealed class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// An SSO sign-in the front end has already completed. Sent server-to-server behind the
/// shared secret — <see cref="EmailVerified"/> is the provider's assertion, relayed, and it
/// decides whether this identity may adopt an existing account.
/// </summary>
public sealed class ExternalLoginRequestDto
{
    public IdentityProvider Provider { get; set; }
    public string ProviderSubject { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string? DisplayName { get; set; }
}

public sealed class EmailOnlyRequestDto
{
    public string Email { get; set; } = string.Empty;
}

public sealed class TokenRequestDto
{
    public string Token { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequestDto
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Issued credentials. Returned only on a genuinely successful sign-in.</summary>
public sealed class AuthResultDto
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool EmailVerified { get; init; }
    public string AccessToken { get; init; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; init; }
    public string RefreshToken { get; init; } = string.Empty;
}
