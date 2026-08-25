using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Entities;
using FinanceTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration;

public class CategoriesApiIntegrationTests : IClassFixture<FinanceTrackerWebApplicationFactory>
{
    private readonly FinanceTrackerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CategoriesApiIntegrationTests(FinanceTrackerWebApplicationFactory factory)
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

    [Fact]
    public async Task PostGetPutDeleteCategory_EndToEnd_ReturnsExpectedResponses()
    {
        var createDto = new CreateCategoryDto { Name = "Salary", CategoryType = CategoryType.Income };
        var postResponse = await _client.PostAsJsonAsync("/api/v1/categories", createDto, HttpJsonOptions.ForApi);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await postResponse.Content.ReadFromJsonAsync<ApiResponseDto<CategoryResponseDto>>(HttpJsonOptions.ForApi);
        var id = created!.Data!.Id;

        var getResponse = await _client.GetAsync($"/api/v1/categories/{id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateDto = new UpdateCategoryDto { Name = "Salary (net)", CategoryType = CategoryType.Income };
        var putResponse = await _client.PutAsJsonAsync($"/api/v1/categories/{id}", updateDto, HttpJsonOptions.ForApi);
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await putResponse.Content.ReadFromJsonAsync<ApiResponseDto<CategoryResponseDto>>(HttpJsonOptions.ForApi);
        updated!.Data!.Name.Should().Be("Salary (net)");

        var deleteResponse = await _client.DeleteAsync($"/api/v1/categories/{id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getMissing = await _client.GetAsync($"/api/v1/categories/{id}");
        getMissing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetCategories_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/categories");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<List<CategoryResponseDto>>>(HttpJsonOptions.ForApi);
        payload!.Success.Should().BeTrue();
        payload.Data.Should().NotBeNull();
    }
}
