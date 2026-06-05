using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using Serilog;

namespace PlikShare.Core.Queue;

public class Queue(
    PlikShareDb plikShareDb,
    DbWriteQueue dbWriteQueue,
    IClock clock,
    QueueJobStatusDecisionEngine queueJobStatusDecisionEngine,
    QueueJobInfoProvider queueJobInfoProvider,
    QueueBatchNotifier batchNotifier) : IQueue
{
    private static int MaxRetryCount { get; } = 3;
    private static int[] RetryDelaysInSeconds { get; } = [3 * 60, 5 * 60, 15 * 60];

    public QueueStatus GetNewJobStatus(string jobType)
    {
        return queueJobStatusDecisionEngine.GetNewJobStatus(jobType);
    }

    public List<int> UnlockStaleProcessingQueueJobs()
    {
        using var connection = plikShareDb.OpenConnection();
        
        return connection
            .Cmd(
                sql: $@"
                    UPDATE q_queue
                    SET q_status = '{QueueStatus.Pending}'
                    WHERE 
                        q_status = '{QueueStatus.Processing}' 
                    RETURNING 
                        q_id
                ",
                readRowFunc: reader => reader.GetInt32(0))
            .Execute();
    }

    public void UnlockBlockedQueueJobs()
    {
        using var connection = plikShareDb.OpenConnection();

        var blockedJobTypes = connection
            .Cmd(
                sql: $@"
                    SELECT DISTINCT q_job_type
                    FROM q_queue
                    WHERE q_status = '{QueueStatus.Blocked}'
                ",
                readRowFunc: reader => reader.GetString(0))
            .Execute();

        var jobTypesToUnlock = blockedJobTypes
            .Where(jobType => queueJobStatusDecisionEngine.GetNewJobStatus(jobType) == QueueStatus.PendingStatus)
            .ToList();
        
        if(jobTypesToUnlock.Count == 0)
            return;

        var unblockedJobIds = connection
            .Cmd(
                sql: $@"
                    UPDATE q_queue
                    SET q_status = '{QueueStatus.Pending}'
                    WHERE 
                        q_job_type IN (
                          SELECT value FROM json_each($jobTypes)
                        ) 
                        AND q_status = '{QueueStatus.Blocked}' 
                    RETURNING 
                        q_id
                ",
                readRowFunc: reader => reader.GetInt32(0))
            .WithJsonParameter("$jobTypes", jobTypesToUnlock)
            .Execute();

        Log.Information("Following QueueJobs status was changed from Blocked -> Pending: {QueueJobIds}",
            unblockedJobIds);
    }
    
    public QueueSagaId InsertSaga<T>(
        Guid correlationId, 
        string onCompletedJobType, 
        T onCompletedJobDefinition, 
        SqliteWriteContext dbWriteContext,
        SqliteTransaction? transaction)
    {
       return dbWriteContext
            .OneRowCmd(
                sql: @"
                    INSERT INTO qs_queue_sagas(
                        qs_on_completed_queue_job_type,
                        qs_on_completed_queue_job_definition,
                        qs_correlation_id
                    ) 
                    VALUES (
                        $jobType,
                        $jobDefinition,
                        $correlationId
                    )
                    RETURNING qs_id                    
                ",
                readRowFunc: reader => new QueueSagaId(
                    Value: reader.GetInt32(0)),
                transaction: transaction)
            .WithParameter("$jobType", onCompletedJobType)
            .WithJsonParameter("$jobDefinition", onCompletedJobDefinition)
            .WithParameter("$correlationId", correlationId)
            .ExecuteOrThrow();
    }
    
    public QueueJobId EnqueueOrThrow<T>(
        Guid correlationId,
        string jobType,
        T definition,
        DateTimeOffset executeAfterDate,
        string? debounceId,
        QueueSagaId? sagaId,
        Guid? batchId,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction? transaction)
    {
        var result = Enqueue(
            correlationId,
            jobType,
            definition,
            executeAfterDate,
            debounceId,
            sagaId,
            batchId,
            dbWriteContext,
            transaction);

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Could not enqueue job '{jobType}' with definition '{definition}'");
        }

        return result.Value;
    }

    public SQLiteOneRowCommandResult<QueueJobId> Enqueue<T>(
        Guid correlationId,
        string jobType,
        T definition,
        DateTimeOffset executeAfterDate,
        string? debounceId,
        QueueSagaId? sagaId,
        Guid? batchId,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction? transaction)
    {
        var status = queueJobStatusDecisionEngine.GetNewJobStatus(
            jobType);

        var definitionJson = Json.Serialize(
            item: definition);

        return dbWriteContext
            .OneRowCmd(
                sql: @"
                    INSERT INTO q_queue (
                        q_job_type,
                        q_definition,
                        q_status,
                        q_failed_retries_count,
                        q_execute_after_date,
                        q_enqueued_at,
                        q_correlation_id,
                        q_debounce_id,
                        q_saga_id,
                        q_batch_id,
                        q_job_category,
                        q_job_priority
                    )
                    VALUES (
                        $jobType,
                        $definition,
                        $status,
                        0,
                        $executeAfterDate,
                        $enqueuedAt,
                        $correlationId,
                        $debounceId,
                        $sagaId,
                        $batchId,
                        $jobCategory,
                        $jobPriority
                    )
                    ON CONFLICT (q_debounce_id)
                    DO UPDATE SET
                            q_execute_after_date = MAX(
                                EXCLUDED.q_execute_after_date,
                                q_queue.q_execute_after_date)
                    RETURNING
                        q_id;
                ",
                readRowFunc: reader => new QueueJobId(
                    Value: reader.GetInt32(0)),
                transaction: transaction)
            .WithParameter("$jobType", jobType)
            .WithParameter("$definition", definitionJson)
            .WithParameter("$status", status.Value)
            .WithParameter("$executeAfterDate", executeAfterDate)
            .WithParameter("$enqueuedAt", clock.UtcNow)
            .WithParameter("$correlationId", correlationId)
            .WithParameter("$debounceId", debounceId)
            .WithParameter("$sagaId", sagaId?.Value)
            .WithParameter("$batchId", batchId)
            .WithParameter("$jobCategory", (int)queueJobInfoProvider.GetJobCategory(jobType))
            .WithParameter("$jobPriority", queueJobInfoProvider.GetJobPriority(jobType))
            .Execute();
    }

    public List<QueueJobId> EnqueueBulk(
        Guid correlationId, 
        List<BulkQueueJobEntity> definitions, 
        DateTimeOffset executeAfterDate, 
        SqliteWriteContext dbWriteContext,
        SqliteTransaction? transaction)
    {
        if (definitions.Count == 0)
            return [];
        
        var result = dbWriteContext
            .Cmd(
                sql: @"
                    INSERT INTO q_queue (
                        q_job_type,
                        q_definition,
                        q_status,
                        q_failed_retries_count,
                        q_execute_after_date,
                        q_enqueued_at,
                        q_correlation_id,
                        q_debounce_id,
                        q_saga_id,
                        q_batch_id,
                        q_job_category,
                        q_job_priority
                    )
                    SELECT
                        json_extract(value, '$.jobType'),
                        json_extract(value, '$.definition'),
                        json_extract(value, '$.status'),
                        0,
                        $executeAfterDate,
                        $enqueuedAt,
                        $correlationId,
                        NULL,
                        json_extract(value, '$.sagaId'),
                        json_extract(value, '$.batchId'),
                        json_extract(value, '$.jobCategory'),
                        json_extract(value, '$.jobPriority')
                    FROM
                        json_each($definitions)
                    RETURNING
                        q_id;
                ",
                readRowFunc: reader => new QueueJobId(
                    Value: reader.GetInt32(0)),
                transaction: transaction)
            .WithParameter("$executeAfterDate", executeAfterDate)
            .WithParameter("$enqueuedAt", clock.UtcNow)
            .WithParameter("$correlationId", correlationId)
            .WithJsonParameter("$definitions", definitions)
            .Execute();

        if (result.Count != definitions.Count)
        {
            throw new InvalidDataException(
                $"Number of inserted queue jobs ({result.Count}) does not match the number of provided job definitions ({definitions.Count}). " +
                $"Correlation ID: {correlationId}. " +
                "This indicates a potential data integrity issue during bulk job insertion.");
        }

        return result;
    }
    
    public BulkQueueJobEntity CreateBulkEntity<T>(
        string jobType,
        T definition,
        QueueSagaId? sagaId,
        Guid? batchId)
    {
        return new BulkQueueJobEntity
        {
            Definition = Json.Serialize(
                definition),

            JobType = jobType,

            Status = queueJobStatusDecisionEngine
                .GetNewJobStatus(jobType)
                .Value,

            SagaId = sagaId?.Value,

            JobCategory = (int)queueJobInfoProvider.GetJobCategory(jobType),

            JobPriority = queueJobInfoProvider.GetJobPriority(jobType),

            BatchId = batchId
        };
    }
   
    public async Task HandleJobFailure(
        QueueJob job, 
        Exception exception, 
        string consumerIdentity,
        CancellationToken cancellationToken)
    {
        Log.Error(exception, "Job '{JobIdentity}' thrown error. Job will be scheduled to next attempt or marked as failed ({ConsumerIdentity}). ",
            job.Identity,
            consumerIdentity);

        var newStatus = GetNewStatus(job);

        switch (newStatus)
        {
            case QueueStatus.Failed:
                await MarkJobAsFailed(
                    job: job,
                    exception: exception,
                    consumerIdentity: consumerIdentity,
                    cancellationToken: cancellationToken);

                break;

            case QueueStatus.Pending:
                await ScheduleJobRetry(
                    job: job,
                    exception: exception,
                    consumerIdentity: consumerIdentity,
                    cancellationToken: cancellationToken);

                break;

            default:
                throw new ArgumentOutOfRangeException(
                    $"Unknown QueueStatus: '{newStatus}' (DbOnly Queue Consumer)");
        }

        NotifyBatch(job);
    }

    private void NotifyBatch(in QueueJob job)
    {
        // Push batch progress to any SSE subscriber. Best-effort: a failed notification must never
        // affect job processing.
        if (job.BatchId is not null)
        {
            try
            {
                batchNotifier.Notify(
                    job.BatchId.Value);
            }
            catch (Exception e)
            {
                Log.Warning(e, "Failed to notify batch {BatchId} subscribers", job.BatchId);
            }
        }
    }
    
    private string GetNewStatus(in QueueJob job)
    {
        return job.FailedRetriesCount >= MaxRetryCount
            ? QueueStatus.Failed
            : QueueStatus.Pending;
    }

    private Task MarkJobAsFailed(
        QueueJob job,
        Exception exception,
        string consumerIdentity,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: dbWriteContext =>
            {
                try
                {
                    var result = dbWriteContext
                        .OneRowCmd(
                            sql: $@"
                            UPDATE 
                                q_queue 
                            SET 
                                q_status = '{QueueStatus.Failed}', 
                                q_failed_at = $now,
                                q_failed_retries_count = $failedRetriesCount
                            WHERE 
                                q_id = $jobId
                            RETURNING
                                q_id
                        ",
                            readRowFunc: reader => reader.GetInt32(0))
                        .WithParameter("$now", clock.UtcNow)
                        .WithParameter("$jobId", job.Id)
                        .WithParameter("$failedRetriesCount", job.FailedRetriesCount + 1)
                        .Execute();

                    if (result.IsEmpty)
                    {
                        Log.Fatal(
                            exception,
                            "Job '{JobIdentity}' failed, but system was not able to mark is as failed! ({ConsumerIdentity})",
                            job.Identity,
                            consumerIdentity);
                    }
                    else
                    {
                        Log.Error(
                            exception,
                            "Job '{JobIdentity}' was marked as failed. ({ConsumerIdentity})",
                            job.Identity,
                            consumerIdentity);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Something went wrong while marking Job '{JobIdentity}' as failed ({ConsumerIdentity})",
                        job.Identity,
                        consumerIdentity);

                    throw;
                }
            },
            cancellationToken: cancellationToken);
    }

    private Task ScheduleJobRetry(
        QueueJob job, 
        Exception exception, 
        string consumerIdentity, 
        CancellationToken cancellationToken)
    {
        var nextAttemptDate = GetNextAttemptDate(
            job: job,
            consumerIdentity: consumerIdentity);

        return dbWriteQueue.Execute(
            operationToEnqueue: dbWriteContext =>
            {
                try
                {
                    var result = dbWriteContext
                        .OneRowCmd(
                            sql: $@"
                                UPDATE 
                                    q_queue
                                SET
                                    q_status = '{QueueStatus.Pending}',
                                    q_processing_started_at = NULL,
                                    q_execute_after_date = $nextAttemptDate,
                                    q_failed_retries_count = $failedRetriesCount
                                WHERE
                                    q_id = $jobId
                                RETURNING
                                    q_id
                            ",
                            readRowFunc: reader => reader.GetInt32(0))
                        .WithParameter("$nextAttemptDate", nextAttemptDate)
                        .WithParameter("$jobId", job.Id)
                        .WithParameter("$failedRetriesCount", job.FailedRetriesCount + 1)
                        .Execute();

                    if (result.IsEmpty)
                    {
                        Log.Fatal(
                            exception,
                            "Job '{JobIdentity}' failed, but system was not able to schedule it for retry at {NextAttemptDate}! ({ConsumerIdentity})",
                            job.Identity, nextAttemptDate, consumerIdentity);
                    }
                    else
                    {
                        Log.Error(
                            exception,
                            "Job '{JobIdentity}' will be retried at {NextAttemptDate}  ({ConsumerIdentity}).",
                            job.Identity,
                            nextAttemptDate,
                            consumerIdentity);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Something went wrong while marking Job '{JobIdentity}' for retry at {NextAttemptDate}  ({ConsumerIdentity})",
                        job.Identity,
                        nextAttemptDate,
                        consumerIdentity);

                    throw;
                }
            },
            cancellationToken: cancellationToken);
    }

    private DateTimeOffset GetNextAttemptDate(
        in QueueJob job,
        string consumerIdentity)
    {
        if (job.FailedRetriesCount >= RetryDelaysInSeconds.Length)
        {
            throw new InvalidOperationException(
                $"No retry delay defined for {job.FailedRetriesCount} failed attempt " +
                $"for job '{job.Identity}'  ({consumerIdentity}).");
        }

        var secondsOffset = RetryDelaysInSeconds[job.FailedRetriesCount];

        return clock.UtcNow.AddSeconds(secondsOffset);
    }

    public void HandleJobSuccess(
        in QueueJob job,
        QueueJobResult result,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        string consumerIdentity)
    {
        switch (result.Code)
        {
            case QueueJobResultCode.Success:
                MarkJobAsCompleted(
                    jobId: job.Id,
                    resultJson: result.ResultJson,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction,
                    consumerIdentity: consumerIdentity);
                break;

            case QueueJobResultCode.Blocked:
                MarkJobAsBlocked(
                    jobId: job.Id,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction,
                    consumerIdentity: consumerIdentity);
                break;

            case QueueJobResultCode.NeedsRetry:
                HandleSoftRetry(
                    job: job,
                    delay: result.RetryDelay,
                    maxAttempts: result.SoftRetryMaxAttempts,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction,
                    consumerIdentity: consumerIdentity);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown {nameof(QueueJobResultCode)} value: '{result}' ({consumerIdentity})");
        }
    }

    public async Task HandleJobSuccess(
        QueueJob job,
        QueueJobResult result,
        string consumerIdentity,
        CancellationToken cancellationToken)
    {
        await dbWriteQueue.Execute(
            operationToEnqueue: dbWriteContext =>
            {
                using var transaction = dbWriteContext.Connection.BeginTransaction();

                try
                {
                    HandleJobSuccess(
                        job: job,
                        result: result,
                        dbWriteContext: dbWriteContext,
                        transaction: transaction,
                        consumerIdentity: consumerIdentity);

                    transaction.Commit();
                }
                catch (Exception)
                {
                    transaction.Rollback();

                    throw;
                }
            },
            cancellationToken: cancellationToken);

        NotifyBatch(job);
    }

    private void HandleSoftRetry(
        QueueJob job,
        TimeSpan delay,
        int maxAttempts,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        string consumerIdentity)
    {
        if (job.SoftRetriesLeft is null)
        {
            var initialBudget = Math.Max(0, maxAttempts - 1);

            if (initialBudget == 0)
            {
                MarkJobAsSoftRetryExhausted(
                    jobId: job.Id,
                    dbWriteContext: dbWriteContext,
                    transaction: transaction,
                    consumerIdentity: consumerIdentity);

                Log.Warning(
                    "Job '{JobIdentity}' soft-retry budget of {MaxAttempts} exhausted on first attempt — marking as Failed ({ConsumerIdentity}).",
                    job.Identity, maxAttempts, consumerIdentity);
                return;
            }

            ScheduleSoftRetry(
                jobId: job.Id,
                softRetriesLeft: initialBudget,
                delay: delay,
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                consumerIdentity: consumerIdentity);

            Log.Information(
                "Job '{JobIdentity}' soft-retry seeded to {SoftRetriesLeft} (max {MaxAttempts}), next attempt in {Delay} ({ConsumerIdentity}).",
                job.Identity, initialBudget, maxAttempts, delay, consumerIdentity);
            return;
        }

        if (job.SoftRetriesLeft.Value <= 0)
        {
            MarkJobAsSoftRetryExhausted(
                jobId: job.Id,
                dbWriteContext: dbWriteContext,
                transaction: transaction,
                consumerIdentity: consumerIdentity);

            Log.Warning(
                "Job '{JobIdentity}' soft-retry budget exhausted — marking as Failed ({ConsumerIdentity}).",
                job.Identity, consumerIdentity);
            return;
        }

        var nextBudget = job.SoftRetriesLeft.Value - 1;

        ScheduleSoftRetry(
            jobId: job.Id,
            softRetriesLeft: nextBudget,
            delay: delay,
            dbWriteContext: dbWriteContext,
            transaction: transaction,
            consumerIdentity: consumerIdentity);

        Log.Information(
            "Job '{JobIdentity}' soft-retry decremented to {SoftRetriesLeft}, next attempt in {Delay} ({ConsumerIdentity}).",
            job.Identity, nextBudget, delay, consumerIdentity);
    }

    private void ScheduleSoftRetry(
        int jobId,
        int softRetriesLeft,
        TimeSpan delay,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        string consumerIdentity)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: @"
                    UPDATE q_queue
                    SET
                        q_status = $pendingStatus,
                        q_processing_started_at = NULL,
                        q_execute_after_date = $executeAfterDate,
                        q_soft_retries_left = $softRetriesLeft
                    WHERE q_id = $jobId
                    RETURNING q_id
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$jobId", jobId)
            .WithParameter("$pendingStatus", QueueStatus.Pending)
            .WithParameter("$executeAfterDate", clock.UtcNow.Add(delay))
            .WithParameter("$softRetriesLeft", softRetriesLeft)
            .Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Could not schedule soft-retry for QueueJob '{jobId}' with delay '{delay}' ({consumerIdentity})");
        }
    }

    private void MarkJobAsSoftRetryExhausted(
        int jobId,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        string consumerIdentity)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: $@"
                    UPDATE q_queue
                    SET
                        q_status = '{QueueStatus.Failed}',
                        q_failed_at = $now
                    WHERE q_id = $jobId
                    RETURNING q_id
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$jobId", jobId)
            .WithParameter("$now", clock.UtcNow)
            .Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Could not mark QueueJob '{jobId}' as Failed after soft-retry exhaustion ({consumerIdentity})");
        }
    }

    private void MarkJobAsBlocked(
       int jobId,
       SqliteWriteContext dbWriteContext,
       SqliteTransaction transaction,
       string consumerIdentity)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: @"
                    UPDATE q_queue
                    SET q_status = $blockedStatus
                    WHERE q_id = $jobId
                    RETURNING q_id
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$jobId", jobId)
            .WithParameter("$blockedStatus", QueueStatus.Blocked)
            .Execute();

        if (result.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Could not update QueueJob '{jobId}' status to '{QueueStatus.Blocked}' ({consumerIdentity})");
        }
    }

    private void MarkJobAsCompleted(
        int jobId,
        string? resultJson,
        SqliteWriteContext dbWriteContext,
        SqliteTransaction transaction,
        string consumerIdentity)
    {
        dbWriteContext.Connection.CreateFunction(
            "app_redact_ephemeral",
            (string? json) => EphemeralValueRedactor.Redact(json),
            isDeterministic: true);

        var insertQueueCompletedResult = dbWriteContext
            .OneRowCmd(
                sql: @"
                    INSERT INTO qc_queue_completed (
                        qc_id,
                        qc_job_type,
                        qc_definition,
                        qc_failed_retries_count,
                        qc_enqueued_at,
                        qc_execute_after_date,
                        qc_completed_at,
                        qc_correlation_id,
                        qc_batch_id,
                        qc_result
                    )
                    SELECT
                        q_id,
                        q_job_type,
                        app_redact_ephemeral(q_definition),
                        q_failed_retries_count,
                        q_enqueued_at,
                        q_execute_after_date,
                        $completedAt,
                        q_correlation_id,
                        q_batch_id,
                        $result
                    FROM
                        q_queue
                    WHERE
                        q_id = $jobId
                    RETURNING
                        qc_id
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$jobId", jobId)
            .WithParameter("$completedAt", clock.UtcNow)
            .WithParameter("$result", resultJson)
            .Execute();

        if (insertQueueCompletedResult.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Could not insert QueueCompletedJob for QueueJob with id '{jobId}' ({consumerIdentity})");
        }

        var deleteQueueJobResult = dbWriteContext
            .OneRowCmd(
                sql: @"
                    DELETE FROM 
                        q_queue
                    WHERE 
                        q_id = $jobId
                    RETURNING
                        q_id
                ",
                readRowFunc: reader => reader.GetInt32(0),
                transaction: transaction)
            .WithParameter("$jobId", jobId)
            .Execute();

        if (deleteQueueJobResult.IsEmpty)
        {
            throw new InvalidOperationException(
                $"Could not delete QueueJob with id '{jobId}' ({consumerIdentity})");
        }
    }
}