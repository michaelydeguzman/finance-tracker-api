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
