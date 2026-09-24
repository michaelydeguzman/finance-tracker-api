using FinanceTracker.Application.Options;
using FinanceTracker.Application.Services.Email;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Tests.Auth;

/// <summary>
/// The logging provider is what a deployment uses before real email is configured, and the
/// bodies it would log are sign-in, reset and verification links — live credentials. In a
/// deployment the log is shipped to a workspace that far more people and tools can read than
/// the addressee's inbox, so a body is only written when a developer has asked for it.
/// </summary>
public class LoggingEmailSenderTests
{
    private const string SignInLink = "https://app.test/magic-link?token=a-live-credential";

    private static readonly EmailMessage Message =
        new("person@example.com", "Your sign-in link", $"<a href=\"{SignInLink}\">Sign in</a>", SignInLink);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Lines { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }

    private static CapturingLogger<LoggingEmailSender> Send(bool logBodies)
    {
        var logger = new CapturingLogger<LoggingEmailSender>();
        var sender = new LoggingEmailSender(logger, Options.Create(new EmailOptions { LogBodies = logBodies }));

        sender.SendAsync(Message).GetAwaiter().GetResult();

        return logger;
    }

    [Fact]
    public void ByDefault_LogsWhoAndWhatButNotTheLink()
    {
        var line = Send(logBodies: false).Lines.Should().ContainSingle().Subject;

        line.Should().Contain("person@example.com").And.Contain("Your sign-in link");
        line.Should().NotContain("a-live-credential");
    }

    [Fact]
    public void WhenAskedTo_LogsTheBodySoALocalDeveloperCanFollowTheLink()
    {
        Send(logBodies: true).Lines.Should().ContainSingle()
            .Which.Should().Contain("a-live-credential");
    }

    [Fact]
    public void LogBodies_IsOffUnlessConfigured()
    {
        new EmailOptions().LogBodies.Should().BeFalse();
    }
}
