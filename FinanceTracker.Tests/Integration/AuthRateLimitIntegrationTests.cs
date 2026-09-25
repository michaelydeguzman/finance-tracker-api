using System.Net;
using System.Text;
using FinanceTracker.API.Authentication;
using FinanceTracker.Application.Options;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// Every auth call reaches the API from the front end's servers, so the per-address limit is
/// one bucket shared by everyone — and the front end's anonymous account routes (forgot
/// password, magic link) feed it. If signed-in sessions drew from that bucket too, anyone on
/// the internet could exhaust it and sign every user out at their next token refresh.
/// </summary>
public class AuthRateLimitIntegrationTests : IClassFixture<FinanceTrackerWebApplicationFactory>
{
    private const string BffSecret = "integration-bff-secret";

    private readonly FinanceTrackerWebApplicationFactory _factory;

    public AuthRateLimitIntegrationTests(FinanceTrackerWebApplicationFactory factory) => _factory = factory;

    private static HttpRequestMessage BffRequest(string path, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add(BffOnlyAttribute.HeaderName, BffSecret);
        return request;
    }

    [Fact]
    public async Task ExhaustingTheLimit_DoesNotStopSessionsFromRefreshingOrSigningIn()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{AuthOptions.SectionName}:{nameof(AuthOptions.AuthRequestsPerMinute)}"] = "2"
                })));
        var client = factory.CreateClient();

        const string credentials = """{"email":"flood@example.com","password":"a sufficiently long password"}""";
        for (var i = 0; i < 2; i++)
            await client.SendAsync(BffRequest("/api/v1/auth/login", credentials));

        // The limit is real for the routes a stranger can drive…
        (await client.SendAsync(BffRequest("/api/v1/auth/login", credentials)))
            .StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // …and does not reach the ones that keep signed-in people signed in.
        (await client.SendAsync(BffRequest("/api/v1/auth/refresh", """{"token":"not-a-real-token"}""")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a bad token is refused on its merits, not throttled");
        (await client.SendAsync(BffRequest(
                "/api/v1/auth/exchange",
                """{"provider":"Google","providerSubject":"flood-subject","email":"flood-sso@example.com","emailVerified":true}""")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void EveryAuthEndpoint_CarriesTheBffOnlyFilter()
    {
        // Structural rather than behavioural. Sending an unauthenticated request and expecting
        // a 401 cannot tell the filter apart from an endpoint that rejects a bad token or bad
        // credentials on its own — which is most of them — so a route that lost the filter
        // would still pass. This also covers auth actions added later, which a hand-written
        // list of routes would not.
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.Contains("/auth", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        endpoints.Should().NotBeEmpty();
        endpoints.Should().OnlyContain(e => e.Metadata.GetMetadata<BffOnlyAttribute>() != null,
            "who may sign up is decided in the front end, so no auth endpoint may answer a direct caller");
    }
}
