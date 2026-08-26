using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// The recurring-template endpoints over real HTTP, with raw JSON on the way in and raw JSON
/// inspected on the way out.
///
/// Handler tests construct DTOs directly and so cannot see a serialization contract at all —
/// which is exactly how an enum sent by name reached production as a 400 with every test
/// green. <see cref="AuthWireFormatIntegrationTests"/> exists for that reason and this class
/// follows it: every assertion about an enum here is made against the bytes on the wire.
/// </summary>
public class RecurringTransactionsApiIntegrationTests : IClassFixture<FinanceTrackerWebApplicationFactory>
{
    /// <summary>Seeded by <c>FrequencyConfiguration.HasData</c>; Monthly is <see cref="FrequencyType.Monthly"/> = 3.</summary>
    private static readonly Guid MonthlyFrequencyId = Guid.Parse("00000000-0000-0000-0000-000000000004");

    private readonly FinanceTrackerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RecurringTransactionsApiIntegrationTests(FinanceTrackerWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
        ResetDatabase();
    }

    private void ResetDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceTrackerContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }

    /// <summary>Raw JSON, not a serialized object — the point is to pin the wire format itself.</summary>
    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static string Iso(DateTime value) => value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    private async Task<Guid> CreateCategoryAsync(CategoryType type = CategoryType.Expense)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new CreateCategoryDto { Name = $"Bills {Guid.NewGuid():N}", CategoryType = type },
            HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private string CreateBody(Guid categoryId, DateTime startDate, DateTime? endDate = null, decimal amount = 1200.50m)
    {
        var end = endDate is null ? "null" : $"\"{Iso(endDate.Value)}\"";
        return $$"""
        {
          "name": "Rent",
          "description": "Monthly rent",
          "amount": {{amount}},
          "categoryId": "{{categoryId}}",
          "frequencyId": "{{MonthlyFrequencyId}}",
          "startDate": "{{Iso(startDate)}}",
          "endDate": {{end}}
        }
        """;
    }

    private async Task<(HttpResponseMessage Response, string Body)> PostTemplateAsync(
        Guid categoryId, DateTime startDate, DateTime? endDate = null)
    {
        var response = await _client.PostAsync(
            "/api/v1/recurring-transactions", Json(CreateBody(categoryId, startDate, endDate)));
        return (response, await response.Content.ReadAsStringAsync());
    }

    private static Guid IdOf(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    private static string StatusOf(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").GetProperty("status").GetString()!;
    }

    // ---- Wire format ---------------------------------------------------------------

    [Fact]
    public async Task Create_AcceptsTheFrontEndPayloadShape_AndAnswersInTheEnvelope()
    {
        var categoryId = await CreateCategoryAsync();

        var (response, body) = await PostTemplateAsync(categoryId, DateTime.UtcNow.AddDays(7));

        response.StatusCode.Should().Be(HttpStatusCode.Created, "a 400 here means nobody can create a template at all");
        body.Should().Contain("\"success\":true");

        foreach (var key in new[]
                 {
                     "\"id\"", "\"name\"", "\"amount\"", "\"categoryId\"", "\"categoryName\"",
                     "\"categoryType\"", "\"frequencyId\"", "\"frequencyName\"", "\"frequencyType\"",
                     "\"startDate\"", "\"endDate\"", "\"nextOccurrenceDate\"", "\"status\"",
                     "\"createdAt\"", "\"createdBy\""
                 })
        {
            body.Should().Contain(key);
        }
    }

    [Fact]
    public async Task Create_WritesStatusAsAName_AndTheOtherEnumsAsTheFrontEndAlreadyReadsThem()
    {
        var categoryId = await CreateCategoryAsync();

        var (_, body) = await PostTemplateAsync(categoryId, DateTime.UtcNow.AddDays(7));

        // Status: a new contract with no numeric consumer, persisted as a string, and read
        // as a badge label. Written by name.
        body.Should().Contain("\"status\":\"Active\"");

        // FrequencyType: /recurring-options already writes this as a number and
        // types/shared/enums.ts declares FrequencyType.Monthly = 3. Changing it here would
        // give the front end two conflicting formats for one enum.
        body.Should().Contain("\"frequencyType\":3");

        // CategoryType: TransactionResponseDto already writes it as a name, and a global
        // JsonStringEnumConverter — which would have been the easy way to do status — is
        // deliberately not registered precisely because it would rewrite this everywhere.
        body.Should().Contain("\"categoryType\":\"Expense\"");
    }

    [Fact]
    public async Task CategoryType_IsStillWrittenAsANumberOnTheCategoryEndpoint()
    {
        // The other half of the same guard: nothing added for recurring transactions may
        // turn CategoryResponseDto's numeric enum into a name.
        var response = await _client.PostAsync("/api/v1/categories", Json("""{"name":"Numeric check","categoryType":1}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadAsStringAsync()).Should().Contain("\"categoryType\":1");
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("active")]
    [InlineData("0")]
    public async Task List_AcceptsTheStatusFilterByNameOrByNumber(string status)
    {
        var categoryId = await CreateCategoryAsync();
        var (_, created) = await PostTemplateAsync(categoryId, DateTime.UtcNow.AddDays(7));
        var id = IdOf(created);

        var response = await _client.GetAsync($"/api/v1/recurring-transactions?status={status}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the query binder must not reject either spelling");
        (await response.Content.ReadAsStringAsync()).Should().Contain(id.ToString());
    }

    [Fact]
    public async Task List_WithAStatusThatIsNotAStatus_IsRejected()
    {
        var response = await _client.GetAsync("/api/v1/recurring-transactions?status=NotAStatus");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_FiltersByStatus()
    {
        var categoryId = await CreateCategoryAsync();
        var (_, created) = await PostTemplateAsync(categoryId, DateTime.UtcNow.AddDays(7));
        var id = IdOf(created);

        var paused = await _client.GetAsync("/api/v1/recurring-transactions?status=Paused");

        (await paused.Content.ReadAsStringAsync()).Should().NotContain(id.ToString());
    }

    // ---- Lifecycle -----------------------------------------------------------------

    [Fact]
    public async Task CreateReadUpdateDelete_EndToEnd()
    {
        var categoryId = await CreateCategoryAsync();
        var (_, created) = await PostTemplateAsync(categoryId, DateTime.UtcNow.AddDays(7));
        var id = IdOf(created);

        var read = await _client.GetAsync($"/api/v1/recurring-transactions/{id}");
        read.StatusCode.Should().Be(HttpStatusCode.OK);
        (await read.Content.ReadAsStringAsync()).Should().Contain("\"name\":\"Rent\"");

        var newStart = DateTime.UtcNow.AddDays(21);
        var update = await _client.PutAsync($"/api/v1/recurring-transactions/{id}", Json($$"""
        {
          "name": "Rent (increased)",
          "description": "Now with parking",
          "amount": 1350.00,
          "categoryId": "{{categoryId}}",
          "frequencyId": "{{MonthlyFrequencyId}}",
          "startDate": "{{Iso(newStart)}}",
          "endDate": null
        }
        """));

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedBody = await update.Content.ReadAsStringAsync();
        updatedBody.Should().Contain("\"name\":\"Rent (increased)\"");
        updatedBody.Should().Contain("\"amount\":1350.00");

        var delete = await _client.DeleteAsync($"/api/v1/recurring-transactions/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.OK);

        var readAfterDelete = await _client.GetAsync($"/api/v1/recurring-transactions/{id}");
        readAfterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PauseResumeCancel_MoveTheStatusAndReportItByName()
    {
        var categoryId = await CreateCategoryAsync();
        var (_, created) = await PostTemplateAsync(categoryId, DateTime.UtcNow.AddDays(7));
        var id = IdOf(created);

        var paused = await _client.PostAsync($"/api/v1/recurring-transactions/{id}/pause", null);
        paused.StatusCode.Should().Be(HttpStatusCode.OK);
        StatusOf(await paused.Content.ReadAsStringAsync()).Should().Be("Paused");

        var resumed = await _client.PostAsync($"/api/v1/recurring-transactions/{id}/resume", null);
        resumed.StatusCode.Should().Be(HttpStatusCode.OK);
        StatusOf(await resumed.Content.ReadAsStringAsync()).Should().Be("Active");

        var cancelled = await _client.PostAsync($"/api/v1/recurring-transactions/{id}/cancel", null);
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);
        StatusOf(await cancelled.Content.ReadAsStringAsync()).Should().Be("Cancelled");

        var resumeAfterCancel = await _client.PostAsync($"/api/v1/recurring-transactions/{id}/resume", null);
        resumeAfterCancel.StatusCode.Should().Be(HttpStatusCode.Conflict, "cancelling is terminal");

        var editAfterCancel = await _client.PutAsync($"/api/v1/recurring-transactions/{id}", Json(
            CreateBody(categoryId, DateTime.UtcNow.AddDays(7))));
        editAfterCancel.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Resume_AfterThePauseOutlastedTheSchedule_DoesNotLeaveABacklogForTheWorker()
    {
        var categoryId = await CreateCategoryAsync();
        var (_, created) = await PostTemplateAsync(categoryId, DateTime.UtcNow.AddDays(7));
        var id = IdOf(created);

        await _client.PostAsync($"/api/v1/recurring-transactions/{id}/pause", null);

        // Rewind the schedule five months, as if the template had been paused that long.
        await _factory.SeedAsync(async context =>
        {
            var template = await context.RecurringTransactions.IgnoreQueryFilters().SingleAsync(r => r.Id == id);
            template.NextOccurrenceDate = DateTime.UtcNow.AddMonths(-5);
            await context.SaveChangesAsync();
        });

        var resumed = await _client.PostAsync($"/api/v1/recurring-transactions/{id}/resume", null);
        resumed.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.SeedAsync(async context =>
        {
            var template = await context.RecurringTransactions.IgnoreQueryFilters().SingleAsync(r => r.Id == id);

            template.Status.Should().Be(RecurringTransactionStatus.Active);
            template.NextOccurrenceDate.Should().BeOnOrAfter(DateTime.UtcNow.Date,
                "otherwise the worker's catch-up loop writes five months of transactions on its next run");
        });
    }

    [Fact]
    public async Task Delete_ATemplateThatHasGeneratedTransactions_IsRefusedAndTheTemplateSurvives()
    {
        var categoryId = await CreateCategoryAsync();
        var (_, created) = await PostTemplateAsync(categoryId, DateTime.UtcNow.AddDays(7));
        var id = IdOf(created);

        await SeedGeneratedTransactionAsync(id, categoryId, _factory.DefaultUserId);

        var delete = await _client.DeleteAsync($"/api/v1/recurring-transactions/{id}");

        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await delete.Content.ReadAsStringAsync()).Should().Contain("Cancel it instead");

        var stillThere = await _client.GetAsync($"/api/v1/recurring-transactions/{id}");
        stillThere.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---- Validation ----------------------------------------------------------------

    [Fact]
    public async Task Create_WithAnotherTenantsCategory_IsRejected()
    {
        var strangerCategoryId = Guid.NewGuid();
        await _factory.SeedAsync(async context =>
        {
            context.Categories.Add(new Category
            {
                Id = strangerCategoryId,
                Name = "Stranger's bills",
                CategoryType = CategoryType.Expense,
                UserId = Guid.NewGuid()
            });
            await context.SaveChangesAsync();
        });

        var (response, _) = await PostTemplateAsync(strangerCategoryId, DateTime.UtcNow.AddDays(7));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithAnEndDateBeforeTheStartDate_IsRejected()
    {
        var categoryId = await CreateCategoryAsync();

        var (response, _) = await PostTemplateAsync(
            categoryId, DateTime.UtcNow.AddDays(30), DateTime.UtcNow.AddDays(10));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_WithAPastStartDate_SchedulesForwardRatherThanLeavingABacklog()
    {
        var categoryId = await CreateCategoryAsync();

        var (response, body) = await PostTemplateAsync(categoryId, DateTime.UtcNow.AddMonths(-5));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var document = JsonDocument.Parse(body);
        var next = document.RootElement.GetProperty("data").GetProperty("nextOccurrenceDate").GetDateTime();

        next.Should().BeOnOrAfter(DateTime.UtcNow.Date,
            "creating a template must never hand the worker months of history to materialise");
    }

    [Fact]
    public async Task RequestWithoutAToken_IsRejected()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/recurring-transactions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---- The transaction "recurring" badge -----------------------------------------

    [Fact]
    public async Task GeneratedTransactions_CarryTheirTemplateAndFrequency()
    {
        // app/transactions/types/transaction.api.ts declares frequencyId and frequencyName on
        // TransactionResponse and transaction-entry-list.tsx renders the "Recurrence" row from
        // frequencyName. The API never sent either, so that row was permanently blank.
        var categoryId = await CreateCategoryAsync();
        var (_, created) = await PostTemplateAsync(categoryId, DateTime.UtcNow.AddDays(7));
        var templateId = IdOf(created);

        await SeedGeneratedTransactionAsync(templateId, categoryId, _factory.DefaultUserId);

        var response = await _client.GetAsync("/api/v1/transactions");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain($"\"recurringTransactionId\":\"{templateId}\"");
        body.Should().Contain($"\"frequencyId\":\"{MonthlyFrequencyId}\"");
        body.Should().Contain("\"frequencyName\":\"Monthly\"");
    }

    [Fact]
    public async Task HandEnteredTransactions_ReportNoTemplateRatherThanOmittingTheFields()
    {
        var categoryId = await CreateCategoryAsync();

        var created = await _client.PostAsJsonAsync(
            "/api/v1/transactions",
            new CreateTransactionDto
            {
                Name = "Manual entry",
                CategoryId = categoryId,
                Amount = 9.99m,
                TransactionDate = DateTime.UtcNow
            },
            HttpJsonOptions.ForApi);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await created.Content.ReadAsStringAsync();
        body.Should().Contain("\"recurringTransactionId\":null");
        body.Should().Contain("\"frequencyId\":null");
        body.Should().Contain("\"frequencyName\":null");
    }

    private Task SeedGeneratedTransactionAsync(Guid templateId, Guid categoryId, Guid userId)
        => _factory.SeedAsync(async context =>
        {
            context.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                Name = "Rent",
                CategoryId = categoryId,
                Category = null!,
                UserId = userId,
                Amount = 1200.50m,
                TransactionDate = DateTime.UtcNow.AddDays(-1),
                RecurringTransactionId = templateId,
                CreatedBy = "worker"
            });

            await context.SaveChangesAsync();
        });
}
