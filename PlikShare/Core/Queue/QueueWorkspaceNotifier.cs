using System.Collections.Concurrent;
using System.Threading.Channels;

namespace PlikShare.Core.Queue;

public sealed class QueueWorkspaceNotifier
{
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<Channel<byte>, byte>> _subscribers = new();

    public WorkspaceSubscription Subscribe(int workspaceId)
    {
        var channel = Channel.CreateBounded<byte>(new BoundedChannelOptions(capacity: 1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        var channels = _subscribers.GetOrAdd(
            workspaceId,
            _ => new ConcurrentDictionary<Channel<byte>, byte>());

        channels.TryAdd(channel, 0);

        return new WorkspaceSubscription(this, workspaceId, channel);
    }

    public void Notify(int workspaceId)
    {
        if (_subscribers.TryGetValue(workspaceId, out var channels))
        {
            foreach (var channel in channels.Keys)
                channel.Writer.TryWrite(0);
        }
    }

    private void Unsubscribe(int workspaceId, Channel<byte> channel)
    {
        if (_subscribers.TryGetValue(workspaceId, out var channels))
        {
            channels.TryRemove(channel, out _);

            if (channels.IsEmpty)
                _subscribers.TryRemove(workspaceId, out _);
        }

        channel.Writer.TryComplete();
    }

    public sealed class WorkspaceSubscription : IDisposable
    {
        private readonly QueueWorkspaceNotifier _owner;
        private readonly int _workspaceId;
        private readonly Channel<byte> _channel;

        internal WorkspaceSubscription(QueueWorkspaceNotifier owner, int workspaceId, Channel<byte> channel)
        {
            _owner = owner;
            _workspaceId = workspaceId;
            _channel = channel;
        }

        public ValueTask<bool> WaitForSignalAsync(CancellationToken cancellationToken)
        {
            return _channel.Reader.WaitToReadAsync(cancellationToken);
        }

        public void DrainPending()
        {
            while (_channel.Reader.TryRead(out _))
            {
            }
        }

        public void Dispose()
        {
            _owner.Unsubscribe(_workspaceId, _channel);
        }
    }
}
