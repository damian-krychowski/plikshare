using System.Threading.Channels;
using Serilog;

namespace PlikShare.Core.Utils;

public class RateLimiter : IDisposable
{
    public int MaxConcurrentUploads { get; }
    public int TokensPerSecond { get; }

    private readonly SemaphoreSlim _semaphore;
    private readonly Channel<Unit> _tokenBucket;
    private readonly Task _replenishmentTask;
    private readonly CancellationTokenSource _cts;

    public RateLimiter(
        int maxConcurrentUploads,
        int tokensPerSecond)
    {
        MaxConcurrentUploads = maxConcurrentUploads;
        TokensPerSecond = tokensPerSecond;
        _semaphore = new SemaphoreSlim(maxConcurrentUploads);
        _tokenBucket = Channel.CreateBounded<Unit>(new BoundedChannelOptions(tokensPerSecond)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        _cts = new CancellationTokenSource();
        _replenishmentTask = ReplenishTokens(_cts.Token);
    }

    private async Task ReplenishTokens(CancellationToken cancellationToken)
    {
        var delayTime = 1000 / TokensPerSecond;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _tokenBucket.Writer.WriteAsync(Unit.Value, cancellationToken);
                await Task.Delay(delayTime, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in token replenishment");
            }
        }
    }

    public async Task<IDisposable> AcquirePermission(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        await _tokenBucket.Reader.ReadAsync(cancellationToken);

        return new UploadPermission(_semaphore);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _semaphore.Dispose();
    }

    private class UploadPermission(SemaphoreSlim semaphore) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                semaphore.Release();
                _disposed = true;
            }
        }
    }

    private struct Unit
    {
        public static readonly Unit Value = new();
    }
}