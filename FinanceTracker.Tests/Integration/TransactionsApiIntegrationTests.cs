using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration;

public class TransactionsApiIntegrationTests : IClassFixture<FinanceTrackerWebApplicationFactory>
{
    private readonly FinanceTrackerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public TransactionsApiIntegrationTests(FinanceTrackerWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        ResetDatabase();
    }

    private void ResetDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceTrackerContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task PostGetPutDeleteTransaction_EndToEnd_ReturnsExpectedResponses()
    {
        var categoryId = await CreateCategoryViaApiAsync("Groceries", CategoryType.Expense);

        var createDto = new CreateTransactionDto
        {
            Name = "Milk",
            CategoryId = categoryId,
            Description = "Dairy",
            Amount = 4.99m,
            TransactionDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            FrequencyId = null,
            CreatedBy = "integration-test"
        };

        var postResponse = await _client.PostAsJsonAsync("/api/v1/transactions", createDto, HttpJsonOptions.ForApi);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createdPayload = await postResponse.Content.ReadFromJsonAsync<ApiResponseDto<TransactionResponseDto>>(HttpJsonOptions.ForApi);
        createdPayload.Should().NotBeNull();
        createdPayload!.Success.Should().BeTrue();
        createdPayload.Data!.Name.Should().Be("Milk");
        var transactionId = createdPayload.Data.Id;

        var listResponse = await _client.GetAsync("/api/v1/transactions");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var listPayload = await listResponse.Content.ReadFromJsonAsync<ApiResponseDto<List<TransactionResponseDto>>>(HttpJsonOptions.ForApi);
        listPayload!.Data.Should().ContainSingle(t => t.Id == transactionId);

        var updateDto = new UpdateTransactionDto
        {
            Name = "Organic milk",
            CategoryId = categoryId,
            Description = "Updated",
            Amount = 5.49m,
            TransactionDate = new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc),
            FrequencyId = null
        };

        var putResponse = await _client.PutAsJsonAsync($"/api/v1/transactions/{transactionId}", updateDto, HttpJsonOptions.ForApi);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedPayload = await putResponse.Content.ReadFromJsonAsync<ApiResponseDto<TransactionResponseDto>>(HttpJsonOptions.ForApi);
        updatedPayload!.Data!.Name.Should().Be("Organic milk");
        updatedPayload.Data.Amount.Should().Be(5.49m);

