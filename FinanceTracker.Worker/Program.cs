using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddDbContext<FinanceTrackerContext>(options =>
            options.UseSqlServer(
                context.Configuration.GetConnectionString("FinanceTrackerDB")));

        services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();
        services.AddScoped<TransactionGenerationService>();
    })
    .Build();

using var scope = host.Services.CreateScope();
var generator = scope.ServiceProvider.GetRequiredService<TransactionGenerationService>();
await generator.RunAsync();
