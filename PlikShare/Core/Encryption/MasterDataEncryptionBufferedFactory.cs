using System.Threading.Channels;

namespace PlikShare.Core.Encryption;

public class MasterDataEncryptionBufferedFactory : IDisposable
{
    private readonly IMasterDataEncryption _masterDataEncryption;
    private readonly Channel<IDerivedMasterDataEncryption> _buffer;
    private readonly CancellationTokenSource _cts;
    private readonly Task _backgroundFillingTask;
    private bool _disposed;

    public MasterDataEncryptionBufferedFactory(
        IMasterDataEncryption masterDataEncryption,
        int bufferSize)
    {
        _masterDataEncryption = masterDataEncryption ?? throw new ArgumentNullException(nameof(masterDataEncryption));
        var bufferSize1 = bufferSize > 0 ? bufferSize : throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be greater than 0.");

        // Create a bounded channel to hold our pre-created encryptions
        _buffer = Channel.CreateBounded<IDerivedMasterDataEncryption>(new BoundedChannelOptions(bufferSize1)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        _cts = new CancellationTokenSource();
        _backgroundFillingTask = Task.Run(FillBufferInBackgroundAsync);
    }

    /// <summary>
    /// Takes a derived master data encryption from the buffer.
    /// If the buffer is empty, waits until an item becomes available.
    /// </summary>
    /// <returns>A ValueTask that represents the asynchronous operation, containing the derived encryption.</returns>
    public ValueTask<IDerivedMasterDataEncryption> Take(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        return _buffer.Reader.ReadAsync(cancellationToken);
    }

    /// <summary>
    /// Background task that fills the buffer with derived encryptions whenever space is available.
    /// </summary>
    private async Task FillBufferInBackgroundAsync()
    {
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                // Create a new derived encryption
                var derivedEncryption = _masterDataEncryption.NewDerived();

                // Try to add it to the buffer, waiting if the buffer is full
                await _buffer.Writer.WriteAsync(derivedEncryption, _cts.Token);
            }
        }
        catch (OperationCanceledException) when (_cts.Token.IsCancellationRequested)
        {
            // Normal cancellation, no need to do anything
        }
        catch (Exception ex)
        {
            // Complete the channel with an error to propagate the exception to readers
            _buffer.Writer.Complete(ex);
        }
        finally
        {
            // Make sure the channel is marked as complete when we exit
            _buffer.Writer.TryComplete();
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MasterDataEncryptionBufferedFactory));
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Cancel the background task
        _cts.Cancel();

        try
        {
            // Wait for the background task to complete with a timeout to avoid hanging
            _backgroundFillingTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
            // Ignore exceptions during disposal
        }

        _cts.Dispose();
    }
}