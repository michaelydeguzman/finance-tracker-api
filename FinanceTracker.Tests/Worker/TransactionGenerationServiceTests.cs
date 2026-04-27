using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using FinanceTracker.Worker.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceTracker.Tests.Worker;

public class TransactionGenerationServiceTests
{
    private static FinanceTrackerContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<FinanceTrackerContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new FinanceTrackerContext(options);
    }

    private static RecurringTransaction CreateTemplate(
        RecurringTransactionStatus status,
        DateTime nextOccurrenceDate,
        FrequencyType frequencyType = FrequencyType.Daily,
        DateTime? endDate = null,
        string name = "Test Template",
        decimal defaultAmount = 100m,
        string createdBy = "test-user")
    {
        var categoryId = Guid.NewGuid();
        var frequencyId = Guid.NewGuid();

        return new RecurringTransaction
        {
            Id = Guid.NewGuid(),
            Name = name,
            DefaultAmount = defaultAmount,
            CategoryId = categoryId,
            Category = new Category { Id = categoryId, Name = "Test Category", CategoryType = CategoryType.Expense },
            FrequencyId = frequencyId,
            Frequency = new Frequency { Id = frequencyId, Name = frequencyType.ToString(), Type = frequencyType },
            StartDate = nextOccurrenceDate.AddMonths(-1),
            EndDate = endDate,
            NextOccurrenceDate = nextOccurrenceDate,
            Status = status,
            CreatedBy = createdBy
        };
    }

    private static TransactionGenerationService CreateService(
        FinanceTrackerContext context,
        IRecurringTransactionRepository repo)
    {
        var logger = new Mock<ILogger<TransactionGenerationService>>().Object;
        return new TransactionGenerationService(context, repo, logger);
    }

    // --- Test 1 ---
    [Fact]
    public async Task RunAsync_SingleActiveOverdueTemplate_GeneratesOneTransaction()
    {
        using var context = CreateInMemoryContext();
        var template = CreateTemplate(RecurringTransactionStatus.Active, DateTime.UtcNow.AddDays(-1));
        var originalDate = template.NextOccurrenceDate;

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        mockRepo.Setup(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecurringTransaction> { template });

        var service = CreateService(context, mockRepo.Object);
        await service.RunAsync();

        var transactions = context.Transactions.ToList();
        transactions.Should().HaveCount(1);
        transactions[0].TransactionDate.Should().Be(originalDate);
    }

    // --- Test 2 ---
    [Fact]
    public async Task RunAsync_MultipleOverdueOccurrences_GeneratesAllMissed()
    {
        using var context = CreateInMemoryContext();
        var template = CreateTemplate(RecurringTransactionStatus.Active, DateTime.UtcNow.AddDays(-3));

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        mockRepo.Setup(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecurringTransaction> { template });

        var service = CreateService(context, mockRepo.Object);
        await service.RunAsync();

        context.Transactions.Count().Should().Be(3);
    }

    // --- Test 3 ---
    [Fact]
    public async Task RunAsync_PausedTemplate_SkipsGeneration()
    {
        using var context = CreateInMemoryContext();

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        mockRepo.Setup(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecurringTransaction>()); // repo filters out Paused

        var service = CreateService(context, mockRepo.Object);
        await service.RunAsync();

        context.Transactions.Count().Should().Be(0);
    }

    // --- Test 4 ---
    [Fact]
    public async Task RunAsync_CancelledTemplate_SkipsGeneration()
    {
        using var context = CreateInMemoryContext();

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        mockRepo.Setup(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecurringTransaction>()); // repo filters out Cancelled

        var service = CreateService(context, mockRepo.Object);
        await service.RunAsync();

        context.Transactions.Count().Should().Be(0);
    }

    // --- Test 5 ---
    [Fact]
    public async Task RunAsync_FutureDatedTemplate_SkipsGeneration()
    {
        using var context = CreateInMemoryContext();

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        mockRepo.Setup(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecurringTransaction>()); // repo filters out future-dated

        var service = CreateService(context, mockRepo.Object);
        await service.RunAsync();

        context.Transactions.Count().Should().Be(0);
    }

    // --- Test 6 ---
    [Fact]
    public async Task RunAsync_GeneratedTransaction_HasCorrectFieldMapping()
    {
        using var context = CreateInMemoryContext();
        var template = CreateTemplate(
            RecurringTransactionStatus.Active,
            DateTime.UtcNow.AddDays(-1),
            name: "Rent",
            defaultAmount: 1200m,
            createdBy: "test-user");
        var originalNextDate = template.NextOccurrenceDate;

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        mockRepo.Setup(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecurringTransaction> { template });

        var service = CreateService(context, mockRepo.Object);
        await service.RunAsync();

        var tx = context.Transactions.Single();
        tx.Name.Should().Be("Rent");                              // D-08
        tx.Amount.Should().Be(1200m);                             // D-09
        tx.CategoryId.Should().Be(template.CategoryId);           // D-10
        tx.RecurringTransactionId.Should().Be(template.Id);       // D-11
        tx.CreatedBy.Should().Be("test-user");                    // D-12
        tx.TransactionDate.Should().Be(originalNextDate);         // D-07 — scheduled date, not wall-clock
    }

    // --- Test 7 ---
    [Fact]
    public async Task RunAsync_AfterGeneration_AdvancesNextOccurrenceDateOnTemplate()
    {
        using var context = CreateInMemoryContext();
        var template = CreateTemplate(RecurringTransactionStatus.Active, DateTime.UtcNow.AddDays(-1));
        var originalDate = template.NextOccurrenceDate;

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        mockRepo.Setup(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecurringTransaction> { template });

        var service = CreateService(context, mockRepo.Object);
        await service.RunAsync();

        template.NextOccurrenceDate.Should().BeAfter(DateTime.UtcNow); // advanced past now
        template.NextOccurrenceDate.Should().Be(originalDate.AddDays(1)); // Daily: +1 day
    }

    // --- Test 8 ---
    [Fact]
    public async Task RunAsync_EndDateExceeded_StopsGenerationWithoutStatusChange()
    {
        using var context = CreateInMemoryContext();
        // NextOccurrenceDate is overdue (outer while fires) but past EndDate (now-2).
        // Forces the D-13 inner guard to be reached — an impl that omits the guard would generate a transaction and fail.
        var endDate = DateTime.UtcNow.AddDays(-2);
        var template = CreateTemplate(
            RecurringTransactionStatus.Active,
            nextOccurrenceDate: DateTime.UtcNow.AddDays(-1), // overdue: outer while fires
            endDate: endDate);                                // EndDate is earlier: inner D-13 guard triggers break
        var originalNextDate = template.NextOccurrenceDate;

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        mockRepo.Setup(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecurringTransaction> { template });

        var service = CreateService(context, mockRepo.Object);
        await service.RunAsync();

        context.Transactions.Count().Should().Be(0);                              // D-13 — no generation
        template.Status.Should().Be(RecurringTransactionStatus.Active);           // D-13 — no auto-cancel
        template.NextOccurrenceDate.Should().Be(originalNextDate);                // D-13 — not advanced
    }

    // --- Test 9 ---
    [Fact]
    public async Task RunAsync_LastOccurrenceOnEndDate_GeneratesAndAdvances()
    {
        using var context = CreateInMemoryContext();
        // NextOccurrenceDate <= EndDate AND <= now — the last allowed occurrence
        var occurrenceDate = DateTime.UtcNow.AddDays(-1);
        var template = CreateTemplate(
            RecurringTransactionStatus.Active,
            nextOccurrenceDate: occurrenceDate,
            endDate: occurrenceDate);

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        mockRepo.Setup(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecurringTransaction> { template });

        var service = CreateService(context, mockRepo.Object);
        await service.RunAsync();

        context.Transactions.Count().Should().Be(1);
        template.NextOccurrenceDate.Should().BeAfter(template.EndDate!.Value); // advanced past EndDate — D-14
    }

    // --- Test 10 ---
    [Fact]
    public async Task RunAsync_OneTemplateThrows_OtherTemplateStillProcessed()
    {
        using var context = CreateInMemoryContext();

        // Bad template: Frequency is null — causes NullReferenceException when service reads template.Frequency.Type
        var badTemplate = new RecurringTransaction
        {
            Id = Guid.NewGuid(),
            Name = "Bad Template",
            DefaultAmount = 50m,
            CategoryId = Guid.NewGuid(),
            Category = new Category { Id = Guid.NewGuid(), Name = "Test", CategoryType = CategoryType.Expense },
            FrequencyId = Guid.NewGuid(),
            Frequency = null!, // intentionally null to force NullReferenceException — tests D-15
            StartDate = DateTime.UtcNow.AddMonths(-1),
            NextOccurrenceDate = DateTime.UtcNow.AddDays(-1),
            Status = RecurringTransactionStatus.Active,
            CreatedBy = "test-user"
        };

        var goodTemplate = CreateTemplate(RecurringTransactionStatus.Active, DateTime.UtcNow.AddDays(-1));

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        mockRepo.Setup(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecurringTransaction> { badTemplate, goodTemplate });

        var service = CreateService(context, mockRepo.Object);
        await service.RunAsync(); // must not throw — D-15 isolation

        var transactions = context.Transactions.ToList();
        transactions.Should().HaveCount(1);                                     // D-15 — good template processed
        transactions[0].RecurringTransactionId.Should().Be(goodTemplate.Id);    // D-15 — correct template
    }
}
