using System.Threading.Channels;

namespace PlikShare.Agents.Events;

/// <summary>
/// In-memory hand-off from the audit funnel to the off-thread projector, mirroring how the audit log
/// itself decouples request handlers from persistence. Unbounded and non-blocking on the writer side:
/// publishing an event never slows down the request that caused it.
/// </summary>
public class AgentEventChannel : IAsyncDisposable
{
    private readonly Channel<AgentEventNotification> _channel = Channel.CreateUnbounded<AgentEventNotification>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    private volatile bool _isDisposed;

    public ChannelReader<AgentEventNotification> Reader => _channel.Reader;

    public void TryPublish(AgentEventNotification notification)
    {
        if (_isDisposed)
            return;

        _channel.Writer.TryWrite(notification);
    }

    public ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return ValueTask.CompletedTask;

        _isDisposed = true;
        _channel.Writer.TryComplete();

        return ValueTask.CompletedTask;
    }
}
