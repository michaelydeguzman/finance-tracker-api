namespace FinanceTracker.Application.Options;

public enum EmailProvider
{
    /// <summary>Writes the message to the log. Default, so a missing config never sends mail.</summary>
    Logging,
    Smtp,
    Resend
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public EmailProvider Provider { get; set; } = EmailProvider.Logging;

    public string FromAddress { get; set; } = "no-reply@localhost";

    public string FromName { get; set; } = "Finance Tracker";

    public SmtpOptions Smtp { get; set; } = new();

    public ResendOptions Resend { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";

    /// <summary>1025 is Mailpit's default; a real relay is usually 587.</summary>
    public int Port { get; set; } = 1025;

    public bool UseStartTls { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }
}

public sealed class ResendOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.resend.com";
}
