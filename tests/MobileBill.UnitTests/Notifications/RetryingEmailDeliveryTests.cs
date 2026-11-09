using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MobileBill.Application.Notifications;
using MobileBill.Infrastructure.Notifications;

namespace MobileBill.UnitTests.Notifications;

public sealed class RetryingEmailDeliveryTests
{
    private static readonly EmailMessage Message = new(["cfo@concord.lk"], "Subject", "Body");

    [Fact]
    public async Task Tries_again_after_a_transient_failure_until_the_message_is_sent()
    {
        var sender = new ScriptedSender(Transient(), Transient(), null);

        Assert.True(await Delivery(sender).DeliverAsync(Message, default));
        Assert.Equal(3, sender.Attempts);
    }

    [Fact]
    public async Task Gives_up_after_the_maximum_number_of_attempts()
    {
        var sender = new ScriptedSender(Transient(), Transient(), Transient(), Transient());

        Assert.False(await Delivery(sender, maxAttempts: 3).DeliverAsync(Message, default));
        Assert.Equal(3, sender.Attempts);
    }

    [Fact]
    public async Task Does_not_retry_a_failure_that_will_not_change()
    {
        var sender = new ScriptedSender(new EmailDeliveryException("Bad credentials", isTransient: false), null);

        Assert.False(await Delivery(sender).DeliverAsync(Message, default));
        Assert.Equal(1, sender.Attempts);
    }

    private static RetryingEmailDelivery Delivery(IEmailSender sender, int maxAttempts = 5) =>
        new(sender, Options.Create(new EmailOptions { MaxAttempts = maxAttempts, RetryDelaySeconds = 0 }), NullLogger<RetryingEmailDelivery>.Instance);

    private static EmailDeliveryException Transient() => new("Busy", isTransient: true);

    // Each attempt throws the next scripted failure, or succeeds on null.
    private sealed class ScriptedSender(params EmailDeliveryException?[] outcomes) : IEmailSender
    {
        public int Attempts { get; private set; }
        public bool IsConfigured => true;
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            var outcome = outcomes[Attempts++];
            return outcome is null ? Task.CompletedTask : Task.FromException(outcome);
        }
    }
}
