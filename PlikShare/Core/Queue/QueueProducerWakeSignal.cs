namespace PlikShare.Core.Queue;

public class QueueProducerWakeSignal
{
    private readonly SemaphoreSlim _signal = new(
        initialCount: 0,
        maxCount: 1);

    public void Pulse()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    public Task WaitAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        return _signal.WaitAsync(
            timeout,
            cancellationToken);
    }
}
