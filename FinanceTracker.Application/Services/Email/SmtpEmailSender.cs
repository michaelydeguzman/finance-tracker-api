using FinanceTracker.Application.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace FinanceTracker.Application.Services.Email;

/// <summary>
/// Sends over SMTP. Points at a local catcher (Mailpit on 1025) in development and at a
/// real relay in production without a code change.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;

    public SmtpEmailSender(IOptions<EmailOptions> options) => _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody
        }.ToMessageBody();

        using var client = new SmtpClient();

        // A local catcher speaks plaintext on 1025 and has no certificate to validate,
        // so TLS is opt-in rather than automatic.
        var socketOptions = _options.Smtp.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(_options.Smtp.Host, _options.Smtp.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
        {
            await client.AuthenticateAsync(_options.Smtp.Username, _options.Smtp.Password, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
