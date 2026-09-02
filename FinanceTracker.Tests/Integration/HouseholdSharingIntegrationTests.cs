using System.Net;
using System.Net.Http.Json;
using FinanceTracker.Application.Dtos;
using FinanceTracker.Application.Dtos.Households;
using FinanceTracker.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// The point of households, asserted end to end: two signed-in people who accept an
/// invitation see one set of financial records, and everyone else still sees none of it.
///
/// Runs through the real pipeline for the same reason the tenancy tests do — the widened
/// query filter, the per-request household lookup and the invitation rules only meet each
/// other inside a request.
/// </summary>
public class HouseholdSharingIntegrationTests : IClassFixture<FinanceTrackerWebApplicationFactory>
{
    private readonly FinanceTrackerWebApplicationFactory _factory;

    public HouseholdSharingIntegrationTests(FinanceTrackerWebApplicationFactory factory) => _factory = factory;

    private sealed record ApiEnvelope<T>(bool Success, string? Message, T? Data);

    private sealed record IdOnly(Guid Id);

    private sealed record HouseholdShape(Guid Id, string Name, bool IsOwner, List<MemberShape> Members);

    private sealed record MemberShape(Guid UserId, string Email, bool IsOwner, bool IsYou);

    private sealed record InvitationShape(Guid Id, Guid HouseholdId, string HouseholdName, string Status);

    /// <summary>A signed-in person with an account behind the token.</summary>
    private async Task<(Guid UserId, string Email, HttpClient Client)> NewPersonAsync(bool emailVerified = true)
    {
        var userId = Guid.NewGuid();
        var email = $"{userId:N}@example.com";

        await _factory.SeedUserAsync(userId, email, emailVerified);

        return (userId, email, _factory.CreateClientFor(userId, email));
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<T>>(HttpJsonOptions.ForApi);
        return envelope!.Data!;
    }

