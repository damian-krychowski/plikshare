using Serilog;

namespace PlikShare.Core.Queue;

public static class QueueStartupExtensions
{
    public static void InitializeQueue(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var queue = app.Services.GetRequiredService<IQueue>();

        var unlockedQueueJobIds = queue.UnlockStaleProcessingQueueJobs();

        if (unlockedQueueJobIds.Any())
        {
            Log.Information("[INITIALIZATION] Queue initialization finished. Following jobs were fixed from stale 'Processing' status: {QueueJobIds}.",
                unlockedQueueJobIds);
        }
        else
        {
            Log.Information("[INITIALIZATION] Queue initialization finished. No stale or blocked queue jobs were found.");
        }
    }
}