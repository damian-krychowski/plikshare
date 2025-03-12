using System.Threading.Channels;

namespace PlikShare.Core.Queue;

public class QueueChannels : IAsyncDisposable
{
    private readonly int _capacity;
    private long _normalJobsCount;
    private long _longRunningJobsCount;
    private long _dbOnlyJobsCount;
    private readonly CancellationTokenSource _cleanupCts;
    private volatile bool _isDisposed;

    public QueueChannels(int capacity)
    {
        _capacity = capacity;
        _cleanupCts = new CancellationTokenSource();
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = false
        };
        NormalJobs = Channel.CreateBounded<QueueJob>(options);
        LongRunningJobs = Channel.CreateBounded<QueueJob>(options);
        DbOnlyJobs = Channel.CreateBounded<QueueJob>(options);
    }

    private Channel<QueueJob> NormalJobs { get; }
    private Channel<QueueJob> LongRunningJobs { get; }
    private Channel<QueueJob> DbOnlyJobs { get; }

    public async Task WriteNormalJobAsync(QueueJob job, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cleanupCts.Token);
        await NormalJobs.Writer.WriteAsync(job, linkedCts.Token);
        Interlocked.Increment(ref _normalJobsCount);
    }

    public async Task WriteLongRunningJobAsync(QueueJob job, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cleanupCts.Token);
        await LongRunningJobs.Writer.WriteAsync(job, linkedCts.Token);
        Interlocked.Increment(ref _longRunningJobsCount);
    }

    public async Task WriteDbOnlyJobAsync(QueueJob job, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cleanupCts.Token);
        await DbOnlyJobs.Writer.WriteAsync(job, linkedCts.Token);
        Interlocked.Increment(ref _dbOnlyJobsCount);
    }

    public async Task<QueueJob> ReadNormalJobAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cleanupCts.Token);
        var job = await NormalJobs.Reader.ReadAsync(linkedCts.Token);
        Interlocked.Decrement(ref _normalJobsCount);
        return job;
    }

    public async Task<QueueJob> ReadLongRunningJobAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cleanupCts.Token);
        var job = await LongRunningJobs.Reader.ReadAsync(linkedCts.Token);
        Interlocked.Decrement(ref _longRunningJobsCount);
        return job;
    }

    public async Task<QueueJob> ReadDbOnlyJobAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cleanupCts.Token);
        var job = await DbOnlyJobs.Reader.ReadAsync(linkedCts.Token);
        Interlocked.Decrement(ref _dbOnlyJobsCount);
        return job;
    }

    public int GetNormalJobsCount() => (int)Interlocked.Read(ref _normalJobsCount);
    public int GetLongRunningJobsCount() => (int)Interlocked.Read(ref _longRunningJobsCount);
    public int GetDbOnlyJobsCount() => (int)Interlocked.Read(ref _dbOnlyJobsCount);

    public CapacitySnapshot GetCapacitySnapshot() => new CapacitySnapshot(
        DbOnlyJobs: _capacity - GetDbOnlyJobsCount(),
        NormalJobs: _capacity - GetNormalJobsCount(),
        LongRunningJobs: _capacity - GetLongRunningJobsCount());

    public readonly record struct CapacitySnapshot(
        int DbOnlyJobs,
        int NormalJobs,
        int LongRunningJobs);

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(QueueChannels));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        try
        {
            // Signal cleanup is starting
            await _cleanupCts.CancelAsync();

            // Wait for any pending writes and complete all channels
            await Task.WhenAll(
                CompleteChannelAsync(NormalJobs),
                CompleteChannelAsync(LongRunningJobs),
                CompleteChannelAsync(DbOnlyJobs)
            );
        }
        finally
        {
            _cleanupCts.Dispose();
        }
    }

    private static async Task CompleteChannelAsync<T>(Channel<T> channel)
    {
        try
        {
            // Wait for any pending writes
            await channel.Writer.WaitToWriteAsync();
            // Mark the channel as complete
            channel.Writer.Complete();
        }
        catch (ChannelClosedException)
        {
            // Channel was already completed, ignore
        }
        catch (OperationCanceledException)
        {
            // Cleanup was cancelled, complete the channel anyway
            channel.Writer.Complete();
        }
    }
}