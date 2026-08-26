using System.Security.Claims;
using System.Text;
using FinanceTracker.Application.Options;
using FinanceTracker.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FinanceTracker.Application.Services.Auth;

public sealed class JwtAccessTokenIssuer : IAccessTokenIssuer
{
    /// <summary>
    /// The claim carrying the tenant. Named explicitly and shared with the reading side,
    /// because the JWT handler's default inbound mapping rewrites <c>sub</c> to a long
    /// ClaimTypes URI — a rename that silently breaks tenant resolution if one side of the
    /// pair assumes it and the other does not. Mapping is switched off; this is the name on
    /// the wire and the name that is read.
    /// </summary>
    public const string UserIdClaim = JwtRegisteredClaimNames.Sub;

    private readonly JwtOptions _options;
    private readonly SigningCredentials _credentials;

    public JwtAccessTokenIssuer(IOptions<JwtOptions> options)
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured. Set it in user-secrets or the environment.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public AccessToken Issue(User user)
    {
        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>
            {
                [UserIdClaim] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),

                // Whether the address is proven, so the front end can gate on it without a
                // round trip. Not an authorization decision on its own.
                ["email_verified"] = user.EmailVerifiedAt is not null
            }
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new AccessToken(token, expiresAt);
    }
}
