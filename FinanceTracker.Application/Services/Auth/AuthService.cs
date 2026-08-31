using FinanceTracker.Application.Dtos.Auth;
using FinanceTracker.Application.Options;
using FinanceTracker.Application.Services.Email;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.Services.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHashingService _passwords;
    private readonly ISecretTokenService _secretTokens;
    private readonly IAccessTokenIssuer _accessTokens;
    private readonly IEmailSender _email;
    private readonly AuthOptions _authOptions;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        IUserRepository users,
        IPasswordHashingService passwords,
        ISecretTokenService secretTokens,
        IAccessTokenIssuer accessTokens,
        IEmailSender email,
        IOptions<AuthOptions> authOptions,
        IOptions<JwtOptions> jwtOptions)
    {
        _users = users;
        _passwords = passwords;
        _secretTokens = secretTokens;
        _accessTokens = accessTokens;
        _email = email;
        _authOptions = authOptions.Value;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        var email = Normalize(request.Email);
        var existing = await _users.GetByEmailAsync(email, cancellationToken);

        if (existing is not null)
        {
            // Same outward behaviour as a fresh registration, but the real owner is told
            // that someone tried — and given a way in, since they may simply have forgotten.
            var recovery = _secretTokens.Issue(
                existing.Id, UserTokenPurpose.PasswordReset, TimeSpan.FromMinutes(_authOptions.PasswordResetMinutes));

            await _users.ConsumeOutstandingTokensAsync(existing.Id, UserTokenPurpose.PasswordReset, cancellationToken);
            await _users.AddTokenAsync(recovery.Record, cancellationToken);
            await _users.SaveChangesAsync(cancellationToken);

            await _email.SendAsync(
                AuthEmailFactory.AccountAlreadyExists(existing.Email, Link("reset-password", recovery.PlainText)),
                cancellationToken);

            return;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        await _users.AddAsync(user, cancellationToken);

        await _users.AddIdentityAsync(new UserIdentity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = null!,
            Provider = IdentityProvider.Password,
            ProviderSubject = user.Id.ToString(),
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        var credential = new UserCredential
        {
            UserId = user.Id,
            User = null!,
            PasswordHash = _passwords.Hash(request.Password),
            SecurityStamp = NewSecurityStamp(),
            UpdatedAt = DateTime.UtcNow
        };
        user.Credential = credential;

        var verification = _secretTokens.Issue(
            user.Id, UserTokenPurpose.EmailVerification, TimeSpan.FromHours(_authOptions.EmailVerificationHours));

        await _users.AddTokenAsync(verification.Record, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        await _email.SendAsync(
            AuthEmailFactory.EmailVerification(user.Email, Link("verify-email", verification.PlainText)),
            cancellationToken);
    }

    public async Task<AuthResultDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(Normalize(request.Email), cancellationToken);

        if (user?.Credential is null)
        {
            // Burn the same CPU a real verification would, so an unknown address and a
            // wrong password are indistinguishable by response time.
            _passwords.SimulateVerification();
            return null;
        }

        if (user.Status != UserStatus.Active)
            return null;

        var outcome = _passwords.Verify(user.Credential.PasswordHash, request.Password);

        if (outcome == PasswordVerificationOutcome.Failed)
            return null;

        if (outcome == PasswordVerificationOutcome.SucceededNeedsRehash)
        {
            user.Credential.PasswordHash = _passwords.Hash(request.Password);
            user.Credential.UpdatedAt = DateTime.UtcNow;
        }

        return await IssueSessionAsync(user, cancellationToken);
    }

    public async Task<AuthResultDto> ExchangeExternalLoginAsync(
        ExternalLoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var linked = await _users.GetByExternalIdentityAsync(
            request.Provider, request.ProviderSubject, cancellationToken);

        if (linked is not null)
            return await IssueSessionAsync(linked, cancellationToken);

        var email = Normalize(request.Email);
        var existing = await _users.GetByEmailAsync(email, cancellationToken);

        if (existing is not null)
        {
            // An unverified provider email must never adopt an account that already exists:
            // anyone able to create an account at that provider claiming this address would
            // otherwise inherit somebody's financial records.
            if (!request.EmailVerified)
            {
                throw new AccountLinkingConflictException(
                    "This email already has an account and the provider has not verified the address.");
            }

            await _users.AddIdentityAsync(new UserIdentity
            {
                Id = Guid.NewGuid(),
                UserId = existing.Id,
                User = null!,
                Provider = request.Provider,
                ProviderSubject = request.ProviderSubject,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            // The provider vouched for the address, which is exactly the proof the
            // verification email would have produced.
            existing.EmailVerifiedAt ??= DateTime.UtcNow;

            await _users.SaveChangesAsync(cancellationToken);
            return await IssueSessionAsync(existing, cancellationToken);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            EmailVerifiedAt = request.EmailVerified ? DateTime.UtcNow : null,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        await _users.AddAsync(user, cancellationToken);
        await _users.AddIdentityAsync(new UserIdentity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = null!,
            Provider = request.Provider,
            ProviderSubject = request.ProviderSubject,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        await _users.SaveChangesAsync(cancellationToken);
        return await IssueSessionAsync(user, cancellationToken);
    }

    public async Task RequestMagicLinkAsync(EmailOnlyRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(Normalize(request.Email), cancellationToken);

        // Unknown address: return quietly. The caller is told the same thing either way.
        if (user is null || user.Status != UserStatus.Active)
            return;

        var issued = _secretTokens.Issue(
            user.Id, UserTokenPurpose.MagicLink, TimeSpan.FromMinutes(_authOptions.MagicLinkMinutes));

        await _users.ConsumeOutstandingTokensAsync(user.Id, UserTokenPurpose.MagicLink, cancellationToken);
        await _users.AddTokenAsync(issued.Record, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        await _email.SendAsync(
            AuthEmailFactory.MagicLink(user.Email, Link("magic-link", issued.PlainText)),
            cancellationToken);
    }

    public async Task<AuthResultDto?> ConsumeMagicLinkAsync(
        TokenRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await ConsumeTokenAsync(request.Token, UserTokenPurpose.MagicLink, cancellationToken);

        if (user is null)
            return null;

        // Following a link sent to the address is itself proof of control over it.
        user.EmailVerifiedAt ??= DateTime.UtcNow;

        await _users.SaveChangesAsync(cancellationToken);
        return await IssueSessionAsync(user, cancellationToken);
    }

    public async Task RequestPasswordResetAsync(
        EmailOnlyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(Normalize(request.Email), cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
            return;

        var issued = _secretTokens.Issue(
            user.Id, UserTokenPurpose.PasswordReset, TimeSpan.FromMinutes(_authOptions.PasswordResetMinutes));

        await _users.ConsumeOutstandingTokensAsync(user.Id, UserTokenPurpose.PasswordReset, cancellationToken);
        await _users.AddTokenAsync(issued.Record, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        await _email.SendAsync(
            AuthEmailFactory.PasswordReset(user.Email, Link("reset-password", issued.PlainText)),
            cancellationToken);
    }

    public async Task<bool> ResetPasswordAsync(
        ResetPasswordRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await ConsumeTokenAsync(request.Token, UserTokenPurpose.PasswordReset, cancellationToken);

        if (user is null)
            return false;

        if (user.Credential is null)
        {
            // An SSO-only account setting a password for the first time.
            user.Credential = new UserCredential
            {
                UserId = user.Id,
                User = null!,
                PasswordHash = _passwords.Hash(request.NewPassword),
                SecurityStamp = NewSecurityStamp(),
                UpdatedAt = DateTime.UtcNow
            };

            await _users.AddIdentityAsync(new UserIdentity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                User = null!,
                Provider = IdentityProvider.Password,
                ProviderSubject = user.Id.ToString(),
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
        else
        {
            user.Credential.PasswordHash = _passwords.Hash(request.NewPassword);
            user.Credential.SecurityStamp = NewSecurityStamp();
            user.Credential.UpdatedAt = DateTime.UtcNow;
        }

        // A reset is the remedy for a suspected compromise, so it has to end every other
        // session: outstanding refresh tokens die here, and access tokens expire within
        // minutes on their own.
        await _users.ConsumeOutstandingTokensAsync(user.Id, UserTokenPurpose.RefreshToken, cancellationToken);
        await _users.ConsumeOutstandingTokensAsync(user.Id, UserTokenPurpose.MagicLink, cancellationToken);

        // Reaching a reset link proves control of the address.
        user.EmailVerifiedAt ??= DateTime.UtcNow;

        await _users.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task RequestEmailVerificationAsync(
        EmailOnlyRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(Normalize(request.Email), cancellationToken);

        // Unknown, disabled, or already confirmed: return quietly. The caller is told the
        // same thing in every case, so none of those states can be read off the response.
        if (user is null || user.Status != UserStatus.Active || user.EmailVerifiedAt is not null)
            return;

        var issued = _secretTokens.Issue(
            user.Id, UserTokenPurpose.EmailVerification, TimeSpan.FromHours(_authOptions.EmailVerificationHours));

        // Retire the earlier link first: asking for a new one should invalidate the old,
        // so a confirmation link that leaked cannot outlive the request that replaced it.
        await _users.ConsumeOutstandingTokensAsync(user.Id, UserTokenPurpose.EmailVerification, cancellationToken);
        await _users.AddTokenAsync(issued.Record, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        await _email.SendAsync(
            AuthEmailFactory.EmailVerification(user.Email, Link("verify-email", issued.PlainText)),
            cancellationToken);
    }

    public async Task<bool> VerifyEmailAsync(TokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await ConsumeTokenAsync(request.Token, UserTokenPurpose.EmailVerification, cancellationToken);

        if (user is null)
            return false;

        user.EmailVerifiedAt ??= DateTime.UtcNow;
        await _users.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AuthResultDto?> RefreshAsync(TokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await ConsumeTokenAsync(request.Token, UserTokenPurpose.RefreshToken, cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
            return null;

        // Rotation: the presented token was just consumed, so a replay of it fails.
        return await IssueSessionAsync(user, cancellationToken);
    }

    /// <summary>
    /// Redeems a single-use token and returns its owner, or null when the token is unknown,
    /// already spent, or expired — the three are deliberately indistinguishable.
    /// </summary>
    private async Task<User?> ConsumeTokenAsync(
        string plainText,
        UserTokenPurpose purpose,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plainText))
            return null;

        var record = await _users.GetActiveTokenAsync(_secretTokens.HashFor(plainText), purpose, cancellationToken);

        if (record is null)
            return null;

        record.ConsumedAt = DateTime.UtcNow;

        var user = await _users.GetByIdAsync(record.UserId, cancellationToken);

        if (user is null)
            return null;

        await _users.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task<AuthResultDto> IssueSessionAsync(User user, CancellationToken cancellationToken)
    {
        var accessToken = _accessTokens.Issue(user);

        var refresh = _secretTokens.Issue(
            user.Id, UserTokenPurpose.RefreshToken, TimeSpan.FromDays(_jwtOptions.RefreshTokenDays));

        await _users.AddTokenAsync(refresh.Record, cancellationToken);
        await _users.SaveChangesAsync(cancellationToken);

        return new AuthResultDto
        {
            UserId = user.Id,
            Email = user.Email,
            EmailVerified = user.EmailVerifiedAt is not null,
            AccessToken = accessToken.Value,
            AccessTokenExpiresAt = accessToken.ExpiresAt,
            RefreshToken = refresh.PlainText
        };
    }

    private string Link(string path, string token) =>
        $"{_authOptions.AppBaseUrl.TrimEnd('/')}/{path}?token={Uri.EscapeDataString(token)}";

    private static string NewSecurityStamp() => Guid.NewGuid().ToString("N");

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
