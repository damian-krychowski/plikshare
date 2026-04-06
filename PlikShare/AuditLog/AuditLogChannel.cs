using System.Threading.Channels;

namespace PlikShare.AuditLog;

public class AuditLogChannel : IAsyncDisposable
{
    private readonly Channel<AuditLogEntry> _channel = Channel.CreateUnbounded<AuditLogEntry>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    private readonly CancellationTokenSource _shutdownCts = new();
    private volatile bool _isDisposed;

    public ChannelReader<AuditLogEntry> Reader => _channel.Reader;

    public async ValueTask WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _shutdownCts.Token);

        await _channel.Writer.WriteAsync(
            entry,
            linkedCts.Token);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        try
        {
            await _shutdownCts.CancelAsync();
            _channel.Writer.TryComplete();
        }
        finally
        {
            _shutdownCts.Dispose();
        }
    }
}
