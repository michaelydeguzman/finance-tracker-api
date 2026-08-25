using Microsoft.Extensions.Logging;

namespace FinanceTracker.Application.Services.Email;

/// <summary>
/// Writes the message to the log instead of sending it. The default provider, so an
/// unconfigured environment cannot silently mail real people — and the one used by tests,
/// where the logged body is how a test recovers the link it needs to follow.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Email not sent (logging provider). To: {To} Subject: {Subject}\n{Body}",
            message.ToAddress, message.Subject, message.TextBody);

        return Task.CompletedTask;
    }
}
