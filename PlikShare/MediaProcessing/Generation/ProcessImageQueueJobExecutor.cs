using PlikShare.Core.Queue;
using Serilog;

namespace PlikShare.MediaProcessing.Generation;

public class ProcessImageQueueJobExecutor() : IQueueLongRunningJobExecutor
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<ProcessImageQueueJobExecutor>();

    // How much of a video file to download for thumbnail extraction. Fast-start mp4 stores moov +
    // first samples in the first few MB; 8 MB covers virtually all 1080p consumer recordings and
    // most short 4K. Higher-bitrate / longer 4K sources may fail demux at this size — acceptable
    // trade-off vs hauling gigabytes. Pair with thumbnail filter's lower frame count (n=25) to
    // keep the in-RAM window proportional.
    private const long VideoRangeLimit = 8L * 1024 * 1024;

    public static string StaticJobType => ProcessImageQueueJobType.Value;
    public static int StaticPriority => QueueJobPriority.Normal;

    public string JobType => StaticJobType;
    public int Priority => StaticPriority;

    public async Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return QueueJobResult.Success;
    }
}
