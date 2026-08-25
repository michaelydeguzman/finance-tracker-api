using FinanceTracker.Domain.Entities;
using FinanceTracker.Domain.Services;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Worker.Services;

public class TransactionGenerationService
{
    // Safety valve for templates that have gone unprocessed for a very long time
    // (e.g. worker down for months, or a short Custom interval never run). Without a cap,
    // a single template could stage an unbounded number of tracked entities before the
    // one SaveChangesAsync call. Remaining occurrences are picked up on the next run.
    private const int MaxOccurrencesPerTemplatePerRun = 500;

    private readonly FinanceTrackerContext _context;
    private readonly IRecurringTransactionRepository _recurringRepo;
    private readonly IRunLock _runLock;
    private readonly ILogger<TransactionGenerationService> _logger;

    public TransactionGenerationService(
        FinanceTrackerContext context,
        IRecurringTransactionRepository recurringRepo,
        IRunLock runLock,
        ILogger<TransactionGenerationService> logger)
    {
        _context = context;
        _recurringRepo = recurringRepo;
        _runLock = runLock;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        // Guards against overlapping worker runs (e.g. a scheduled invocation firing while a
        // previous slow run is still in progress) generating duplicate transactions for the
        // same overdue template. Held for the whole run, independent of the per-template
        // SaveChangesAsync calls below.
        if (!await _runLock.TryAcquireAsync())
        {
            _logger.LogWarning("Another {Service} run is already in progress; skipping this run.", nameof(TransactionGenerationService));
            return;
        }

        try
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

                    // D-15: Revert the template's NextOccurrenceDate mutation and detach any
                    // Transaction entities that were Added-but-not-yet-saved for this template.
                    // Without reverting the template too, its advanced (but unsaved) date would
                    // stay tracked as Modified and be silently committed by the next successful
                    // template's SaveChangesAsync, permanently skipping the failed occurrences.
                    var templateEntry = _context.Entry(template);
                    templateEntry.CurrentValues.SetValues(templateEntry.OriginalValues);
                    templateEntry.State = EntityState.Unchanged;

                    var unsaved = _context.ChangeTracker.Entries()
                        .Where(e => e.State == EntityState.Added)
                        .ToList();
                    foreach (var entry in unsaved)
                        entry.State = EntityState.Detached;
                }
            }
        }
        finally
        {
            await _runLock.ReleaseAsync();
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

            if (count >= MaxOccurrencesPerTemplatePerRun)
            {
                _logger.LogWarning(
                    "Template {TemplateId} hit the per-run cap of {Cap} generated occurrence(s); remaining backlog will be picked up on the next run.",
                    template.Id, MaxOccurrencesPerTemplatePerRun);
                break;
            }

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                Name = template.Name,                           // D-08
                CategoryId = template.CategoryId,               // D-10 (FK only)
                Category = null!,                               // nav property not set — EF Core does not validate at Add() time
                UserId = template.UserId,                       // tenancy: generated rows inherit the template's owner
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
