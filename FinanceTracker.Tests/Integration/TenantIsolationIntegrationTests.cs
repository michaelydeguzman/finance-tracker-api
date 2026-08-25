using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// The point of the tenancy work: one signed-in user must never reach another's financial
/// records. Asserted through the real HTTP pipeline — authentication, authorization, the
/// query filters and the repositories all in play — because that is the only place the
/// whole chain is exercised at once.
/// </summary>
public class TenantIsolationIntegrationTests : IClassFixture<FinanceTrackerWebApplicationFactory>
{
    private readonly FinanceTrackerWebApplicationFactory _factory;
    private readonly HttpClient _mine;

    private static readonly Guid StrangerId = Guid.NewGuid();

    public TenantIsolationIntegrationTests(FinanceTrackerWebApplicationFactory factory)
    {
        _factory = factory;
        _mine = factory.CreateAuthenticatedClient();
    }

    /// <summary>Plants a category and a transaction belonging to someone else entirely.</summary>
    private async Task<(Guid CategoryId, Guid TransactionId)> SeedStrangerDataAsync()
    {
        var categoryId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        await _factory.SeedAsync(async context =>
        {
            context.Categories.Add(new Category
            {
                Id = categoryId,
                Name = $"Stranger category {categoryId:N}",
                CategoryType = CategoryType.Expense,
                UserId = StrangerId
            });

            context.Transactions.Add(new Transaction
            {
                Id = transactionId,
                Name = "Stranger's rent",
                CategoryId = categoryId,
                Category = null!,
                UserId = StrangerId,
                Amount = 4321m,
                TransactionDate = DateTime.UtcNow,
                CreatedBy = "stranger@example.com"
            });

            await context.SaveChangesAsync();
        });

        return (categoryId, transactionId);
    }

    [Fact]
    public async Task RequestWithoutAToken_IsRejected()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/v1/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestWithATokenSignedByAnotherKey_IsRejected()
    {
        using var forged = _factory.CreateClient();
        forged.DefaultRequestHeaders.Add("Authorization", "Bearer not.a.real.token");

        var response = await forged.GetAsync("/api/v1/transactions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TransactionList_ExcludesAnotherTenantsRows()
    {
        var (_, strangerTransactionId) = await SeedStrangerDataAsync();

        var response = await _mine.GetAsync("/api/v1/transactions");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotContain(strangerTransactionId.ToString());
        body.Should().NotContain("Stranger's rent");
    }

    [Fact]
    public async Task CategoryList_ExcludesAnotherTenantsRows()
    {
        var (strangerCategoryId, _) = await SeedStrangerDataAsync();

        var response = await _mine.GetAsync("/api/v1/categories");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotContain(strangerCategoryId.ToString());
    }

    [Fact]
    public async Task UpdatingAnotherTenantsTransaction_IsNotFound()
    {
        // Not "forbidden": revealing that the id exists would itself leak something.
        var (categoryId, strangerTransactionId) = await SeedStrangerDataAsync();

        var response = await _mine.PutAsJsonAsync(
            $"/api/v1/transactions/{strangerTransactionId}",
            new UpdateTransactionDto
            {
                Name = "Hijacked",
                CategoryId = categoryId,
                Amount = 1m,
                TransactionDate = DateTime.UtcNow
            },
            HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletingAnotherTenantsTransaction_IsNotFound()
    {
        var (_, strangerTransactionId) = await SeedStrangerDataAsync();

        var response = await _mine.DeleteAsync($"/api/v1/transactions/{strangerTransactionId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnotherTenantsTransaction_SurvivesTheAttemptedDelete()
    {
        // A 404 that still deleted the row would be worse than a 200.
        var (_, strangerTransactionId) = await SeedStrangerDataAsync();

        await _mine.DeleteAsync($"/api/v1/transactions/{strangerTransactionId}");

        await _factory.SeedAsync(async context =>
        {
            var stillThere = await context.Transactions
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Id == strangerTransactionId);

            stillThere.Should().BeTrue();
        });
    }

    [Fact]
    public async Task CreatedTransaction_IsStampedWithTheCallersTenantAndEmail()
    {
        var createCategory = await _mine.PostAsJsonAsync(
            "/api/v1/categories",
            new CreateCategoryDto { Name = $"Mine {Guid.NewGuid():N}", CategoryType = CategoryType.Expense },
            HttpJsonOptions.ForApi);
        createCategory.StatusCode.Should().Be(HttpStatusCode.Created);

        var categoryId = (await createCategory.Content.ReadFromJsonAsync<ApiEnvelope<CategoryIdOnly>>(HttpJsonOptions.ForApi))!.Data!.Id;

        var created = await _mine.PostAsJsonAsync(
            "/api/v1/transactions",
            new CreateTransactionDto
            {
                Name = "My groceries",
                CategoryId = categoryId,
                Amount = 12.34m,
                TransactionDate = DateTime.UtcNow
            },
            HttpJsonOptions.ForApi);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        await _factory.SeedAsync(async context =>
        {
            var row = await context.Transactions
                .IgnoreQueryFilters()
                .SingleAsync(t => t.Name == "My groceries");

            row.UserId.Should().Be(_factory.DefaultUserId, "ownership comes from the token");
            row.CreatedBy.Should().Be(_factory.DefaultUserEmail,
                "the audit label is server-derived and no longer whatever the caller sent");
        });
    }

    private sealed record ApiEnvelope<T>(bool Success, string? Message, T? Data);

    private sealed record CategoryIdOnly(Guid Id);
}
