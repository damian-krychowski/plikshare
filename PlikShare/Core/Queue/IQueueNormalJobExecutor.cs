using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;

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
        DbWriteQueue.Context dbWriteContext,
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
    TimeSpan RetryDelay = default)
{
    public static QueueJobResult Success => new(
        QueueJobResultCode.Success);

    public static QueueJobResult Blocked => new(
        QueueJobResultCode.Blocked);

    public static QueueJobResult NeedsRetry(TimeSpan delay) => new(
        QueueJobResultCode.NeedsRetry,
        delay);
};

public static class QueueJobPriority
{
    public const int ExtremelyHigh = 0;
    public const int High = 1;
    public const int Normal = 2;
    public const int Low = 3;
    public const int ExtremelyLow = 4;
}