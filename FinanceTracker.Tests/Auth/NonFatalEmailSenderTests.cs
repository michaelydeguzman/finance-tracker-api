using FinanceTracker.Application.Services.Email;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinanceTracker.Tests.Auth;

/// <summary>
/// The wrapper exists so a dead mail server cannot fail a request whose work is already
/// committed. These cover both halves of that: that it absorbs a delivery failure, and
/// that it does not absorb so much that a broken mail server goes unnoticed.
/// </summary>
public class NonFatalEmailSenderTests
{
    private static readonly EmailMessage Message =
        new("person@example.com", "Confirm your email", "<p>link</p>", "link");

    private sealed class ThrowingEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("the mail server is not listening");
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = new();

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class CancellingEmailSender : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ADeliveryFailure_DoesNotReachTheCaller()
    {
        var sender = new NonFatalEmailSender(
            new ThrowingEmailSender(), NullLogger<NonFatalEmailSender>.Instance);

        var send = async () => await sender.SendAsync(Message);

        await send.Should().NotThrowAsync(
            "the account the message refers to is already committed");
    }

    [Fact]
    public async Task ADeliveryFailure_IsLoggedAsAnError()
    {
        var logger = new Mock<ILogger<NonFatalEmailSender>>();
        var sender = new NonFatalEmailSender(new ThrowingEmailSender(), logger.Object);

        await sender.SendAsync(Message);

        logger.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "absorbing the failure without recording it would hide a broken mail server");
    }

    [Fact]
    public async Task ASuccessfulSend_PassesTheMessageStraightThrough()
    {
        var inner = new RecordingEmailSender();
        var sender = new NonFatalEmailSender(inner, NullLogger<NonFatalEmailSender>.Instance);

        await sender.SendAsync(Message);

        inner.Sent.Should().ContainSingle().Which.Should().Be(Message);
    }

    [Fact]
    public async Task Cancellation_StillPropagates()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        var sender = new NonFatalEmailSender(
            new CancellingEmailSender(), NullLogger<NonFatalEmailSender>.Instance);

        var send = async () => await sender.SendAsync(Message, source.Token);

        await send.Should().ThrowAsync<OperationCanceledException>(
            "a cancelled request is not a failed delivery");
    }
}
