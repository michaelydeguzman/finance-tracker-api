using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinanceTracker.Application.Options;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.Services.Email;

/// <summary>
/// Sends via Resend's REST API over a plain <see cref="HttpClient"/> — the payload is four
/// fields, so an SDK would be more dependency than it is worth.
/// </summary>
public sealed class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private readonly EmailOptions _options;

    public ResendEmailSender(HttpClient httpClient, IOptions<EmailOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.Resend.BaseUrl.TrimEnd('/')}/emails")
        {
            Content = JsonContent.Create(new
            {
                from = $"{_options.FromName} <{_options.FromAddress}>",
                to = new[] { message.ToAddress },
                subject = message.Subject,
                html = message.HtmlBody,
                text = message.TextBody
            })
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Resend.ApiKey);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        // Surfaced to the caller so a failed verification mail is a failed request rather
        // than a silent dead end, but the provider's body is not propagated — it can carry
        // the API key back in an echoed request.
        response.EnsureSuccessStatusCode();
    }
}
