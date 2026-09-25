using FinanceTracker.Application.Dtos.Auth;
using FinanceTracker.Application.Services.Auth;
using FinanceTracker.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Auth;

public class AuthServiceTests
{
    private const string Password = "correct horse battery";
    private const string Email = "person@example.com";

    private static RegisterRequestDto Registration(string email = Email, string password = Password) =>
        new() { Email = email, Password = password, DisplayName = "Test Person" };

    // --- Registration ---

    [Fact]
    public async Task Register_CreatesUserWithPasswordIdentityAndCredential()
    {
        using var h = new AuthServiceHarness();

        await h.Service.RegisterAsync(Registration());

        var user = await h.Context.Users
            .Include(u => u.Identities)
            .Include(u => u.Credential)
            .SingleAsync();

        user.Email.Should().Be(Email);
        user.EmailVerifiedAt.Should().BeNull("registration alone does not prove the address");
        user.Credential.Should().NotBeNull();
        user.Credential!.PasswordHash.Should().NotContain(Password, "the password must never be stored in the clear");
        user.Identities.Should().ContainSingle()
            .Which.Provider.Should().Be(IdentityProvider.Password);
    }

    [Fact]
    public async Task Register_NormalizesEmailToLowercase()
    {
        using var h = new AuthServiceHarness();

        await h.Service.RegisterAsync(Registration(email: "  Person@Example.COM  "));

        (await h.Context.Users.SingleAsync()).Email.Should().Be(Email);
    }

    [Fact]
    public async Task Register_WithTakenEmail_DoesNotCreateSecondAccount()
    {
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());

        await h.Service.RegisterAsync(Registration(password: "a different password"));

        (await h.Context.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Register_WithTakenEmail_NotifiesTheRealOwnerInstead()
    {
        // The endpoint answers identically either way, so this email is what stops the
        // silence from hiding a hijack attempt from the person who owns the address.
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());
        h.Email.Sent.Clear();

        await h.Service.RegisterAsync(Registration());

        h.Email.Last!.Subject.Should().Contain("tried to create an account");
    }

    [Fact]
    public async Task Register_WithTakenEmail_DoesNotChangeTheExistingPassword()
    {
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());
        var originalHash = (await h.Context.UserCredentials.SingleAsync()).PasswordHash;

        await h.Service.RegisterAsync(Registration(password: "attacker chosen password"));

