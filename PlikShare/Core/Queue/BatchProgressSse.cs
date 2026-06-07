using Microsoft.AspNetCore.Http;
using PlikShare.Core.Clock;
using PlikShare.Core.Utils;

namespace PlikShare.Core.Queue;

// Generic counts-only Server-Sent Events stream for any queue batch. Pushes an initial snapshot,
// then a fresh {total, completed, failed, pending} on every batch notification (throttled to
// 1/s), and closes once Pending hits 0. The richer thumbnail stream keeps its own endpoint
// (it also carries per-file outstanding ids + ready deltas); everything that only needs a
// progress bar uses this.
public static class BatchProgressSse
{
    public static async Task Stream(
        HttpContext httpContext,
        Guid batchId,
        QueueBatchNotifier notifier,
        Func<BatchProgressQuery.Counts> getCounts,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var response = httpContext.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Append("X-Accel-Buffering", "no");

        using var subscription = notifier.Subscribe(batchId);

        var initial = getCounts();
        await Write(response, initial, cancellationToken);

        if (initial.Pending == 0)
            return;

        var keepAlive = TimeSpan.FromSeconds(20);
        var minPushInterval = TimeSpan.FromSeconds(1);
        var lastPushAt = clock.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            subscription.DrainPending();

            bool signalled;

            using (var keepAliveCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken))
            {
                keepAliveCts.CancelAfter(keepAlive);

                try
                {
                    signalled = await subscription.WaitForSignalAsync(keepAliveCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    await response.WriteAsync(": keep-alive\n\n", cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                    continue;
                }
            }

            if (!signalled)
                break;

            var elapsed = clock.UtcNow - lastPushAt;

            if (elapsed < minPushInterval)
            {
                try
                {
                    await Task.Delay(minPushInterval - elapsed, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                subscription.DrainPending();
            }

            var counts = getCounts();
            await Write(response, counts, cancellationToken);
            lastPushAt = clock.UtcNow;

            if (counts.Pending == 0)
                break;
        }
    }

    private static async Task Write(
        HttpResponse response,
        BatchProgressQuery.Counts counts,
        CancellationToken cancellationToken)
    {
        var dto = new BatchProgressDto
        {
            Total = counts.Total,
            Completed = counts.Completed,
            Failed = counts.Failed,
            Pending = counts.Pending
        };

        await response.WriteAsync(
            text: $"data: {Json.Serialize(dto)}\n\n",
            cancellationToken: cancellationToken);

        await response.Body.FlushAsync(cancellationToken);
    }
}
