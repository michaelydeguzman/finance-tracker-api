using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Services;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
        var now = DateTime.UtcNow;
        var templates = await _recurringRepo.GetActiveOverdueAsync(now);

        _logger.LogInformation("Found {Count} active overdue recurring template(s)", templates.Count);

        foreach (var template in templates)
        {
            try
            {
                await GenerateForTemplateAsync(template, now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate transactions for template {TemplateId}", template.Id);

                // D-15: Detach any Transaction entities that were Added-but-not-yet-saved for this template.
                // Without this, a partial failure (e.g. exception thrown after Add() but before SaveChangesAsync())
                // would cause those orphaned rows to be committed by the next successful template's SaveChangesAsync().
                var unsaved = _context.ChangeTracker.Entries()
                    .Where(e => e.State == EntityState.Added)
                    .ToList();
                foreach (var entry in unsaved)
                    entry.State = EntityState.Detached;
            }
        }
    }

    private async Task GenerateForTemplateAsync(RecurringTransaction template, DateTime now)
    {
        int count = 0;

        while (template.NextOccurrenceDate <= now)
        {
            // D-13: EndDate is a generation boundary only.
            // Check at the TOP of the loop — before generation and before advancement.
            // If NextOccurrenceDate has exceeded EndDate, stop. Do NOT advance NextOccurrenceDate.
            // Do NOT change template.Status.
            if (template.EndDate.HasValue && template.NextOccurrenceDate > template.EndDate.Value)
                break;

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                Name = template.Name,                           // D-08
                CategoryId = template.CategoryId,               // D-10 (FK only)
                Category = null!,                               // nav property not set — EF Core does not validate at Add() time
                Amount = template.DefaultAmount,                 // D-09
                TransactionDate = template.NextOccurrenceDate,  // D-07: scheduled date, not wall-clock run time
                RecurringTransactionId = template.Id,           // D-11
                CreatedBy = template.CreatedBy,                 // D-12
                CreatedAt = DateTime.UtcNow,
                Description = string.Empty
            };

            _context.Transactions.Add(transaction);

            // D-14: advance NextOccurrenceDate using RecurrenceCalculator (snap-back anchoring for calendar types)
            template.NextOccurrenceDate = RecurrenceCalculator.NextOccurrence(
                template.Frequency.Type,
                template.Frequency.IntervalDays,
                template.NextOccurrenceDate,
                template.StartDate);

            count++;
        }

        if (count > 0)
        {
            // Saves all generated Transaction rows + the updated NextOccurrenceDate on the template
            // in a single round-trip. Per-template SaveChanges provides D-15 isolation:
            // if this save fails, the exception is caught in RunAsync and other templates proceed.
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation(
            "Template {TemplateId}: generated {Count} transaction(s), NextOccurrenceDate now {NextDate}",
            template.Id, count, template.NextOccurrenceDate);
    }
}
