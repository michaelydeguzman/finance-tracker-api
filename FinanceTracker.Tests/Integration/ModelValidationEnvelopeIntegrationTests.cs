using System.Net;
using System.Net.Http.Json;
using System.Text;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Responses;
using FinanceTracker.Domain.Entities;
using FluentAssertions;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// Model validation failures are produced by <c>[ApiController]</c> before any action body
/// runs, so they never passed through a controller's own ModelState check. These assert that
/// the framework's response carries the same <see cref="ApiResponseDto{T}"/> envelope as
/// every other response, rather than the default ValidationProblemDetails.
/// </summary>
public class ModelValidationEnvelopeIntegrationTests : IClassFixture<FinanceTrackerWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ModelValidationEnvelopeIntegrationTests(FinanceTrackerWebApplicationFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task PostTransaction_WithAnAmountBelowTheAllowedRange_ReturnsTheFailureEnvelope()
    {
        // Amount violates [Range(0.01, ...)] on CreateTransactionDto.
        var dto = new CreateTransactionDto
        {
            Name = "Zero",
            CategoryId = Guid.NewGuid(),
            Amount = 0m,
            TransactionDate = DateTime.UtcNow
        };

        var response = await _client.PostAsJsonAsync("/api/v1/transactions", dto, HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(HttpJsonOptions.ForApi);
        payload.Should().NotBeNull();
        payload!.Success.Should().BeFalse();
        payload.Message.Should().NotBeNullOrWhiteSpace(
            "the envelope must explain the rejection, not just carry success:false");
    }

    [Fact]
    public async Task PostCategory_WithAMissingRequiredField_ReturnsTheFailureEnvelope()
    {
        // Name is [Required]; sending it as null trips validation rather than the handler.
        var response = await _client.PostAsJsonAsync(
            "/api/v1/categories",
            new { name = (string?)null, categoryType = CategoryType.Expense },
            HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(HttpJsonOptions.ForApi);
        payload!.Success.Should().BeFalse();
        payload.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PostTransaction_WithAnUnparseableBody_ReturnsTheFailureEnvelope()
    {
        // A binding failure rather than an attribute failure — the other route into the
        // response factory, and the one whose ModelState entries may carry no message.
        var body = new StringContent(
            """{"name":"Bad","amount":"not-a-number"}""",
            Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/v1/transactions", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>(HttpJsonOptions.ForApi);
        payload!.Success.Should().BeFalse();
        payload.Message.Should().NotBeNullOrWhiteSpace();
    }
}
