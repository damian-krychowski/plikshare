using System.Collections.Concurrent;
using System.Threading.Channels;

namespace PlikShare.Core.Queue;

/// <summary>
/// In-process pub/sub for queue batch progress. The queue lifecycle (<see cref="Queue"/>) calls
/// <see cref="Notify"/> whenever a job belonging to a batch finishes; SSE endpoints subscribe per
/// batch and re-read the batch status on each signal. PlikShare runs single-process (in-memory
/// channels + keystore), so this needs no backplane.
///
/// Each subscriber gets a bounded channel of capacity 1 with drop-oldest semantics: a burst of
/// notifications coalesces into "something changed, re-query", which is exactly what the consumer
/// does — it always reads the freshest state from the DB, never relying on the signal's payload.
/// </summary>
public sealed class QueueBatchNotifier
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Channel<byte>, byte>> _subscribers = new();

    public BatchSubscription Subscribe(Guid batchId)
    {
        var channel = Channel.CreateBounded<byte>(new BoundedChannelOptions(capacity: 1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

        var channels = _subscribers.GetOrAdd(
            batchId, 
            _ => new ConcurrentDictionary<Channel<byte>, byte>());
            
        channels.TryAdd(channel, 0);

        return new BatchSubscription(this, batchId, channel);
    }

    public void Notify(Guid batchId)
    {
        if (_subscribers.TryGetValue(batchId, out var channels))
        {
            foreach (var channel in channels.Keys)
                channel.Writer.TryWrite(0);
        }
    }

    private void Unsubscribe(Guid batchId, Channel<byte> channel)
    {
        if (_subscribers.TryGetValue(batchId, out var channels))
        {
            channels.TryRemove(channel, out _);

            if (channels.IsEmpty)
                _subscribers.TryRemove(batchId, out _);
        }

        channel.Writer.TryComplete();
    }

    public sealed class BatchSubscription : IDisposable
    {
        private readonly QueueBatchNotifier _owner;
        private readonly Guid _batchId;
        private readonly Channel<byte> _channel;

        internal BatchSubscription(QueueBatchNotifier owner, Guid batchId, Channel<byte> channel)
        {
            _owner = owner;
            _batchId = batchId;
            _channel = channel;
        }

        /// <summary>Completes once per coalesced notification; throws on cancellation.</summary>
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
            _owner.Unsubscribe(_batchId, _channel);
        }
    }
}
