using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FinanceTracker.API.Authentication;
using FluentAssertions;

namespace FinanceTracker.Tests.Integration;

/// <summary>
/// Sends the exact JSON the front end sends, over HTTP, through the real model binder.
///
/// This exists because the rest of the suite does not. Handler tests construct DTOs
/// directly, so they cannot see a serialization contract at all — and an enum arriving as
/// a name rather than a number was rejected by the binder for exactly that reason, with
/// every test still green.
/// </summary>
public class AuthWireFormatIntegrationTests : IClassFixture<FinanceTrackerWebApplicationFactory>
{
    private const string BffSecret = "integration-bff-secret";

    private readonly FinanceTrackerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthWireFormatIntegrationTests(FinanceTrackerWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>Raw JSON, not a serialized object — the point is to pin the wire format itself.</summary>
    private static StringContent Json(string body) =>
        new(body, Encoding.UTF8, "application/json");

    private HttpRequestMessage BffRequest(string path, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = Json(body) };
        request.Headers.Add(BffOnlyAttribute.HeaderName, BffSecret);
        return request;
    }

    [Fact]
    public async Task Exchange_AcceptsTheProviderAsAName()
    {
        // Precisely what lib/server/api-session.ts sends.
        var response = await _client.SendAsync(BffRequest(
            "/api/v1/auth/exchange",
            """
            {
              "provider": "Google",
              "providerSubject": "google-oidc-subject-123",
              "email": "person@example.com",
              "emailVerified": true,
              "displayName": "Test Person"
            }
            """));

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the front end sends the provider by name, and a 400 here means no one can sign in with SSO");
    }

    [Fact]
    public async Task Exchange_StillAcceptsTheProviderAsANumber()
    {
        var response = await _client.SendAsync(BffRequest(
            "/api/v1/auth/exchange",
            """
            {
              "provider": 1,
              "providerSubject": "google-oidc-subject-456",
              "email": "numeric@example.com",
              "emailVerified": true
            }
            """));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Exchange_WithoutTheSharedSecret_IsRejected()
    {
        var response = await _client.PostAsync(
            "/api/v1/auth/exchange",
            Json("""{"provider":"Google","providerSubject":"s","email":"a@b.com","emailVerified":true}"""));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Exchange_WithAWrongSharedSecret_IsRejected()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/exchange")
        {
            Content = Json("""{"provider":"Google","providerSubject":"s","email":"a@b.com","emailVerified":true}"""),
        };
        request.Headers.Add(BffOnlyAttribute.HeaderName, "not-the-secret");

        (await _client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CategoryType_IsStillWrittenAsANumber()
    {
        // Guards the other direction, which is how the first attempt at the fix went wrong:
        // registering the name-based converter globally also changes how enums are written,
        // and the front end reads categoryType as a numeric TypeScript enum. A response of
        // "Expense" instead of 1 would break every category screen.
        var client = _factory.CreateAuthenticatedClient();

        var created = await client.PostAsync(
            "/api/v1/categories",
            Json("""{"name":"Wire format check","categoryType":1}"""));

        created.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await created.Content.ReadAsStringAsync();
        body.Should().Contain("\"categoryType\":1",
            "the front end reads this as a number, not a name");
    }

    [Fact]
    public async Task Register_AcceptsTheFrontEndPayloadShape()
    {
        var response = await _client.PostAsync(
            "/api/v1/auth/register",
            Json("""
            {
              "email": "wire-format@example.com",
              "password": "a sufficiently long password",
              "displayName": "Wire Format"
            }
            """));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Login_ReturnsTheFieldNamesTheFrontEndReads()
    {
        await _client.PostAsync(
            "/api/v1/auth/register",
            Json("""{"email":"reader@example.com","password":"a sufficiently long password"}"""));

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "reader@example.com", password = "a sufficiently long password" });

        // Registration does not verify the address, and login does not require it, so this
        // should succeed and carry the exact camelCase keys ApiSession destructures.
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        foreach (var key in new[]
                 {
                     "\"userId\"", "\"email\"", "\"emailVerified\"",
                     "\"accessToken\"", "\"accessTokenExpiresAt\"", "\"refreshToken\"",
                 })
        {
            body.Should().Contain(key, "lib/server/api-session.ts reads this key by name");
        }
    }
}
