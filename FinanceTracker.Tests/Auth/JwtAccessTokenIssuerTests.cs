using System.Text;
using FinanceTracker.Application.Options;
using FinanceTracker.Application.Services.Auth;
using FinanceTracker.Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FinanceTracker.Tests.Auth;

/// <summary>
/// Issues a token and validates it with the same parameters the API pipeline uses, so the
/// issuing and reading halves are checked against each other rather than in isolation.
/// A claim-name mismatch between them silently breaks tenant resolution.
/// </summary>
public class JwtAccessTokenIssuerTests
{
    private const string SigningKey = "test-signing-key-that-is-long-enough-for-hmac-sha256";

    private static JwtOptions Options() => new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = SigningKey,
        AccessTokenMinutes = 15
    };

    private static User AUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "person@example.com",
        EmailVerifiedAt = DateTime.UtcNow
    };

    private static TokenValidationParameters ValidationParameters(JwtOptions options) => new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = options.Issuer,
        ValidAudience = options.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
        ClockSkew = TimeSpan.Zero,
        NameClaimType = JwtAccessTokenIssuer.UserIdClaim
    };

    [Fact]
    public async Task IssuedToken_ValidatesAndCarriesTheUserIdUnderTheExpectedClaim()
    {
        var options = Options();
        var user = AUser();

        var token = new JwtAccessTokenIssuer(Microsoft.Extensions.Options.Options.Create(options)).Issue(user);

        var result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token.Value, ValidationParameters(options));

        result.IsValid.Should().BeTrue();
        result.Claims[JwtAccessTokenIssuer.UserIdClaim].Should().Be(user.Id.ToString());
        result.Claims["email_verified"].Should().Be(true);
    }

    [Fact]
    public async Task TokenSignedWithADifferentKey_IsRejected()
    {
        var options = Options();
        var token = new JwtAccessTokenIssuer(Microsoft.Extensions.Options.Options.Create(options)).Issue(AUser());

        var otherKey = Options();
        otherKey.SigningKey = "a-completely-different-key-also-long-enough-yes";

        var result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token.Value, ValidationParameters(otherKey));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ExpiredToken_IsRejectedWithNoGracePeriod()
    {
        // ClockSkew defaults to five minutes, which would quietly extend every token well
        // past the window it was issued for.
        var options = Options();
        options.AccessTokenMinutes = -1;

        var token = new JwtAccessTokenIssuer(Microsoft.Extensions.Options.Options.Create(options)).Issue(AUser());

        var result = await new JsonWebTokenHandler()
            .ValidateTokenAsync(token.Value, ValidationParameters(options));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void MissingSigningKey_FailsLoudlyRatherThanIssuingUnsignedTokens()
    {
        var options = Options();
        options.SigningKey = string.Empty;

        var act = () => new JwtAccessTokenIssuer(Microsoft.Extensions.Options.Options.Create(options));

        act.Should().Throw<InvalidOperationException>().WithMessage("*SigningKey*");
    }
}
