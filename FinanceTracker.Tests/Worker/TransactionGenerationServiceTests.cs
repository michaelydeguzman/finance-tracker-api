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
        // Bound to the tenant the test templates belong to. The real worker runs with no
        // tenant and reaches across them with IgnoreQueryFilters; here the same context is
        // used to assert on what it generated, which needs to see those rows.
        var options = new DbContextOptionsBuilder<FinanceTrackerContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new FinanceTrackerContext(options, new TestCurrentUserAccessor());
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
            Category = new Category { Id = categoryId, Name = "Test Category", CategoryType = CategoryType.Expense, UserId = TestCurrentUserAccessor.DefaultUserId },
            UserId = TestCurrentUserAccessor.DefaultUserId,
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
        IRecurringTransactionRepository repo,
        IRunLock? runLock = null)
    {
        var logger = new Mock<ILogger<TransactionGenerationService>>().Object;
        return new TransactionGenerationService(context, repo, runLock ?? new FakeRunLock(), logger);
    }

    // --- Test 1 ---
    [Fact]
    public async Task RunAsync_SingleActiveOverdueTemplate_GeneratesOneTransaction()
    {
        using var context = CreateInMemoryContext();
        // AddMinutes(5) ensures NextOccurrenceDate advances into the future (now + 5 min) after 1 daily cycle,
        // preventing the while-loop from firing a spurious extra iteration due to sub-millisecond clock drift.
        var template = CreateTemplate(RecurringTransactionStatus.Active, DateTime.UtcNow.AddDays(-1).AddMinutes(5));
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
        // AddMinutes(5): after 3 daily advances, NextOccurrenceDate = now + 5 min — loop stops at exactly 3.
        var template = CreateTemplate(RecurringTransactionStatus.Active, DateTime.UtcNow.AddDays(-3).AddMinutes(5));

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
            DateTime.UtcNow.AddDays(-1).AddMinutes(5),
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
        // AddMinutes(5): after 1 daily advance, NextOccurrenceDate = now + 5 min, satisfying BeAfter(DateTime.UtcNow).
        var template = CreateTemplate(RecurringTransactionStatus.Active, DateTime.UtcNow.AddDays(-1).AddMinutes(5));
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
        // AddMinutes(5) applied to both dates preserves the invariant nextOccurrenceDate > endDate (D-13 trigger).
        var endDate = DateTime.UtcNow.AddDays(-2).AddMinutes(5);
        var template = CreateTemplate(
            RecurringTransactionStatus.Active,
            nextOccurrenceDate: DateTime.UtcNow.AddDays(-1).AddMinutes(5), // overdue: outer while fires
            endDate: endDate);                                               // EndDate is earlier: inner D-13 guard triggers break
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
        // NextOccurrenceDate <= EndDate AND <= now — the last allowed occurrence.
        // AddMinutes(5): after 1 advance, NextOccurrenceDate = now + 5 min > EndDate, confirming D-14 advancement.
        var occurrenceDate = DateTime.UtcNow.AddDays(-1).AddMinutes(5);
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

        // Bad template: Frequency is null — causes NullReferenceException when service reads template.Frequency.Type.
        // AddMinutes(5) ensures the good template's loop also runs exactly once (same buffer logic as other tests).
        var badTemplate = new RecurringTransaction
        {
            Id = Guid.NewGuid(),
            Name = "Bad Template",
            DefaultAmount = 50m,
            CategoryId = Guid.NewGuid(),
            Category = new Category { Id = Guid.NewGuid(), Name = "Test", CategoryType = CategoryType.Expense, UserId = TestCurrentUserAccessor.DefaultUserId },
            UserId = TestCurrentUserAccessor.DefaultUserId,
            FrequencyId = Guid.NewGuid(),
            Frequency = null!, // intentionally null to force NullReferenceException — tests D-15
            StartDate = DateTime.UtcNow.AddMonths(-1),
            NextOccurrenceDate = DateTime.UtcNow.AddDays(-1).AddMinutes(5),
            Status = RecurringTransactionStatus.Active,
            CreatedBy = "test-user"
        };

        var goodTemplate = CreateTemplate(RecurringTransactionStatus.Active, DateTime.UtcNow.AddDays(-1).AddMinutes(5));

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        mockRepo.Setup(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecurringTransaction> { badTemplate, goodTemplate });

        var service = CreateService(context, mockRepo.Object);
        await service.RunAsync(); // must not throw — D-15 isolation

        var transactions = context.Transactions.ToList();
        transactions.Should().HaveCount(1);                                     // D-15 — good template processed
        transactions[0].RecurringTransactionId.Should().Be(goodTemplate.Id);    // D-15 — correct template
    }

    // --- Run lock ---
    [Fact]
    public async Task RunAsync_WhenLockHeldByAnotherRun_GeneratesNothing()
    {
        using var context = CreateInMemoryContext();
        var template = CreateTemplate(RecurringTransactionStatus.Active, DateTime.UtcNow.AddDays(-1).AddMinutes(5));

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        mockRepo.Setup(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecurringTransaction> { template });

        var runLock = new FakeRunLock(canAcquire: false);
        var service = CreateService(context, mockRepo.Object, runLock);

        await service.RunAsync();

        context.Transactions.Should().BeEmpty("a concurrent run already holds the lock");
        mockRepo.Verify(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()), Times.Never,
            "the run should bail out before querying for work");
    }

    [Fact]
    public async Task RunAsync_WhenLockNotAcquired_DoesNotReleaseIt()
    {
        // Releasing an applock this session does not own raises an error in SQL Server,
        // so the skip path must not touch it.
        using var context = CreateInMemoryContext();

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        var runLock = new FakeRunLock(canAcquire: false);

        await CreateService(context, mockRepo.Object, runLock).RunAsync();

        runLock.Released.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_ReleasesLockAfterSuccessfulRun()
    {
        using var context = CreateInMemoryContext();
        var template = CreateTemplate(RecurringTransactionStatus.Active, DateTime.UtcNow.AddDays(-1).AddMinutes(5));

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        mockRepo.Setup(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()))
            .ReturnsAsync(new List<RecurringTransaction> { template });

        var runLock = new FakeRunLock();
        await CreateService(context, mockRepo.Object, runLock).RunAsync();

        runLock.Released.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_ReleasesLockEvenWhenTheRunThrows()
    {
        // A lock left held by a crashed run would block every subsequent run indefinitely.
        using var context = CreateInMemoryContext();

        var mockRepo = new Mock<IRecurringTransactionRepository>();
        mockRepo.Setup(r => r.GetActiveOverdueAsync(It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidOperationException("repository unavailable"));

        var runLock = new FakeRunLock();
        var service = CreateService(context, mockRepo.Object, runLock);

        await service.Invoking(x => x.RunAsync())
            .Should().ThrowAsync<InvalidOperationException>();

        runLock.Released.Should().BeTrue();
    }
}
