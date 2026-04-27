using FinanceTracker.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Worker.Services;

public class TransactionGenerationService
{
    private readonly FinanceTrackerContext _context;
    private readonly IRecurringTransactionRepository _recurringRepo;
    private readonly ILogger<TransactionGenerationService> _logger;

    public TransactionGenerationService(
        FinanceTrackerContext context,
        IRecurringTransactionRepository recurringRepo,
        ILogger<TransactionGenerationService> logger)
    {
        _context = context;
        _recurringRepo = recurringRepo;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        // Implementation added in Plan 02
        await Task.CompletedTask;
    }
}
