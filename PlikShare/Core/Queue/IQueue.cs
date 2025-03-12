using Microsoft.Data.Sqlite;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.Core.Queue;

public interface IQueue
{
    QueueSagaId InsertSaga<T>(
        Guid correlationId,
        string onCompletedJobType,
        T onCompletedJobDefinition,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction? transaction);

    SQLiteOneRowCommandResult<QueueJobId> Enqueue<T>(
        Guid correlationId,
        string jobType,
        T definition,
        DateTimeOffset executeAfterDate,
        string? debounceId,
        QueueSagaId? sagaId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction? transaction);

    QueueJobId EnqueueOrThrow<T>(
        Guid correlationId,
        string jobType,
        T definition,
        DateTimeOffset executeAfterDate,
        string? debounceId,
        QueueSagaId? sagaId,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction? transaction);

    List<QueueJobId> EnqueueBulk(
        Guid correlationId,
        List<BulkQueueJobEntity> definitions,
        DateTimeOffset executeAfterDate,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction? transaction);

    QueueStatus GetNewJobStatus(string jobType);

    List<int> UnlockStaleProcessingQueueJobs();
    void UnlockBlockedQueueJobs();

    public BulkQueueJobEntity CreateBulkEntity<T>(
        string jobType,
        T definition,
        QueueSagaId? sagaId);
    
    public Task HandleJobFailure(
        QueueJob job,
        Exception exception,
        string consumerIdentity,
        CancellationToken cancellationToken);

    public void HandleJobSuccess(
        in QueueJob job,
        QueueJobResult result,
        DbWriteQueue.Context dbWriteContext,
        SqliteTransaction transaction,
        string consumerIdentity);
    
    public Task HandleJobSuccess(
        QueueJob job,
        QueueJobResult result,
        string consumerIdentity,
        CancellationToken cancellationToken);
}