using System.Text.Json;

namespace FinanceTracker.Tests.Integration;

internal static class HttpJsonOptions
{
    /// <summary>Matches ASP.NET Core default camelCase JSON for API payloads.</summary>
    public static JsonSerializerOptions ForApi { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
