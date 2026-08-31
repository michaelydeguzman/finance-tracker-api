using FinanceTracker.Application.Options;
using FinanceTracker.Application.Services.Auth;
using FinanceTracker.Application.Services.Email;
using FinanceTracker.Domain.Repositories;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Auth;

/// <summary>Captures messages instead of sending them, so a test can read the link it needs.</summary>
public sealed class CapturingEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = new();

    public EmailMessage? Last => Sent.Count == 0 ? null : Sent[^1];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Wires a real <see cref="AuthService"/> over the InMemory provider. Deliberately uses the
/// genuine password hasher, token service and JWT issuer rather than mocks — the behaviour
/// worth asserting on here is cryptographic, and a mock would assert nothing.
/// </summary>
public sealed class AuthServiceHarness : IDisposable
{
    public AuthServiceHarness()
    {
        var options = new DbContextOptionsBuilder<FinanceTrackerContext>()
            .UseInMemoryDatabase($"Auth_{Guid.NewGuid()}")
            .Options;

        Context = new FinanceTrackerContext(options);
        Email = new CapturingEmailSender();

        AuthOptions = new AuthOptions
        {
            BffSharedSecret = "test-secret",
            AppBaseUrl = "https://app.test",
            MinimumPasswordLength = 12
        };

        JwtOptions = new JwtOptions
        {
            Issuer = "test-issuer",
            Audience = "test-audience",
            SigningKey = "test-signing-key-that-is-long-enough-for-hmac-sha256",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        };

        Users = new UserRepository(Context);
        SecretTokens = new SecretTokenService();

        Service = new AuthService(
            Users,
            new PasswordHashingService(),
            SecretTokens,
            new JwtAccessTokenIssuer(Options.Create(JwtOptions)),
            Email,
            Options.Create(AuthOptions),
            Options.Create(JwtOptions));
    }

    public FinanceTrackerContext Context { get; }

    public CapturingEmailSender Email { get; }

    public AuthOptions AuthOptions { get; }

    public JwtOptions JwtOptions { get; }

    public IUserRepository Users { get; }

    public ISecretTokenService SecretTokens { get; }

    public AuthService Service { get; }

    /// <summary>Pulls the token out of the link in the most recently sent email.</summary>
    public string LastEmailedToken()
    {
        var body = Email.Last?.TextBody ?? throw new InvalidOperationException("No email was sent.");
        var marker = "token=";
        var start = body.IndexOf(marker, StringComparison.Ordinal);

        if (start < 0)
            throw new InvalidOperationException($"No token in email body:\n{body}");

        start += marker.Length;
        var end = body.IndexOfAny(new[] { '\n', '\r', ' ' }, start);
        var raw = end < 0 ? body[start..] : body[start..end];

        return Uri.UnescapeDataString(raw);
    }

    public void Dispose() => Context.Dispose();
}
