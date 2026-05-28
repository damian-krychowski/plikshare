using Microsoft.Data.Sqlite;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Queue;

public interface IQueueNormalJobExecutor
{
    string JobType { get; }
    int Priority { get; }
    
    Task<QueueJobResult> Execute(
        string definitionJson, 
        Guid correlationId, 
        CancellationToken cancellationToken);
}

public interface IQueueLongRunningJobExecutor
{
    string JobType { get; }
    int Priority { get; }

    Task<QueueJobResult> Execute(
        string definitionJson,
        Guid correlationId,
        CancellationToken cancellationToken);
}

public interface IQueueDbOnlyJobExecutor
{
    string JobType { get; }
    int Priority { get; }

    (QueueJobResult Result, Func<CancellationToken, ValueTask> SideEffectsToRun) Execute(
        string definitionJson, 
        Guid correlationId,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction);
}

public enum QueueJobResultCode
{
    //when everything went
    Success = 0,
    
    //when for some reason scheduled action cannot be executed at given moment
    //for example, you cannot send email if user has not configured any email provider
    Blocked,


    //when job cannot definitely be finished at the moment and needs to be rerun after some time
    //for example, when you poll some external job status, and it's still in progress
    NeedsRetry
}

public readonly record struct QueueJobResult(
    QueueJobResultCode Code,
    TimeSpan RetryDelay = default,
    int SoftRetryMaxAttempts = 0,
    string? ResultJson = null)
{
    public static QueueJobResult Success => new(
        QueueJobResultCode.Success);

    /// <summary>
    /// Job finished successfully but produced an outcome payload worth keeping — persisted into
    /// <c>qc_queue_completed.qc_result</c>. Use for partial outcomes (eg. some thumbnail variants
    /// failed) that should surface to the user without failing the job on the queue.
    /// </summary>
    public static QueueJobResult SuccessWithResult(string resultJson) => new(
        QueueJobResultCode.Success,
        ResultJson: resultJson);

    public static QueueJobResult Blocked => new(
        QueueJobResultCode.Blocked);

    /// <summary>
    /// Schedule the job to run again after <paramref name="delay"/>. The queue tracks how
    /// many soft retries are left in <c>q_soft_retries_left</c>: on the first <c>NeedsRetry</c>
    /// the column is seeded to <paramref name="maxAttempts"/> - 1, and decremented on each
    /// subsequent soft retry. When the budget is exhausted the job falls through to the
    /// hard-retry mechanism (<see cref="QueueJobResultCode"/>) — i.e. it is treated as a
    /// real failure with the standard back-off and final <c>Failed</c> status.
    /// </summary>
    public static QueueJobResult NeedsRetry(int maxAttempts, TimeSpan delay) => new(
        QueueJobResultCode.NeedsRetry,
        delay,
        maxAttempts);
};

public static class QueueJobPriority
{
    public const int ExtremelyHigh = 0;
    public const int High = 1;
    public const int Normal = 2;
    public const int Low = 3;
    public const int ExtremelyLow = 4;
}