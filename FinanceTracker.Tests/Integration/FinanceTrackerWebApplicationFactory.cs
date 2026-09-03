using System.Net.Http.Headers;
using FinanceTracker.Application.Options;
using FinanceTracker.Application.Services.Auth;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// Hosts the API with EF Core InMemory instead of SQL Server for deterministic integration tests.
///
/// Identity is not stubbed: the factory signs a genuine JWT with the same key the host
/// validates against, so requests travel the real authentication pipeline. Stubbing
/// <c>ICurrentUserAccessor</c> here would leave the bearer setup, the claim names and the
/// tenancy filters untested — which is most of what these tests exist to cover.
/// </summary>
public sealed class FinanceTrackerWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string SigningKey = "integration-test-signing-key-long-enough-for-hmac-sha256";
    private const string Issuer = "finance-tracker-api";
    private const string Audience = "finance-tracker-ui";

    private readonly string _databaseName = $"FinanceTracker_Integration_{Guid.NewGuid():N}";

    /// <summary>The tenant every request from <see cref="CreateAuthenticatedClient"/> belongs to.</summary>
    public Guid DefaultUserId { get; } = Guid.NewGuid();

    public string DefaultUserEmail => "integration@example.com";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}"] = SigningKey,
                [$"{JwtOptions.SectionName}:{nameof(JwtOptions.Issuer)}"] = Issuer,
                [$"{JwtOptions.SectionName}:{nameof(JwtOptions.Audience)}"] = Audience,
                [$"{AuthOptions.SectionName}:{nameof(AuthOptions.BffSharedSecret)}"] = "integration-bff-secret",

                // The suite issues far more invitations in a few seconds than any household
                // ever would, from a single client address. Raised so the household tests
                // exercise household rules rather than the rate limiter.
                [$"{AuthOptions.SectionName}:{nameof(AuthOptions.HouseholdInvitesPerMinute)}"] = "1000",

                // Never a real provider from a test run.
                [$"{EmailOptions.SectionName}:{nameof(EmailOptions.Provider)}"] = nameof(EmailProvider.Logging)
            });
        });

        builder.ConfigureServices(services =>
        {
            RemoveServiceDescriptors(services, typeof(DbContextOptions<FinanceTrackerContext>));
            RemoveServiceDescriptors(services, typeof(FinanceTrackerContext));

            services.AddDbContext<FinanceTrackerContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    /// <summary>A client whose requests carry a valid bearer token for <see cref="DefaultUserId"/>.</summary>
    public HttpClient CreateAuthenticatedClient() => CreateClientFor(DefaultUserId, DefaultUserEmail);

    /// <summary>A client signed in as someone else — for asserting that tenants cannot see each other.</summary>
    public HttpClient CreateClientFor(Guid userId, string email)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", IssueTokenFor(userId, email));
        return client;
    }

    public string IssueTokenFor(Guid userId, string email)
    {
        var issuer = new JwtAccessTokenIssuer(Options.Create(new JwtOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKey = SigningKey,
            AccessTokenMinutes = 30
        }));

        return issuer.Issue(new User { Id = userId, Email = email, EmailVerifiedAt = DateTime.UtcNow }).Value;
    }

    /// <summary>
    /// Plants a real <c>User</c> row. Most tests need only a token, but anything touching
    /// households needs the account behind it to exist: membership hangs off the user record,
    /// and accepting an invitation checks the address has been confirmed.
    /// </summary>
    public async Task SeedUserAsync(Guid userId, string email, bool emailVerified = true)
    {
        await SeedAsync(async context =>
        {
            if (await context.Users.AnyAsync(u => u.Id == userId))
                return;

            context.Users.Add(new User
            {
                Id = userId,
                Email = email,
                EmailVerifiedAt = emailVerified ? DateTime.UtcNow : null,
                DisplayName = email,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });
    }

    /// <summary>Seeds directly, bypassing the tenancy filter so a test can plant another tenant's data.</summary>
    public async Task SeedAsync(Func<FinanceTrackerContext, Task> seed)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FinanceTrackerContext>();
        await seed(context);
    }

    private static void RemoveServiceDescriptors(IServiceCollection services, Type serviceType)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == serviceType).ToList())
            services.Remove(descriptor);
    }
}
