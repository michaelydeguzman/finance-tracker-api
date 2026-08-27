using Microsoft.Extensions.Logging;

namespace FinanceTracker.Application.Services.Email;

/// <summary>
/// Sends through an inner sender, and logs a delivery failure rather than throwing it.
///
/// Every flow that emails a link — registration, magic link, password reset — commits its
/// work to the database before handing the message over, and answers the caller the same
/// way whether or not the address exists. A transport failure after that commit cannot be
/// reported without breaking that neutrality, and must not fail the request either: the
/// account or token is already real, so a 500 would tell the caller the work was undone
/// when it was not.
///
/// The failure is logged, never swallowed silently. The body is deliberately left out of
/// the log line: these messages carry single-use credentials in their links.
/// </summary>
public sealed class NonFatalEmailSender : IEmailSender
{
    private readonly IEmailSender _inner;
    private readonly ILogger<NonFatalEmailSender> _logger;

    public NonFatalEmailSender(IEmailSender inner, ILogger<NonFatalEmailSender> logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.SendAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller went away. That is a cancelled request, not a failed delivery.
            throw;
        }
        catch (Exception reason)
        {
            _logger.LogError(
                reason,
                "Could not deliver {Subject} to {Recipient}. Whatever it refers to was already saved.",
                message.Subject,
                message.ToAddress);
        }
    }
}
