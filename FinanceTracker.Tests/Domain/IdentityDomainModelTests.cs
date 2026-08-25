using FinanceTracker.Application.Services;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Domain;

public class IdentityDomainModelTests
{
    private static DbContextOptions<FinanceTrackerContext> NewOptions(string prefix) =>
        new DbContextOptionsBuilder<FinanceTrackerContext>()
            .UseInMemoryDatabase($"{prefix}_{Guid.NewGuid()}")
            .Options;

    private static User NewUser(string email = "person@example.com") => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        DisplayName = "Test Person",
        Status = UserStatus.Active,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public void User_DefaultsToActiveAndUnverified()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "new@example.com" };

        user.Status.Should().Be(UserStatus.Active);
        user.EmailVerifiedAt.Should().BeNull(
            "a freshly created account has not proven ownership of its address yet");
    }

    [Fact]
    public void UserStatus_HasExactlyTwoValues()
    {
        Enum.GetValues<UserStatus>().Should().BeEquivalentTo(
            new[] { UserStatus.Active, UserStatus.Disabled });
    }

    [Fact]
    public void IdentityProvider_CoversPasswordAndBothSsoProviders()
    {
        Enum.GetValues<IdentityProvider>().Should().BeEquivalentTo(
            new[] { IdentityProvider.Password, IdentityProvider.Google, IdentityProvider.GitHub });
    }

    [Fact]
    public void UserTokenPurpose_CoversAllThreeEmailedFlows()
    {
        Enum.GetValues<UserTokenPurpose>().Should().BeEquivalentTo(
            new[]
            {
                UserTokenPurpose.EmailVerification,
                UserTokenPurpose.PasswordReset,
                UserTokenPurpose.MagicLink
            });
    }

    [Fact]
    public async Task User_CanHoldBothAPasswordAndAnSsoIdentity()
    {
        // The reason identities are a separate table: signing in with Google must land on
        // the same account as signing in with the password, not create a second one.
        var options = NewOptions("LinkedIdentities");
        var user = NewUser();

        using (var context = new FinanceTrackerContext(options))
        {
            context.Users.Add(user);
            context.UserIdentities.AddRange(
                new UserIdentity
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    User = user,
                    Provider = IdentityProvider.Password,
                    ProviderSubject = user.Id.ToString()
                },
                new UserIdentity
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    User = user,
                    Provider = IdentityProvider.Google,
                    ProviderSubject = "google-oidc-subject-123"
                });

            await context.SaveChangesAsync();
        }

        using (var context = new FinanceTrackerContext(options))
        {
            var identities = await context.UserIdentities
                .Where(i => i.UserId == user.Id)
                .ToListAsync();

            identities.Should().HaveCount(2);
            identities.Select(i => i.Provider).Should().BeEquivalentTo(
                new[] { IdentityProvider.Password, IdentityProvider.Google });
        }
    }

    [Fact]
    public async Task UserCredential_IsOptionalSoSsoOnlyAccountsStoreNoPassword()
    {
        var options = NewOptions("SsoOnly");
        var user = NewUser("sso-only@example.com");

        using (var context = new FinanceTrackerContext(options))
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        using (var context = new FinanceTrackerContext(options))
        {
            var loaded = await context.Users
                .Include(u => u.Credential)
                .SingleAsync(u => u.Id == user.Id);

            loaded.Credential.Should().BeNull();
        }
    }

    [Fact]
    public async Task UserToken_RoundTripsExpiryAndConsumption()
    {
        var options = NewOptions("Tokens");
        var user = NewUser("token@example.com");
        var expiresAt = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var tokenId = Guid.NewGuid();

        using (var context = new FinanceTrackerContext(options))
        {
            context.Users.Add(user);
            context.UserTokens.Add(new UserToken
            {
                Id = tokenId,
                UserId = user.Id,
                User = user,
                Purpose = UserTokenPurpose.PasswordReset,
                TokenHash = "hash-of-the-emailed-secret",
                ExpiresAt = expiresAt
            });

            await context.SaveChangesAsync();
        }

        using (var context = new FinanceTrackerContext(options))
        {
            var token = await context.UserTokens.SingleAsync(t => t.Id == tokenId);

            token.ExpiresAt.Should().Be(expiresAt);
            token.ConsumedAt.Should().BeNull("an unredeemed token is not yet spent");

            token.ConsumedAt = new DateTime(2026, 3, 1, 11, 0, 0, DateTimeKind.Utc);
            await context.SaveChangesAsync();
        }

        using (var context = new FinanceTrackerContext(options))
        {
            var token = await context.UserTokens.SingleAsync(t => t.Id == tokenId);
            token.ConsumedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public void RequireUserId_ThrowsWhenNoIdentityIsInScope()
    {
        // Fail-closed: a write with no tenant must fault the request rather than
        // quietly produce an unowned financial record.
        ICurrentUserAccessor accessor = new TestCurrentUserAccessor(null);

        accessor.Invoking(a => a.RequireUserId())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RequireUserId_ReturnsTheSignedInUser()
    {
        ICurrentUserAccessor accessor = new TestCurrentUserAccessor();

        accessor.RequireUserId().Should().Be(TestCurrentUserAccessor.DefaultUserId);
    }
}
