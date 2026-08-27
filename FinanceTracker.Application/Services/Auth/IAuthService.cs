using FinanceTracker.Application.Dtos.Auth;

namespace FinanceTracker.Application.Services.Auth;

/// <summary>
/// Raised when an external sign-in would have to adopt an existing account on an email
/// address the provider has not verified. Auto-linking on an unproven address is an account
/// takeover path, so this refuses instead.
/// </summary>
public sealed class AccountLinkingConflictException : Exception
{
    public AccountLinkingConflictException(string message) : base(message) { }
}

public interface IAuthService
{
    /// <summary>
    /// Registers an account and emails a verification link. Returns nothing on purpose:
    /// the answer is identical whether or not the address was already taken, so that
    /// registration cannot be used to enumerate accounts.
    /// </summary>
    Task RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Null when the credentials do not match, whatever the reason.</summary>
    Task<AuthResultDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <exception cref="AccountLinkingConflictException">The identity may not adopt the matching account.</exception>
    Task<AuthResultDto> ExchangeExternalLoginAsync(ExternalLoginRequestDto request, CancellationToken cancellationToken = default);

    Task RequestMagicLinkAsync(EmailOnlyRequestDto request, CancellationToken cancellationToken = default);

    Task<AuthResultDto?> ConsumeMagicLinkAsync(TokenRequestDto request, CancellationToken cancellationToken = default);

    Task RequestPasswordResetAsync(EmailOnlyRequestDto request, CancellationToken cancellationToken = default);

    Task<bool> ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Emails a fresh confirmation link. The link from registration is single use, so
    /// following it, losing it, or never receiving it leaves no other way to confirm.
    /// Silent for an unknown, disabled, or already confirmed address, for the same reason
    /// registration is.
    /// </summary>
    Task RequestEmailVerificationAsync(EmailOnlyRequestDto request, CancellationToken cancellationToken = default);

    Task<bool> VerifyEmailAsync(TokenRequestDto request, CancellationToken cancellationToken = default);

    Task<AuthResultDto?> RefreshAsync(TokenRequestDto request, CancellationToken cancellationToken = default);
}