        var deleteResponse = await _client.DeleteAsync($"/api/v1/transactions/{transactionId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getAfterDelete = await _client.GetAsync("/api/v1/transactions");
        var afterDeletePayload = await getAfterDelete.Content.ReadFromJsonAsync<ApiResponseDto<List<TransactionResponseDto>>>(HttpJsonOptions.ForApi);
        afterDeletePayload!.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task PutTransaction_WhenMissing_ReturnsNotFound()
    {
        var categoryId = await CreateCategoryViaApiAsync("Misc", CategoryType.Expense);

        var updateDto = new UpdateTransactionDto
        {
            Name = "N/A",
            CategoryId = categoryId,
            Amount = 1m,
            TransactionDate = DateTime.UtcNow
        };

        var response = await _client.PutAsJsonAsync($"/api/v1/transactions/{Guid.NewGuid()}", updateDto, HttpJsonOptions.ForApi);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTransaction_WhenMissing_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync($"/api/v1/transactions/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TransactionsList_ByDateRange_FiltersInclusive_TRX01()
    {
        var cat = await CreateCategoryViaApiAsync("D1", CategoryType.Expense);
        var early = await CreateTransactionViaApiAsync("early", cat, new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc));
        var mid = await CreateTransactionViaApiAsync("mid", cat, new DateTime(2026, 2, 15, 0, 0, 0, DateTimeKind.Utc));
        var late = await CreateTransactionViaApiAsync("late", cat, new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc));

        var response = await _client.GetAsync(
            "/api/v1/transactions?from=2026-02-01T00:00:00Z&to=2026-02-28T23:59:59Z");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<TransactionResponseDto>>>(HttpJsonOptions.ForApi);
        payload!.Data!.Select(t => t.Id).Should().BeEquivalentTo(new[] { mid });
        payload.Data!.Should().NotContain(t => t.Id == early || t.Id == late);
    }

    [Fact]
    public async Task TransactionsList_ByCategoryIds_FiltersToSelectedGuids_TRX02()
    {
        var catA = await CreateCategoryViaApiAsync("CatA", CategoryType.Expense);
        var catB = await CreateCategoryViaApiAsync("CatB", CategoryType.Expense);
        var txA = await CreateTransactionViaApiAsync("a", catA, DateTime.UtcNow);
        var txB = await CreateTransactionViaApiAsync("b", catB, DateTime.UtcNow);

        var response = await _client.GetAsync($"/api/v1/transactions?categoryIds={catA}&categoryIds={catB}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<TransactionResponseDto>>>(HttpJsonOptions.ForApi);
        payload!.Data!.Select(t => t.Id).Should().BeEquivalentTo(new[] { txA, txB });
    }

    [Fact]
    public async Task TransactionsList_CategoryType_WhenCategoryIdsOmitted_StillFilters_TRX03()
    {
        var expenseCat = await CreateCategoryViaApiAsync("Exp", CategoryType.Expense);
        var incomeCat = await CreateCategoryViaApiAsync("Inc", CategoryType.Income);
        await CreateTransactionViaApiAsync("e", expenseCat, DateTime.UtcNow);
        await CreateTransactionViaApiAsync("i", incomeCat, DateTime.UtcNow);

        var response = await _client.GetAsync("/api/v1/transactions?categoryType=1");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<TransactionResponseDto>>>(HttpJsonOptions.ForApi);
        payload!.Data.Should().ContainSingle();
        payload.Data![0].CategoryId.Should().Be(expenseCat);
    }

    [Fact]
    public async Task TransactionsList_Paged_ReturnsItemsAndTotalCount_TRX04_05()
    {
        var cat = await CreateCategoryViaApiAsync("Paged", CategoryType.Expense);
        await CreateTransactionViaApiAsync("t1", cat, DateTime.UtcNow);
        await CreateTransactionViaApiAsync("t2", cat, DateTime.UtcNow);
        await CreateTransactionViaApiAsync("t3", cat, DateTime.UtcNow);

        var response = await _client.GetAsync("/api/v1/transactions?page=1&pageSize=2");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<PagedTransactionsResponseDto>>(HttpJsonOptions.ForApi);
        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();
        payload.Data.Should().NotBeNull();
        payload.Data!.TotalCount.Should().Be(3);
        payload.Data.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task TransactionsList_Paged_OrderedByTransactionDateDescThenIdDesc_TRX06()
    {
        var cat = await CreateCategoryViaApiAsync("Sort", CategoryType.Expense);
        var sameDay = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var idFirst = await CreateTransactionViaApiAsync("first", cat, sameDay);
        var idSecond = await CreateTransactionViaApiAsync("second", cat, sameDay);

        var response = await _client.GetAsync("/api/v1/transactions?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<PagedTransactionsResponseDto>>(HttpJsonOptions.ForApi);
        var ids = payload!.Data!.Items.Select(t => t.Id).ToList();
        ids.Should().HaveCount(2);
        var maxFirst = ids[0].CompareTo(ids[1]) > 0;
        maxFirst.Should().BeTrue("paged list should be ordered by Id descending when TransactionDate ties");
    }

    [Fact]
    public async Task TransactionsList_PageSizeOver20_Returns400_TRX07()
    {
        var response = await _client.GetAsync("/api/v1/transactions?page=1&pageSize=21");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TransactionsList_Unpaged_ReturnsListEnvelope_NotPagedDto_TRX08()
    {
        var cat = await CreateCategoryViaApiAsync("Unpaged", CategoryType.Expense);
        await CreateTransactionViaApiAsync("x", cat, DateTime.UtcNow);

        var response = await _client.GetAsync("/api/v1/transactions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<TransactionResponseDto>>>(HttpJsonOptions.ForApi);
        payload!.Success.Should().BeTrue();
        payload.Data.Should().ContainSingle();
    }

    [Fact]
    public async Task TransactionsList_EmptyCategoryIdsQuery_Returns400_TRX09()
    {
        var response = await _client.GetAsync("/api/v1/transactions?categoryIds=");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<Guid> CreateTransactionViaApiAsync(string name, Guid categoryId, DateTime transactionDate)
    {
        var dto = new CreateTransactionDto
        {
            Name = name,
            CategoryId = categoryId,
            Amount = 1m,
            TransactionDate = transactionDate,
            CreatedBy = "integration-test"
        };
        var postResponse = await _client.PostAsJsonAsync("/api/v1/transactions", dto, HttpJsonOptions.ForApi);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await postResponse.Content.ReadFromJsonAsync<ApiResponseDto<TransactionResponseDto>>(HttpJsonOptions.ForApi);
        created!.Data!.Id.Should().NotBeEmpty();
        return created.Data.Id;
    }

    private async Task<Guid> CreateCategoryViaApiAsync(string name, CategoryType categoryType)
    {
        var dto = new CreateCategoryDto { Name = name, CategoryType = categoryType };
        var response = await _client.PostAsJsonAsync("/api/v1/categories", dto, HttpJsonOptions.ForApi);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<CategoryResponseDto>>(HttpJsonOptions.ForApi);
        payload.Should().NotBeNull();
        payload!.Data.Should().NotBeNull();
        return payload.Data!.Id;
    }
}
