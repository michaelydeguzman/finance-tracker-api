using FinanceTracker.Application.Dtos.Auth;
using FinanceTracker.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Auth;

/// <summary>
/// The emailed single-use flows and refresh rotation. These are the paths where a mistake
/// is a standing account-takeover vector rather than a bug someone notices.
/// </summary>
public class AuthTokenFlowTests
{
    private const string Password = "correct horse battery";
    private const string NewPassword = "a completely different one";
    private const string Email = "person@example.com";

    private static async Task<AuthServiceHarness> RegisteredAsync()
    {
        var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(new RegisterRequestDto { Email = Email, Password = Password });
        return h;
    }

    // --- Email verification ---

    [Fact]
    public async Task VerifyEmail_WithTheEmailedToken_MarksAddressVerified()
    {
        using var h = await RegisteredAsync();
        var token = h.LastEmailedToken();

        var succeeded = await h.Service.VerifyEmailAsync(new TokenRequestDto { Token = token });

        succeeded.Should().BeTrue();
        (await h.Context.Users.SingleAsync()).EmailVerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task VerifyEmail_WithTheSameTokenTwice_FailsTheSecondTime()
    {
        using var h = await RegisteredAsync();
        var token = h.LastEmailedToken();
        await h.Service.VerifyEmailAsync(new TokenRequestDto { Token = token });

        var replay = await h.Service.VerifyEmailAsync(new TokenRequestDto { Token = token });

        replay.Should().BeFalse("these tokens are single use");
    }

    [Fact]
    public async Task VerifyEmail_WithAnExpiredToken_Fails()
    {
        using var h = await RegisteredAsync();
        var token = h.LastEmailedToken();
        var record = await h.Context.UserTokens
            .SingleAsync(t => t.Purpose == UserTokenPurpose.EmailVerification);
        record.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await h.Context.SaveChangesAsync();

        var succeeded = await h.Service.VerifyEmailAsync(new TokenRequestDto { Token = token });

        succeeded.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-token")]
    public async Task VerifyEmail_WithGarbage_Fails(string token)
    {
        using var h = await RegisteredAsync();

        (await h.Service.VerifyEmailAsync(new TokenRequestDto { Token = token })).Should().BeFalse();
    }

    // --- Magic link ---

    [Fact]
    public async Task MagicLink_ForUnknownAddress_SendsNothingAndDoesNotThrow()
    {
        using var h = new AuthServiceHarness();

        await h.Service.RequestMagicLinkAsync(new EmailOnlyRequestDto { Email = "nobody@example.com" });

        h.Email.Sent.Should().BeEmpty("silence is what stops the endpoint enumerating accounts");
    }

    [Fact]
    public async Task MagicLink_Consumed_IssuesASessionAndVerifiesTheAddress()
    {
        using var h = await RegisteredAsync();
        await h.Service.RequestMagicLinkAsync(new EmailOnlyRequestDto { Email = Email });

        var result = await h.Service.ConsumeMagicLinkAsync(new TokenRequestDto { Token = h.LastEmailedToken() });

        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.EmailVerified.Should().BeTrue("following a link sent to the address proves control of it");
    }

    [Fact]
    public async Task MagicLink_RequestingASecond_InvalidatesTheFirst()
    {
        using var h = await RegisteredAsync();
        await h.Service.RequestMagicLinkAsync(new EmailOnlyRequestDto { Email = Email });
        var firstToken = h.LastEmailedToken();

        await h.Service.RequestMagicLinkAsync(new EmailOnlyRequestDto { Email = Email });

        var withOldToken = await h.Service.ConsumeMagicLinkAsync(new TokenRequestDto { Token = firstToken });
        withOldToken.Should().BeNull("only the most recent link should work");
    }

    [Fact]
    public async Task MagicLink_CannotBeRedeemedAsAPasswordReset()
    {
        // Purpose is part of the lookup, so a token minted for one flow is inert in another.
        using var h = await RegisteredAsync();
        await h.Service.RequestMagicLinkAsync(new EmailOnlyRequestDto { Email = Email });
        var token = h.LastEmailedToken();

        var succeeded = await h.Service.ResetPasswordAsync(
            new ResetPasswordRequestDto { Token = token, NewPassword = NewPassword });

        succeeded.Should().BeFalse();
    }

    // --- Password reset ---

    [Fact]
    public async Task ResetPassword_ChangesTheCredentialAndRotatesTheSecurityStamp()
    {
        using var h = await RegisteredAsync();
        var originalStamp = (await h.Context.UserCredentials.SingleAsync()).SecurityStamp;
        await h.Service.RequestPasswordResetAsync(new EmailOnlyRequestDto { Email = Email });

        var succeeded = await h.Service.ResetPasswordAsync(
            new ResetPasswordRequestDto { Token = h.LastEmailedToken(), NewPassword = NewPassword });

        succeeded.Should().BeTrue();
        (await h.Context.UserCredentials.SingleAsync()).SecurityStamp.Should().NotBe(originalStamp);
        (await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = NewPassword })).Should().NotBeNull();
        (await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = Password })).Should().BeNull();
    }

    [Fact]
    public async Task ResetPassword_EndsEveryOtherSession()
    {
        // A reset is the remedy for a suspected compromise, so an attacker holding a
        // refresh token must not survive it.
        using var h = await RegisteredAsync();
        var attackerSession = await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = Password });
        await h.Service.RequestPasswordResetAsync(new EmailOnlyRequestDto { Email = Email });

        await h.Service.ResetPasswordAsync(
            new ResetPasswordRequestDto { Token = h.LastEmailedToken(), NewPassword = NewPassword });

        var refreshed = await h.Service.RefreshAsync(new TokenRequestDto { Token = attackerSession!.RefreshToken });
        refreshed.Should().BeNull();
    }

    [Fact]
    public async Task ResetPassword_LetsAnSsoOnlyAccountSetItsFirstPassword()
    {
        using var h = new AuthServiceHarness();
        await h.Service.ExchangeExternalLoginAsync(new ExternalLoginRequestDto
        {
            Provider = IdentityProvider.Google,
            ProviderSubject = "google-subject-1",
            Email = Email,
            EmailVerified = true
        });
        await h.Service.RequestPasswordResetAsync(new EmailOnlyRequestDto { Email = Email });

        var succeeded = await h.Service.ResetPasswordAsync(
            new ResetPasswordRequestDto { Token = h.LastEmailedToken(), NewPassword = NewPassword });

        succeeded.Should().BeTrue();
        (await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = NewPassword })).Should().NotBeNull();
        (await h.Context.UserIdentities.CountAsync()).Should().Be(2, "a password identity is added alongside Google");
    }

    [Fact]
    public async Task ResetPassword_ForUnknownAddress_SendsNothing()
    {
        using var h = new AuthServiceHarness();

        await h.Service.RequestPasswordResetAsync(new EmailOnlyRequestDto { Email = "nobody@example.com" });

        h.Email.Sent.Should().BeEmpty();
    }

    // --- Refresh rotation ---

    [Fact]
    public async Task Refresh_ReturnsANewSession()
    {
        using var h = await RegisteredAsync();
        var session = await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = Password });

        var refreshed = await h.Service.RefreshAsync(new TokenRequestDto { Token = session!.RefreshToken });

        refreshed.Should().NotBeNull();
        refreshed!.RefreshToken.Should().NotBe(session.RefreshToken, "the token rotates on use");
    }

    [Fact]
    public async Task Refresh_WithAConsumedToken_Fails()
    {
        using var h = await RegisteredAsync();
        var session = await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = Password });
        await h.Service.RefreshAsync(new TokenRequestDto { Token = session!.RefreshToken });

        var replay = await h.Service.RefreshAsync(new TokenRequestDto { Token = session.RefreshToken });

        replay.Should().BeNull("a replayed refresh token is the signature of a stolen one");
    }
}
