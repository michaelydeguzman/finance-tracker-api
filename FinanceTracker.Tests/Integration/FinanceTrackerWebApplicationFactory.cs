using FinanceTracker.Application.Services;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// Hosts the API with EF Core InMemory instead of SQL Server for deterministic integration tests.
///
/// Also substitutes a fixed-tenant <see cref="ICurrentUserAccessor"/>, since the real one
/// reads claims off an authenticated principal that these requests do not carry. Once JWT
/// bearer auth lands, this can be swapped for a genuine signed test token.
/// </summary>
public sealed class FinanceTrackerWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"FinanceTracker_Integration_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            RemoveServiceDescriptors(services, typeof(DbContextOptions<FinanceTrackerContext>));
            RemoveServiceDescriptors(services, typeof(FinanceTrackerContext));

            services.AddDbContext<FinanceTrackerContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            RemoveServiceDescriptors(services, typeof(ICurrentUserAccessor));
            services.AddScoped<ICurrentUserAccessor>(_ => new TestCurrentUserAccessor());
        });
    }

    private static void RemoveServiceDescriptors(IServiceCollection services, Type serviceType)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == serviceType).ToList())
            services.Remove(descriptor);
    }
}