        (await h.Context.UserCredentials.SingleAsync()).PasswordHash.Should().Be(originalHash);
        var login = await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = "attacker chosen password" });
        login.Should().BeNull();
    }

    // --- Login ---

    [Fact]
    public async Task Login_WithCorrectPassword_IssuesASession()
    {
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());

        var result = await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = Password });

        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.AccessTokenExpiresAt.Should().BeCloseTo(
            DateTime.UtcNow.AddMinutes(h.JwtOptions.AccessTokenMinutes), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsNull()
    {
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());

        var result = await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = "wrong password!!" });

        result.Should().BeNull();
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsNull()
    {
        using var h = new AuthServiceHarness();

        var result = await h.Service.LoginAsync(new LoginRequestDto { Email = "nobody@example.com", Password = Password });

        result.Should().BeNull();
    }

    [Fact]
    public async Task Login_WithDisabledAccount_ReturnsNull()
    {
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());
        var user = await h.Context.Users.SingleAsync();
        user.Status = UserStatus.Disabled;
        await h.Context.SaveChangesAsync();

        var result = await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = Password });

        result.Should().BeNull();
    }

    [Fact]
    public async Task Login_StoresRefreshTokenHashedNotInPlainText()
    {
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());

        var result = await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = Password });

        var stored = await h.Context.UserTokens
            .Where(t => t.Purpose == UserTokenPurpose.RefreshToken)
            .Select(t => t.TokenHash)
            .ToListAsync();

        stored.Should().NotContain(result!.RefreshToken,
            "a leaked backup must not hand over working refresh tokens");
        stored.Should().ContainSingle().Which.Should().Be(h.SecretTokens.HashFor(result.RefreshToken));
    }

    // --- External login and account linking ---

    private static ExternalLoginRequestDto External(
        string subject = "google-subject-1",
        string email = Email,
        bool verified = true) =>
        new()
        {
            Provider = IdentityProvider.Google,
            ProviderSubject = subject,
            Email = email,
            EmailVerified = verified,
            DisplayName = "Test Person"
        };

    [Fact]
    public async Task Exchange_FirstTime_CreatesUserAndIdentity()
    {
        using var h = new AuthServiceHarness();

        var result = await h.Service.ExchangeExternalLoginAsync(External());

        result.UserId.Should().NotBeEmpty();
        result.EmailVerified.Should().BeTrue("the provider asserted the address");
        (await h.Context.UserIdentities.SingleAsync()).Provider.Should().Be(IdentityProvider.Google);
    }

    [Fact]
    public async Task Exchange_SameSubjectTwice_ReturnsTheSameAccount()
    {
        using var h = new AuthServiceHarness();
        var first = await h.Service.ExchangeExternalLoginAsync(External());

        var second = await h.Service.ExchangeExternalLoginAsync(External());

        second.UserId.Should().Be(first.UserId);
        (await h.Context.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Exchange_WithVerifiedEmailMatchingPasswordAccount_LinksToIt()
    {
        // The whole reason identities are their own table: one person, one account, two
        // ways in. Verified first — both ways in survive only for someone who proved the
        // address before the provider did.
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());
        await h.Service.VerifyEmailAsync(new VerifyEmailRequestDto { Token = h.LastEmailedToken(), Password = Password });
        var existingId = (await h.Context.Users.SingleAsync()).Id;

        var result = await h.Service.ExchangeExternalLoginAsync(External(verified: true));

        result.UserId.Should().Be(existingId);
        (await h.Context.Users.CountAsync()).Should().Be(1);
        (await h.Context.UserIdentities.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Exchange_WithUnverifiedEmailMatchingExistingAccount_IsRefused()
    {
        // Account takeover path: anyone who can create an account at a provider claiming
        // this address would otherwise inherit the financial records behind it.
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());

        var act = () => h.Service.ExchangeExternalLoginAsync(External(verified: false));

        await act.Should().ThrowAsync<AccountLinkingConflictException>();
        (await h.Context.UserIdentities.CountAsync()).Should().Be(1, "no identity should have been linked");
    }

    [Fact]
    public async Task Exchange_LinkingVerifiesAPreviouslyUnverifiedAddress()
    {
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());

        await h.Service.ExchangeExternalLoginAsync(External(verified: true));

        (await h.Context.Users.SingleAsync()).EmailVerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Exchange_AdoptingAnUnverifiedAccount_RevokesEveryEarlierWayIn()
    {
        // Pre-hijacking: a stranger registers the owner's address with a password of their
        // own before the owner ever arrives. Nobody has proved the address, so the account
        // sits unverified — until the owner signs in with a provider that vouches for it and
        // is linked to that account. If the stranger's password and sessions survived, they
        // would hold a key to everything the owner records from then on.
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());
        var strangersSession = await h.Service.LoginAsync(
            new LoginRequestDto { Email = Email, Password = Password });

        await h.Service.ExchangeExternalLoginAsync(External(verified: true));

        (await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = Password }))
            .Should().BeNull("the password was set by someone who never proved they own the address");
        (await h.Service.RefreshAsync(new TokenRequestDto { Token = strangersSession!.RefreshToken }))
            .Should().BeNull("sessions that password opened must end with it");
        (await h.Context.UserIdentities.SingleAsync()).Provider.Should().Be(IdentityProvider.Google,
            "the provider that proved the address is now the only way in");
        (await h.Context.UserCredentials.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Exchange_AdoptingAnUnverifiedAccount_StillLetsTheOwnerSetAPassword()
    {
        // Losing the stranger's password must not cost the owner the option of one. The
        // identity-row half of that — a leftover Password row would collide with the one a
        // reset adds on the unique (UserId, Provider) index — is pinned by the single-identity
        // assertion above, not here: InMemory does not enforce unique indexes.
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());
        await h.Service.ExchangeExternalLoginAsync(External(verified: true));

        await h.Service.RequestPasswordResetAsync(new EmailOnlyRequestDto { Email = Email });
        var reset = await h.Service.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Token = h.LastEmailedToken(),
            NewPassword = "the owner's own new password"
        });

        reset.Should().BeTrue();
        (await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = "the owner's own new password" }))
            .Should().NotBeNull();
    }

    // --- Pre-hijacking through the other first proofs of an address ---
    //
    // Google sign-in is one way an owner first proves their address. A magic link, a password
    // reset and the confirmation email are the others, and each has to settle the same
    // question: whatever existed before that proof was set up by someone unproven.

    [Fact]
    public async Task MagicLink_OnAnUnverifiedAccount_RevokesTheStrangersPassword()
    {
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());
        var strangersSession = await h.Service.LoginAsync(
            new LoginRequestDto { Email = Email, Password = Password });

        await h.Service.RequestMagicLinkAsync(new EmailOnlyRequestDto { Email = Email });
        var ownersSession = await h.Service.ConsumeMagicLinkAsync(new TokenRequestDto { Token = h.LastEmailedToken() });

        ownersSession.Should().NotBeNull();
        (await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = Password }))
            .Should().BeNull("the password predates the first proof of the address");
        (await h.Service.RefreshAsync(new TokenRequestDto { Token = strangersSession!.RefreshToken }))
            .Should().BeNull();
    }

    [Fact]
    public async Task MagicLink_OnAVerifiedAccount_KeepsItsPassword()
    {
        // Only the first proof settles ownership. An owner who confirmed their address and
        // later signs in by link must not lose the password they chose.
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());
        await h.Service.VerifyEmailAsync(new VerifyEmailRequestDto { Token = h.LastEmailedToken(), Password = Password });

        await h.Service.RequestMagicLinkAsync(new EmailOnlyRequestDto { Email = Email });
        await h.Service.ConsumeMagicLinkAsync(new TokenRequestDto { Token = h.LastEmailedToken() });

        (await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = Password }))
            .Should().NotBeNull();
    }

    [Fact]
    public async Task PasswordReset_OnAnUnverifiedAccount_RevokesEveryOtherWayIn()
    {
        // A reset already replaces the password. What it used to leave behind is every other
        // identity — here one a stranger created through a provider that did not vouch for
        // the address, which would have kept working after the owner took the account back.
        using var h = new AuthServiceHarness();
        var strangersIdentity = new ExternalLoginRequestDto
        {
            Provider = IdentityProvider.GitHub,
            ProviderSubject = "strangers-github",
            Email = Email,
            EmailVerified = false
        };
        await h.Service.ExchangeExternalLoginAsync(strangersIdentity);

        await h.Service.RequestPasswordResetAsync(new EmailOnlyRequestDto { Email = Email });
        (await h.Service.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Token = h.LastEmailedToken(),
            NewPassword = "the owner's own new password"
        })).Should().BeTrue();

        var act = () => h.Service.ExchangeExternalLoginAsync(strangersIdentity);
        await act.Should().ThrowAsync<AccountLinkingConflictException>(
            "the stranger's identity was removed, and an unverified provider cannot re-link");
        (await h.Service.LoginAsync(new LoginRequestDto { Email = Email, Password = "the owner's own new password" }))
            .Should().NotBeNull();
    }

    [Fact]
    public async Task VerifyEmail_WithoutThePasswordChosenAtSignUp_ConfirmsNothing()
    {
        // The confirmation email goes to the address's owner, but the password was chosen by
        // whoever registered. Clicking alone would let an owner who never signed up vouch for
        // a stranger's password without knowing it.
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());

        var confirmed = await h.Service.VerifyEmailAsync(
            new VerifyEmailRequestDto { Token = h.LastEmailedToken(), Password = "not the password" });

        confirmed.Should().BeFalse();
        (await h.Context.Users.SingleAsync()).EmailVerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task VerifyEmail_AfterAMistypedPassword_StillWorksWithTheRightOne()
    {
        // The link is spent only when it succeeds, so a typo does not cost the registrant it.
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());
        var token = h.LastEmailedToken();

        await h.Service.VerifyEmailAsync(new VerifyEmailRequestDto { Token = token, Password = "a typo" });
        var confirmed = await h.Service.VerifyEmailAsync(new VerifyEmailRequestDto { Token = token, Password = Password });

        confirmed.Should().BeTrue();
    }

    [Fact]
    public async Task RequestEmailVerification_ForAnAccountWithNoPassword_SendsNothing()
    {
        // Confirming by link needs the account's password, so an account without one proves its
        // address another way — a magic link or a provider that vouches for it — both of which
        // revoke whatever came before.
        using var h = new AuthServiceHarness();
        await h.Service.ExchangeExternalLoginAsync(new ExternalLoginRequestDto
        {
            Provider = IdentityProvider.GitHub,
            ProviderSubject = "github-subject",
            Email = Email,
            EmailVerified = false
        });

        await h.Service.RequestEmailVerificationAsync(new EmailOnlyRequestDto { Email = Email });

        h.Email.Sent.Should().BeEmpty();
    }

    // --- Cancellation ---

    /// <summary>
    /// These messages are sent after their work is committed, so the caller must not be able
    /// to cancel them. Requesting a reset consumes any outstanding one first: a send the
    /// caller could cancel would leave the owner's previous link dead and its replacement
    /// never delivered, with no way to tell that had happened.
    /// </summary>
    [Fact]
    public async Task RequestPasswordReset_SendsTheEmailWithATokenTheCallerCannotCancel()
    {
        using var h = new AuthServiceHarness();
        await h.Service.RegisterAsync(Registration());
        h.Email.Tokens.Clear();

        using var cts = new CancellationTokenSource();
        await h.Service.RequestPasswordResetAsync(new EmailOnlyRequestDto { Email = Email }, cts.Token);

        h.Email.Tokens.Should().ContainSingle();
        h.Email.Tokens[0].CanBeCanceled.Should().BeFalse("the work this message describes is already saved");
    }

    /// <summary>Registration commits the account before mailing the verification link.</summary>
    [Fact]
    public async Task Register_SendsTheVerificationEmailWithATokenTheCallerCannotCancel()
    {
        using var h = new AuthServiceHarness();

        using var cts = new CancellationTokenSource();
        await h.Service.RegisterAsync(Registration(), cts.Token);

        h.Email.Tokens.Should().ContainSingle();
        h.Email.Tokens[0].CanBeCanceled.Should().BeFalse("the account is already created");
    }
}
