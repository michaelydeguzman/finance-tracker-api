using FinanceTracker.Domain.Repositories;
using FinanceTracker.Domain.Services;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config => config.AddUserSecrets<Program>(optional: true))
    .ConfigureServices((context, services) =>
    {
        services.AddDbContext<FinanceTrackerContext>(options =>
            options.UseSqlServer(
                context.Configuration.GetConnectionString("FinanceTrackerDB")));

        // The worker has no signed-in user. Stated explicitly so the tenancy query filters
        // resolve to "no tenant" and every cross-tenant read has to opt in by name.
        services.AddScoped<ICurrentUserAccessor, NoTenantAccessor>();

        services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();
        services.AddScoped<IRunLock, SqlServerRunLock>();
        services.AddScoped<TransactionGenerationService>();
    })
    .Build();

// Ctrl+C should end the run cleanly rather than killing the process mid-save: the template
// in flight is left untouched and still overdue, and the app lock is released on the way out.
// Host.CreateDefaultBuilder only wires the console lifetime when the host is actually run,
// and this is a run-and-exit job, so the token is established here instead.
using var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

using var scope = host.Services.CreateScope();
var generator = scope.ServiceProvider.GetRequiredService<TransactionGenerationService>();

try
{
    await generator.RunAsync(cancellation.Token);
}
catch (OperationCanceledException)
{
    // A clean stop rather than a failure, but the exit code still has to say the run did not
    // finish, so the scheduler does not record it as a completed generation.
    return 2;
}

return 0;
