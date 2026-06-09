using PlikShare.Core.SQLite;

namespace PlikShare.Core.Configuration;

public interface IConfig
{
    int QueueProcessingBatchSize { get; }

    /// <summary>
    /// Per-lane anti-starvation budget for the main DbWriteQueue, indexed by <see cref="DbWritePriority"/>.
    /// A write waiting in a job lane longer than its budget jumps ahead of higher-priority work once, so
    /// a steady stream of UI writes cannot indefinitely starve job completion writes. The <c>Ui</c> lane
    /// entry is unused (UI is always served first). Configure via <c>Queue:DbWriteLaneMaxWaitMs:*</c>.
    /// </summary>
    IReadOnlyList<TimeSpan> DbWritePriorityMaxWaits { get; }

    /// <summary>
    /// Extremely-low-priority jobs (background work) are held back until the queue has been free of
    /// any higher-priority work for at least this long — so background work stays off the hot path
    /// during bursts (e.g. a bulk upload) without a fixed delay. Configure via
    /// <c>Queue:ExtremelyLowPriority:IdleGracePeriodSeconds</c>.
    /// </summary>
    TimeSpan ExtremelyLowPriorityIdleGracePeriod { get; }

    /// <summary>
    /// Anti-starvation valve: an extremely-low-priority job that has waited at least this long runs
    /// regardless of higher-priority activity. Configure via
    /// <c>Queue:ExtremelyLowPriority:MaxWaitSeconds</c>.
    /// </summary>
    TimeSpan ExtremelyLowPriorityMaxWait { get; }

    string AppUrl { get; }

    bool ForcePasswordLoginEnabled { get; }

    /// <summary>
    /// Optional override for the ffmpeg binary location. When set, FfmpegService probes
    /// and invokes this exact path; when null or empty, the service falls back to "ffmpeg"
    /// (resolved through the process PATH). Configure via <c>Ffmpeg:Path</c>.
    /// </summary>
    string? FfmpegPath { get; }
}