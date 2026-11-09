using System.Threading.Channels;
using MobileBill.Application.Notifications;

namespace MobileBill.Infrastructure.Notifications;

// In memory: messages still waiting when the API stops are not sent.
public sealed class ChannelEmailQueue : IEmailQueue
{
    private readonly Channel<EmailMessage> channel = Channel.CreateUnbounded<EmailMessage>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(EmailMessage message) => channel.Writer.TryWrite(message);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken cancellationToken) => channel.Reader.ReadAllAsync(cancellationToken);
}