    private static async Task<Guid> CreateCategoryAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/categories",
            new CreateCategoryDto { Name = name, CategoryType = CategoryType.Expense },
            HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await ReadDataAsync<IdOnly>(response)).Id;
    }

    private static async Task<string> RecordSpendOnAsync(HttpClient client, Guid categoryId, string label)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/transactions",
            new CreateTransactionDto
            {
                Name = label,
                CategoryId = categoryId,
                Amount = 42m,
                TransactionDate = DateTime.UtcNow
            },
            HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return label;
    }

    /// <summary>Creates a category and one transaction on it, both owned by the same person.</summary>
    private static async Task<string> RecordSpendAsync(HttpClient client, string label)
    {
        var categoryId = await CreateCategoryAsync(client, $"{label} category");

        return await RecordSpendOnAsync(client, categoryId, label);
    }

    private static async Task<HouseholdShape> CreateHouseholdAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/households", new CreateHouseholdDto { Name = name }, HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return await ReadDataAsync<HouseholdShape>(response);
    }

    private static async Task<InvitationShape> InviteAsync(HttpClient owner, string email)
    {
        var response = await owner.PostAsJsonAsync(
            "/api/v1/households/me/invitations",
            new InviteHouseholdMemberDto { Email = email },
            HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await ReadDataAsync<InvitationShape>(response);
    }

    /// <summary>Alice creates a household, invites Bob, Bob accepts.</summary>
    private async Task<(
        (Guid UserId, string Email, HttpClient Client) Alice,
        (Guid UserId, string Email, HttpClient Client) Bob)> SharedHouseholdAsync()
    {
        var alice = await NewPersonAsync();
        var bob = await NewPersonAsync();

        await CreateHouseholdAsync(alice.Client, "The De Guzmans");
        var invitation = await InviteAsync(alice.Client, bob.Email);

        var accepted = await bob.Client.PostAsync($"/api/v1/households/invitations/{invitation.Id}/accept", null);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);

        return (alice, bob);
    }

    [Fact]
    public async Task AMemberSeesTransactionsEnteredByTheirHouseholdmate()
    {
        var (alice, bob) = await SharedHouseholdAsync();

        var label = await RecordSpendAsync(alice.Client, $"Alice's shop {Guid.NewGuid():N}");

        var response = await bob.Client.GetAsync("/api/v1/transactions");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().Contain(label);
    }

    [Fact]
    public async Task SharingRunsBothWays()
    {
        var (alice, bob) = await SharedHouseholdAsync();

        var label = await RecordSpendAsync(bob.Client, $"Bob's shop {Guid.NewGuid():N}");

        var body = await (await alice.Client.GetAsync("/api/v1/transactions")).Content.ReadAsStringAsync();

        body.Should().Contain(label);
    }

    [Fact]
    public async Task HistoryEnteredBeforeJoiningComesWithTheJoiner()
    {
        // Otherwise a household starts empty and only fills up going forward, which is not
        // what "we share our finances" means to anyone.
        var alice = await NewPersonAsync();
        var bob = await NewPersonAsync();

        var label = await RecordSpendAsync(bob.Client, $"Bob's history {Guid.NewGuid():N}");

        await CreateHouseholdAsync(alice.Client, "Late joiners");
        var invitation = await InviteAsync(alice.Client, bob.Email);
        await bob.Client.PostAsync($"/api/v1/households/invitations/{invitation.Id}/accept", null);

        var body = await (await alice.Client.GetAsync("/api/v1/transactions")).Content.ReadAsStringAsync();

        body.Should().Contain(label);
    }

    [Fact]
    public async Task CategoriesAreSharedToo()
    {
        var (alice, bob) = await SharedHouseholdAsync();

        var name = $"Alice's category {Guid.NewGuid():N}";
        var created = await alice.Client.PostAsJsonAsync(
            "/api/v1/categories",
            new CreateCategoryDto { Name = name, CategoryType = CategoryType.Expense },
            HttpJsonOptions.ForApi);
        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await (await bob.Client.GetAsync("/api/v1/categories")).Content.ReadAsStringAsync();

        body.Should().Contain(name);
    }

    [Fact]
    public async Task SomeoneOutsideTheHouseholdStillSeesNothing()
    {
        var (alice, _) = await SharedHouseholdAsync();
        var outsider = await NewPersonAsync();

        var label = await RecordSpendAsync(alice.Client, $"Not yours {Guid.NewGuid():N}");

        var body = await (await outsider.Client.GetAsync("/api/v1/transactions")).Content.ReadAsStringAsync();

        body.Should().NotContain(label);
    }

    [Fact]
    public async Task AnInvitationCannotBeAcceptedByAnyoneButItsRecipient()
    {
        // Answered as 404, not 403: confirming the id exists would confirm that the address
        // it names has been approached.
        var alice = await NewPersonAsync();
        var bob = await NewPersonAsync();
        var interloper = await NewPersonAsync();

        await CreateHouseholdAsync(alice.Client, "Not for you");
        var invitation = await InviteAsync(alice.Client, bob.Email);

        var response = await interloper.Client.PostAsync(
            $"/api/v1/households/invitations/{invitation.Id}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnUnverifiedAddressCannotJoin()
    {
        // Accepting publishes the joiner's whole history to the household. An address nobody
        // has proved control of must not be able to do that.
        var alice = await NewPersonAsync();
        var unverified = await NewPersonAsync(emailVerified: false);

        await CreateHouseholdAsync(alice.Client, "Verified only");
        var invitation = await InviteAsync(alice.Client, unverified.Email);

        var response = await unverified.Client.PostAsync(
            $"/api/v1/households/invitations/{invitation.Id}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OnlyTheOwnerMayInvite()
    {
        var (_, bob) = await SharedHouseholdAsync();

        var response = await bob.Client.PostAsJsonAsync(
            "/api/v1/households/me/invitations",
            new InviteHouseholdMemberDto { Email = "someone-else@example.com" },
            HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task InvitingTheSameAddressTwiceIsAConflict()
    {
        var alice = await NewPersonAsync();
        var bob = await NewPersonAsync();

        await CreateHouseholdAsync(alice.Client, "Twice invited");
        await InviteAsync(alice.Client, bob.Email);

        var response = await alice.Client.PostAsJsonAsync(
            "/api/v1/households/me/invitations",
            new InviteHouseholdMemberDto { Email = bob.Email },
            HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task LeavingTakesYourRecordsWithYou()
    {
        var (alice, bob) = await SharedHouseholdAsync();

        var label = await RecordSpendAsync(bob.Client, $"Bob's shop {Guid.NewGuid():N}");

        var left = await bob.Client.PostAsync("/api/v1/households/me/leave", null);
        left.StatusCode.Should().Be(HttpStatusCode.OK);

        var aliceSees = await (await alice.Client.GetAsync("/api/v1/transactions")).Content.ReadAsStringAsync();
        var bobSees = await (await bob.Client.GetAsync("/api/v1/transactions")).Content.ReadAsStringAsync();

        aliceSees.Should().NotContain(label, "a former member's records are not the household's");
        bobSees.Should().Contain(label, "they are still his");
    }

    [Fact]
    public async Task RemovingAMemberEndsTheSharingInBothDirections()
    {
        var (alice, bob) = await SharedHouseholdAsync();

        var aliceLabel = await RecordSpendAsync(alice.Client, $"Alice's shop {Guid.NewGuid():N}");
        var bobLabel = await RecordSpendAsync(bob.Client, $"Bob's shop {Guid.NewGuid():N}");

        var removed = await alice.Client.DeleteAsync($"/api/v1/households/me/members/{bob.UserId}");
        removed.StatusCode.Should().Be(HttpStatusCode.OK);

        var aliceSees = await (await alice.Client.GetAsync("/api/v1/transactions")).Content.ReadAsStringAsync();
        var bobSees = await (await bob.Client.GetAsync("/api/v1/transactions")).Content.ReadAsStringAsync();

        aliceSees.Should().NotContain(bobLabel);
        bobSees.Should().NotContain(aliceLabel);
        bobSees.Should().Contain(bobLabel);
    }

    [Fact]
    public async Task AMemberCannotRemoveAnyone()
    {
        var (alice, bob) = await SharedHouseholdAsync();

        var response = await bob.Client.DeleteAsync($"/api/v1/households/me/members/{alice.UserId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task BeingInAHouseholdIsExclusive()
    {
        var (_, bob) = await SharedHouseholdAsync();
        var carol = await NewPersonAsync();

        await CreateHouseholdAsync(carol.Client, "Somewhere else");
        var invitation = await InviteAsync(carol.Client, bob.Email);

        var response = await bob.Client.PostAsync($"/api/v1/households/invitations/{invitation.Id}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TheInviteeSeesTheOfferOnTheirOwnPage()
    {
        var alice = await NewPersonAsync();
        var bob = await NewPersonAsync();

        await CreateHouseholdAsync(alice.Client, "Waiting on Bob");
        await InviteAsync(alice.Client, bob.Email);

        var invitations = await ReadDataAsync<List<InvitationShape>>(
            await bob.Client.GetAsync("/api/v1/households/invitations"));

        invitations.Should().ContainSingle().Which.HouseholdName.Should().Be("Waiting on Bob");
    }

    [Fact]
    public async Task DecliningLeavesNothingShared()
    {
        var alice = await NewPersonAsync();
        var bob = await NewPersonAsync();

        await CreateHouseholdAsync(alice.Client, "Politely declined");
        var invitation = await InviteAsync(alice.Client, bob.Email);

        var declined = await bob.Client.PostAsync($"/api/v1/households/invitations/{invitation.Id}/decline", null);
        declined.StatusCode.Should().Be(HttpStatusCode.OK);

        var label = await RecordSpendAsync(alice.Client, $"Still private {Guid.NewGuid():N}");
        var bobSees = await (await bob.Client.GetAsync("/api/v1/transactions")).Content.ReadAsStringAsync();

        bobSees.Should().NotContain(label);
    }

    [Fact]
    public async Task ARevokedInvitationCannotBeAccepted()
    {
        var alice = await NewPersonAsync();
        var bob = await NewPersonAsync();

        await CreateHouseholdAsync(alice.Client, "Changed my mind");
        var invitation = await InviteAsync(alice.Client, bob.Email);

        var revoked = await alice.Client.DeleteAsync($"/api/v1/households/me/invitations/{invitation.Id}");
        revoked.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await bob.Client.PostAsync($"/api/v1/households/invitations/{invitation.Id}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task NotBeingInAHouseholdIsASuccessfulNullRatherThanA404()
    {
        var loner = await NewPersonAsync();

        var response = await loner.Client.GetAsync("/api/v1/households/me");
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<HouseholdShape>>(HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        envelope!.Success.Should().BeTrue();
        envelope.Data.Should().BeNull();
    }

    [Fact]
    public async Task TheLastMemberOutClosesTheHousehold()
    {
        var alice = await NewPersonAsync();
        var household = await CreateHouseholdAsync(alice.Client, "Briefly a household");

        var left = await alice.Client.PostAsync("/api/v1/households/me/leave", null);
        left.StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.SeedAsync(async context =>
        {
            var stillThere = await context.Households.AnyAsync(h => h.Id == household.Id);
            stillThere.Should().BeFalse();
        });
    }

    [Fact]
    public async Task AnOwnerWhoLeavesHandsOwnershipOn()
    {
        // Rather than trapping them: the only alternative way out of a household you own
        // would be removing people whose records are not yours to decide about.
        var (alice, bob) = await SharedHouseholdAsync();

        var left = await alice.Client.PostAsync("/api/v1/households/me/leave", null);
        left.StatusCode.Should().Be(HttpStatusCode.OK);

        var household = await ReadDataAsync<HouseholdShape>(await bob.Client.GetAsync("/api/v1/households/me"));

        household.IsOwner.Should().BeTrue();
        household.Members.Should().ContainSingle().Which.UserId.Should().Be(bob.UserId);
    }

    [Fact]
    public async Task GeneratedRecordsStayInsideTheHousehold()
    {
        // The worker has no request identity, so a generated row can only get its household
        // from the template it came from.
        var (alice, bob) = await SharedHouseholdAsync();

        var frequencyId = Guid.NewGuid();
        await _factory.SeedAsync(async context =>
        {
            context.Frequencies.Add(new Frequency
            {
                Id = frequencyId,
                Name = $"Monthly {frequencyId:N}",
                Type = FrequencyType.Monthly,
                IntervalDays = 30,
                IsActive = true
            });

            await context.SaveChangesAsync();
        });

        var categoryResponse = await alice.Client.PostAsJsonAsync(
            "/api/v1/categories",
            new CreateCategoryDto { Name = $"Bills {Guid.NewGuid():N}", CategoryType = CategoryType.Expense },
            HttpJsonOptions.ForApi);
        var categoryId = (await ReadDataAsync<IdOnly>(categoryResponse)).Id;

        var templateName = $"Shared rent {Guid.NewGuid():N}";
        var created = await alice.Client.PostAsJsonAsync(
            "/api/v1/recurring-transactions",
            new CreateRecurringTransactionDto
            {
                Name = templateName,
                CategoryId = categoryId,
                FrequencyId = frequencyId,
                Amount = 1200m,
                StartDate = DateTime.UtcNow.Date.AddDays(1)
            },
            HttpJsonOptions.ForApi);

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var bobSees = await (await bob.Client.GetAsync("/api/v1/recurring-transactions")).Content.ReadAsStringAsync();

        bobSees.Should().Contain(templateName);
    }

    [Fact]
    public async Task ATransactionSurvivesTheDepartureOfWhoeverOwnedItsCategory()
    {
        // A Transaction's Category is a *required* navigation. If the category leaves the
        // household while the transaction stays, the filter hides the principal and the
        // required join drops the dependent — so Bob loses his own transaction because
        // Alice left. The category is held back for exactly this reason.
        var (alice, bob) = await SharedHouseholdAsync();

        var sharedCategoryId = await CreateCategoryAsync(alice.Client, $"Groceries {Guid.NewGuid():N}");
        var label = await RecordSpendOnAsync(bob.Client, sharedCategoryId, $"Bob's milk {Guid.NewGuid():N}");

        (await alice.Client.PostAsync("/api/v1/households/me/leave", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var unfiltered = await (await bob.Client.GetAsync("/api/v1/transactions")).Content.ReadAsStringAsync();
        var expensesOnly = await (await bob.Client.GetAsync("/api/v1/transactions?categoryType=Expense"))
            .Content.ReadAsStringAsync();

        unfiltered.Should().Contain(label, "it is Bob's own transaction");
        expensesOnly.Should().Contain(label, "the category filter must not drop it either");
    }

    [Fact]
    public async Task AHouseholdStillClosesWhenACategoryWasHeldBackForSomeoneElse()
    {
        // The held-back category above still points at the household, and every tenancy FK
        // is Restrict. Without clearing the stamp first, the last member's departure throws
        // DbUpdateException and the household can never be closed.
        var (alice, bob) = await SharedHouseholdAsync();

        var sharedCategoryId = await CreateCategoryAsync(alice.Client, $"Bills {Guid.NewGuid():N}");
        await RecordSpendOnAsync(bob.Client, sharedCategoryId, $"Bob's bill {Guid.NewGuid():N}");

        var household = await ReadDataAsync<HouseholdShape>(
            await alice.Client.GetAsync("/api/v1/households/me"));

        (await alice.Client.PostAsync("/api/v1/households/me/leave", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await bob.Client.PostAsync("/api/v1/households/me/leave", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.SeedAsync(async context =>
        {
            (await context.Households.AnyAsync(h => h.Id == household.Id)).Should().BeFalse();

            var stranded = await context.Categories
                .IgnoreQueryFilters()
                .AnyAsync(c => c.HouseholdId == household.Id);

            stranded.Should().BeFalse("nothing may still point at a deleted household");
        });
    }

    [Fact]
    public async Task AnInvitationDiesWithTheMembershipOfWhoeverSentIt()
    {
        // Otherwise an offer outlives its author: Alice invites Bob, Alice leaves, ownership
        // passes to Mallory, and Bob's acceptance days later publishes his whole history to
        // Mallory — someone Bob has never heard of. The invitation only ever showed Bob a
        // household name, so nothing warned him.
        var alice = await NewPersonAsync();
        var mallory = await NewPersonAsync();
        var bob = await NewPersonAsync();

        await CreateHouseholdAsync(alice.Client, "Changing hands");

        var bobsInvitation = await InviteAsync(alice.Client, bob.Email);

        var mallorysInvitation = await InviteAsync(alice.Client, mallory.Email);
        (await mallory.Client.PostAsync($"/api/v1/households/invitations/{mallorysInvitation.Id}/accept", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await alice.Client.PostAsync("/api/v1/households/me/leave", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await bob.Client.PostAsync(
            $"/api/v1/households/invitations/{bobsInvitation.Id}/accept", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var label = await RecordSpendAsync(bob.Client, $"Bob's private {Guid.NewGuid():N}");
        var mallorySees = await (await mallory.Client.GetAsync("/api/v1/transactions")).Content.ReadAsStringAsync();

        mallorySees.Should().NotContain(label);
    }

    [Fact]
    public async Task JoiningANewHouseholdDoesNotDragACategoryOutOfTheOldOne()
    {
        // The pin has to hold in both directions. Bob's category is load-bearing for Alice's
        // transaction; if Bob's next household re-stamped it, Alice would silently lose her
        // own row — the same defect as a departure, arriving by the other door.
        var (alice, bob) = await SharedHouseholdAsync();

        var bobsCategoryId = await CreateCategoryAsync(bob.Client, $"Snacks {Guid.NewGuid():N}");
        var label = await RecordSpendOnAsync(alice.Client, bobsCategoryId, $"Alice's snack {Guid.NewGuid():N}");

        (await bob.Client.PostAsync("/api/v1/households/me/leave", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await CreateHouseholdAsync(bob.Client, $"Bob's new place {Guid.NewGuid():N}");

        var expensesOnly = await (await alice.Client.GetAsync("/api/v1/transactions?categoryType=Expense"))
            .Content.ReadAsStringAsync();

        expensesOnly.Should().Contain(label, "Alice's own transaction must survive Bob moving on");
    }

    [Fact]
    public async Task ALeaverKeepsTransactionsFiledUnderSomebodyElsesCategory()
    {
        // The half of the required-navigation problem that costs the person leaving. Alice's
        // rows point at Bob's category; on the way out that category stays with Bob, so
        // without forking her a copy the required join drops every one of them and they
        // vanish from her own list, her totals and her exports — unreachable through the API
        // entirely, since fetching one by id Includes the category too.
        var (alice, bob) = await SharedHouseholdAsync();

        var bobsCategoryId = await CreateCategoryAsync(bob.Client, $"Groceries {Guid.NewGuid():N}");
        var label = await RecordSpendOnAsync(alice.Client, bobsCategoryId, $"Alice's shop {Guid.NewGuid():N}");

        (await alice.Client.PostAsync("/api/v1/households/me/leave", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var unfiltered = await (await alice.Client.GetAsync("/api/v1/transactions")).Content.ReadAsStringAsync();
        var expensesOnly = await (await alice.Client.GetAsync("/api/v1/transactions?categoryType=Expense"))
            .Content.ReadAsStringAsync();

        unfiltered.Should().Contain(label, "they are Alice's own transactions");
        expensesOnly.Should().Contain(label, "and the category filter must not drop them either");
    }

    [Fact]
    public async Task ALeaverKeepsControlOfATemplateFiledUnderSomebodyElsesCategory()
    {
        // Same mechanism on RecurringTransaction, where the consequence is worse: the worker
        // sweeps with IgnoreQueryFilters, so a template its owner can no longer see keeps
        // generating real money every month with no way to pause, cancel or delete it.
        var (alice, bob) = await SharedHouseholdAsync();

        var frequencyId = Guid.NewGuid();
        await _factory.SeedAsync(async context =>
        {
            context.Frequencies.Add(new Frequency
            {
                Id = frequencyId,
                Name = $"Monthly {frequencyId:N}",
                Type = FrequencyType.Monthly,
                IntervalDays = 30,
                IsActive = true
            });

            await context.SaveChangesAsync();
        });

        var bobsCategoryId = await CreateCategoryAsync(bob.Client, $"Rent {Guid.NewGuid():N}");

        var templateName = $"Alice's rent {Guid.NewGuid():N}";
        var created = await alice.Client.PostAsJsonAsync(
            "/api/v1/recurring-transactions",
            new CreateRecurringTransactionDto
            {
                Name = templateName,
                CategoryId = bobsCategoryId,
                FrequencyId = frequencyId,
                Amount = 1200m,
                StartDate = DateTime.UtcNow.Date.AddDays(1)
            },
            HttpJsonOptions.ForApi);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var templateId = (await ReadDataAsync<IdOnly>(created)).Id;

        (await alice.Client.PostAsync("/api/v1/households/me/leave", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var listed = await (await alice.Client.GetAsync("/api/v1/recurring-transactions"))
            .Content.ReadAsStringAsync();

        listed.Should().Contain(templateName);

        var paused = await alice.Client.PostAsync($"/api/v1/recurring-transactions/{templateId}/pause", null);

        paused.StatusCode.Should().Be(HttpStatusCode.OK, "an unstoppable template is money leaving an account");
    }

    [Fact]
    public async Task ForkingReusesACategoryTheDatabaseWouldCallADuplicate()
    {
        // The unique index on (UserId, CategoryType, Name) is enforced under SQL Server's
        // collation, which ignores trailing whitespace. If the fork's own matching does not,
        // it inserts a category the index then rejects, and the DbUpdateException leaves the
        // member unable to leave the household at all.
        var (alice, bob) = await SharedHouseholdAsync();

        var stem = $"Snacks {Guid.NewGuid():N}";
        await CreateCategoryAsync(alice.Client, $"{stem} ");

        var bobsCategoryId = await CreateCategoryAsync(bob.Client, stem);
        var label = await RecordSpendOnAsync(alice.Client, bobsCategoryId, $"Alice's snack {Guid.NewGuid():N}");

        (await alice.Client.PostAsync("/api/v1/households/me/leave", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await _factory.SeedAsync(async context =>
        {
            var hers = await context.Categories
                .IgnoreQueryFilters()
                .Where(c => c.UserId == alice.UserId && c.Name.StartsWith(stem))
                .ToListAsync();

            hers.Should().ContainSingle("the fork must reuse her category, not add one the index rejects");
        });

        var expensesOnly = await (await alice.Client.GetAsync("/api/v1/transactions?categoryType=Expense"))
            .Content.ReadAsStringAsync();

        expensesOnly.Should().Contain(label);
    }

    [Fact]
    public async Task ATransactionCannotBeFiledUnderACategoryTheCallerCannotReach()
    {
        var mine = await NewPersonAsync();
        var stranger = await NewPersonAsync();

        var strangersCategoryId = await CreateCategoryAsync(stranger.Client, $"Theirs {Guid.NewGuid():N}");

        var response = await mine.Client.PostAsJsonAsync(
            "/api/v1/transactions",
            new CreateTransactionDto
            {
                Name = "Not allowed",
                CategoryId = strangersCategoryId,
                Amount = 1m,
                TransactionDate = DateTime.UtcNow
            },
            HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AnUnconfirmedAddressCannotStartAHousehold()
    {
        // A household is the only thing here that mails a third party, and it puts a name its
        // creator chose in the subject line. An account that has not proved its own address
        // must not be able to reach anyone else's.
        var unverified = await NewPersonAsync(emailVerified: false);

        var response = await unverified.Client.PostAsJsonAsync(
            "/api/v1/households",
            new CreateHouseholdDto { Name = "Unproven" },
            HttpJsonOptions.ForApi);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
