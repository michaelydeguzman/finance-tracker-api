using System.Net;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// Pins <c>/healthz</c> as a liveness answer about the process alone.
///
/// The database behind a deployment is serverless and auto-pauses when idle, billed for every
/// second it is awake. A health check that queried it would be polled by the platform and the
/// deploy pipeline, keep it from ever pausing, and spend the month's free compute on nothing.
/// </summary>
public class HealthEndpointIntegrationTests : IClassFixture<FinanceTrackerWebApplicationFactory>
{
    private readonly FinanceTrackerWebApplicationFactory _factory;

    public HealthEndpointIntegrationTests(FinanceTrackerWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Healthz_AnswersWithoutAToken()
    {
        var response = await _factory.CreateClient().GetAsync("/healthz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Healthz_AnswersWhileTheDatabaseIsUnreachable()
    {
        // Port 1 on loopback refuses at once, so a check that did reach for the database
        // would fail fast and turn this response unhealthy rather than hang the test.
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                foreach (var descriptor in services
                             .Where(d => d.ServiceType == typeof(DbContextOptions<FinanceTrackerContext>))
                             .ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<FinanceTrackerContext>(options =>
                    options.UseSqlServer("Server=127.0.0.1,1;Database=unreachable;Connect Timeout=1;Encrypt=False"));
            }));

        var response = await factory.CreateClient().GetAsync("/healthz");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
