using FinanceTracker.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Application.Services.Email;

/// <summary>
/// Writes the message to the log instead of sending it. The default provider, so an
/// unconfigured environment cannot silently mail real people.
///
/// The body is written only when <see cref="EmailOptions.LogBodies"/> is on. These messages
/// carry live sign-in and reset links, and a deployment still on this provider ships its log
/// to a workspace — see LoggingEmailSenderTests.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    private readonly bool _logBodies;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger, IOptions<EmailOptions> options)
    {
        _logger = logger;
        _logBodies = options.Value.LogBodies;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (_logBodies)
        {
            _logger.LogInformation(
                "Email not sent (logging provider). To: {To} Subject: {Subject}\n{Body}",
                message.ToAddress, message.Subject, message.TextBody);
        }
        else
        {
            _logger.LogInformation(
                "Email not sent (logging provider). To: {To} Subject: {Subject} (body withheld; set Email:LogBodies to log it)",
                message.ToAddress, message.Subject);
        }

        return Task.CompletedTask;
    }
}
