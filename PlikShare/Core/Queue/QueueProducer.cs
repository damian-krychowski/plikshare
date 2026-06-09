using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Configuration;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using Serilog;
using Serilog.Events;

namespace PlikShare.Core.Queue;

public sealed class QueueProducer : BackgroundService
{
    private readonly TimeSpan _checkBlockedJobsInterval = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _idleWakeInterval = TimeSpan.FromSeconds(1);
    private DateTimeOffset? _blockedJobsCheckedAt = null;

    private readonly SqliteConnection _connection;

    private readonly SqliteCommand _selectJobsBatchCommand;
    private readonly SqliteCommand _markJobsProcessingCommand;
    private readonly SqliteCommand _anySagaExistsCommand;
    private readonly SqliteCommand _deleteCompletedSagasCommand;
    private readonly SqliteCommand _insertQueueSagaJobsCommand;

    // Last time a dequeued batch carried any higher-than-extremely-low work. Extremely-low jobs are
    // released only once this is at least the grace period in the past — see
    // GetBatchOfJobsAndMarkAsProcessing.
    private DateTimeOffset? _higherPriorityActivitySeenAt;

    private readonly IClock _clock;
    private readonly IQueue _queue;
    private readonly QueueChannels _channels;
    private readonly IConfig _config;
    private readonly QueueJobInfoProvider _queueJobInfoProvider;
    private readonly QueueProducerWakeSignal _wakeSignal;
    private readonly CancellationTokenSource _gracefulShutdownCts;
    private bool _disposed;

    public QueueProducer(
        PlikShareDb plikShareDb,
        IClock clock,
        IQueue queue,
        QueueChannels channels,
        IConfig config,
        QueueJobInfoProvider queueJobInfoProvider,
        QueueProducerWakeSignal wakeSignal)
    {
        _clock = clock;
        _queue = queue;
        _channels = channels;
        _config = config;
        _queueJobInfoProvider = queueJobInfoProvider;
        _wakeSignal = wakeSignal;
        _gracefulShutdownCts = new CancellationTokenSource();

        _connection = plikShareDb.OpenConnection();

        _selectJobsBatchCommand = PrepareSelectJobsBatchCommand(_connection);
        _markJobsProcessingCommand = PrepareMarkJobsProcessingCommand(_connection);
        _anySagaExistsCommand = PrepareAnySagaExistsCommand(_connection);
        _deleteCompletedSagasCommand = PrepareDeleteCompletedSagasCommand(_connection);
        _insertQueueSagaJobsCommand = PrepareInsertQueueSagaJobsCommand(_connection);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Create a linked token that combines the service stopping token
            // and our graceful shutdown token
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken,
                _gracefulShutdownCts.Token);

            while (!linkedCts.Token.IsCancellationRequested)
            {
                try
                {
                    await _wakeSignal.WaitAsync(
                        timeout: _idleWakeInterval,
                        cancellationToken: linkedCts.Token);

                    UnlockBlockedJobsIfNeeded();
                    await ProcessQueue(linkedCts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    Log.Warning(ex, "Queue Producer listening for queue jobs cancelled.");
                    break;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Something went wrong during processing the queue");
                }
            }
        }
        finally
        {
            // Ensure we flush any remaining items and clean up resources
            await CleanupAsync();
        }
    }

    private async Task CleanupAsync()
    {
        try
        {
            await _channels.DisposeAsync();

            Log.Information("Queue Producer cleanup completed successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during Queue Producer cleanup");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Signal graceful shutdown
            await _gracefulShutdownCts.CancelAsync();

            // Wait for base cleanup
            await base.StopAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during Queue Producer stop");
        }
    }

    public override void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _gracefulShutdownCts.Dispose();
            _selectJobsBatchCommand.Dispose();
            _markJobsProcessingCommand.Dispose();
            _anySagaExistsCommand.Dispose();
            _deleteCompletedSagasCommand.Dispose();
            _insertQueueSagaJobsCommand.Dispose();
            _connection.Dispose();
        }

        _disposed = true;

        base.Dispose();
    }

    private void UnlockBlockedJobsIfNeeded()
    {
        if (!ShouldUnlockBlockedJobs())
            return;

        _blockedJobsCheckedAt = _clock.UtcNow;
        _queue.UnlockBlockedQueueJobs();
    }

    private bool ShouldUnlockBlockedJobs()
    {
        if (_blockedJobsCheckedAt is null)
            return true;

        return _blockedJobsCheckedAt.Value + _checkBlockedJobsInterval < _clock.UtcNow;
    }

    private async Task ProcessQueue(CancellationToken stoppingToken)
    {
        bool sagasCreatedJobs;

        do
        {
            var allJobsProcessed = false;

            do
            {
                if (stoppingToken.IsCancellationRequested)
                    return;

                var jobsBatchResult = GetBatchOfJobsAndMarkAsProcessing();

                if (jobsBatchResult.Count == 0)
                {
                    allJobsProcessed = true;
                }
                else
                {
                    await PushAllJobsThroughChannel(
                        jobsBatch: jobsBatchResult,
                        stoppingToken: stoppingToken);
                }
            } while (!allJobsProcessed);

            var newSagaJobs = ProcessQueueSagas();
            sagasCreatedJobs = newSagaJobs > 0;

        } while (sagasCreatedJobs);
    }

    private int ProcessQueueSagas()
    {
        var anySagaExists = _anySagaExistsCommand
            .GetRows(
                readRowFunc: reader => reader.GetBoolean(0),
                name: "queue.any_saga_exists")
            .Single();

        if (!anySagaExists)
            return 0;

        using var transaction = _connection.BeginTransaction();

        try
        {
            _deleteCompletedSagasCommand.Parameters.Clear();
            _deleteCompletedSagasCommand.Transaction = transaction;

            var sagas = _deleteCompletedSagasCommand.GetRows(reader =>
            {
                var id = reader.GetInt32(0);
                var jobType = reader.GetString(1);
                var definition = reader.GetString(2);
                var correlationId = reader.GetGuid(3);

                return new QueueSagaJob
                {
                    Id = id,
                    JobType = jobType,
                    Definition = definition,
                    CorrelationId = correlationId,
                    Status = _queue.GetNewJobStatus(jobType).Value,
                    JobCategory = (int)_queueJobInfoProvider.GetJobCategory(jobType),
                    JobPriority = _queueJobInfoProvider.GetJobPriority(jobType)
                };
            },
            name: "queue.delete_completed_sagas");

            _insertQueueSagaJobsCommand.Parameters.Clear();
            _insertQueueSagaJobsCommand.WithParameter("$now", _clock.UtcNow);
            _insertQueueSagaJobsCommand.WithJsonParameter("$definitions", sagas);
            _insertQueueSagaJobsCommand.Transaction = transaction;

            var insertedSagaJobs = _insertQueueSagaJobsCommand.GetRows(
                reader => reader.GetInt32(0),
                name: "queue.insert_saga_jobs");

            transaction.Commit();

            if (sagas.Any() && Log.IsEnabled(LogEventLevel.Information))
            {
                var sagaIds = IdsRange.GroupConsecutiveIds(
                    sagas.Select(x => x.Id));

                var queueJobIds = IdsRange.GroupConsecutiveIds(
                    ids: insertedSagaJobs);

                Log.Information("Following Sagas were completed and converted to Queue Jobs. " +
                                "Saga ids ({SagasCount}): [{SagaIds}], " +
                                "Queue job ids ({QueueJobsCount}): [{QueueJobIds}]",
                    sagas.Count,
                    sagaIds,
                    insertedSagaJobs.Count,
                    queueJobIds);
            }

            return insertedSagaJobs.Count;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while processing completed Queue Sagas.");

            return 0;
        }
    }

    private async Task PushAllJobsThroughChannel(
        List<QueueJob> jobsBatch,
        CancellationToken stoppingToken)
    {
        foreach (var job in jobsBatch)
        {
            var jobCategory = _queueJobInfoProvider.GetJobCategory(
                jobType: job.JobType);
            
            switch (jobCategory)
            {
                case QueueJobCategory.Normal:
                    await _channels.WriteNormalJobAsync(
                        job: job,
                        cancellationToken: stoppingToken);
                    break;

                case QueueJobCategory.LongRunning:
                    await _channels.WriteLongRunningJobAsync(
                        job: job,
                        cancellationToken: stoppingToken);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public List<QueueJob> GetBatchOfJobsAndMarkAsProcessing()
    {
        // Per-category capacity is passed into the SELECT as parameters (snapshot taken now).
        var capacitySnapshot = _channels.GetCapacitySnapshot();

        if (capacitySnapshot.NormalJobs <= 0 && capacitySnapshot.LongRunningJobs <= 0)
            return [];

        try
        {
            // Two-step to avoid holding the single SQLite write-lock during the O(N) selection.
            // Step 1 — SELECT the eligible (id, priority) pairs: scans/sorts pending jobs by the
            // materialized q_job_category / q_job_priority columns. It's a READ, so it doesn't block
            // the workers writing their completions (WAL: readers don't block writers).
            var now = _clock.UtcNow;

            // Background lane: an extremely-low-priority job is only released once the queue has been
            // free of any higher-priority work for the whole grace period, so it stays off the hot
            // path during bursts. The grace timer is stamped (below) from any dequeued batch that
            // carries higher-priority work — that's the signal foreground work is flowing. The
            // anti-starvation threshold lets a job that has waited too long run regardless.
            var allowExtremelyLow =
                _higherPriorityActivitySeenAt is null
                || now - _higherPriorityActivitySeenAt.Value >= _config.ExtremelyLowPriorityIdleGracePeriod;

            var selected = SelectEligibleJobs(allowExtremelyLow);

            if (selected.Count == 0)
                return [];

            var hasHigherPriorityWork = selected
                .Any(job => job.Priority < QueueJobPriority.ExtremelyLow);

            if (hasHigherPriorityWork)
                _higherPriorityActivitySeenAt = now;

            // Race: the grace window was open, but this very batch revealed that fresh foreground work
            // has just arrived. Drop it and re-select with the lane now closed, so we don't dispatch
            // extremely-low jobs alongside the new higher-priority work at the start of a burst.
            if (allowExtremelyLow && hasHigherPriorityWork)
            {
                selected = SelectEligibleJobs(allowExtremelyLow: false);

                if (selected.Count == 0)
                    return [];
            }

            var jobIds = selected
                .Select(job => job.Id)
                .ToList();

            // Step 2 — mark exactly those ids as Processing. The write-lock is held only for this
            // tiny UPDATE keyed by primary id (O(batchSize)), not for the heavy scan above.
            _markJobsProcessingCommand.Parameters.Clear();
            _markJobsProcessingCommand.WithParameter("$processingStatus", QueueStatus.Processing);
            _markJobsProcessingCommand.WithParameter("$pendingStatus", QueueStatus.Pending);
            _markJobsProcessingCommand.WithParameter("$now", _clock.UtcNow);
            _markJobsProcessingCommand.WithJsonParameter("$jobIds", jobIds);

            return _markJobsProcessingCommand.GetRows(reader => new QueueJob(
                Id: reader.GetInt32(0),
                CorrelationId: reader.GetGuid(1),
                JobType: reader.GetString(2),
                DefinitionJson: reader.GetString(3),
                EnqueuedAt: reader.GetFieldValue<DateTimeOffset>(4),
                ExecuteAfterDate: reader.GetFieldValue<DateTimeOffset>(5),
                FailedRetriesCount: reader.GetInt32(6),
                SoftRetriesLeft: reader.GetInt32OrNull(7),
                BatchId: reader.GetGuidOrNull(8)),
                name: "queue.mark_jobs_processing");

            List<(int Id, int Priority)> SelectEligibleJobs(bool allowExtremelyLow)
            {
                var normalJobs = SelectCategoryJobs(
                    category: QueueJobCategory.Normal,
                    capacity: capacitySnapshot.NormalJobs,
                    allowExtremelyLow: allowExtremelyLow);

                var longRunningJobs = SelectCategoryJobs(
                    category: QueueJobCategory.LongRunning,
                    capacity: capacitySnapshot.LongRunningJobs,
                    allowExtremelyLow: allowExtremelyLow);

                return normalJobs
                    .Concat(longRunningJobs)
                    .OrderBy(job => job.Id)
                    .Take(_config.QueueProcessingBatchSize)
                    .ToList();
            }

            List<(int Id, int Priority)> SelectCategoryJobs(
                QueueJobCategory category,
                int capacity,
                bool allowExtremelyLow)
            {
                var limit = Math.Min(capacity, _config.QueueProcessingBatchSize);

                if (limit <= 0)
                    return [];

                _selectJobsBatchCommand.Parameters.Clear();
                _selectJobsBatchCommand.WithParameter("$pendingStatus", QueueStatus.Pending);
                _selectJobsBatchCommand.WithParameter("$category", (int)category);
                _selectJobsBatchCommand.WithParameter("$now", now);
                _selectJobsBatchCommand.WithParameter("$limit", limit);
                _selectJobsBatchCommand.WithParameter("$extremelyLowPriority", QueueJobPriority.ExtremelyLow);
                _selectJobsBatchCommand.WithParameter("$allowExtremelyLow", allowExtremelyLow ? 1 : 0);
                _selectJobsBatchCommand.WithParameter("$antiStarvationThreshold", now - _config.ExtremelyLowPriorityMaxWait);

                return _selectJobsBatchCommand.GetRows(
                    reader => (Id: reader.GetInt32(0), Priority: reader.GetInt32(1)),
                    name: "queue.select_jobs_batch");
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Cannot get a batch of queue jobs to process");
            return [];
        }
    }

    // Step 1 (READ): pick the eligible job ids honoring per-category capacity + priority, reading the
    // materialized q_job_category / q_job_priority columns (capacity passed in as parameters). As a
    // SELECT it never holds the write lock — workers can write their completions concurrently (WAL).
    private SqliteCommand PrepareSelectJobsBatchCommand(
        SqliteConnection connection)
    {
        var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT q_id, q_job_priority
            FROM q_queue
            WHERE
                q_status = $pendingStatus
                AND q_job_category = $category
                AND q_execute_after_date <= $now
                AND (
                    q_job_priority < $extremelyLowPriority
                    OR $allowExtremelyLow = 1
                    OR q_execute_after_date <= $antiStarvationThreshold
                )
            ORDER BY q_job_priority
            LIMIT $limit
            """;

        return command;
    }

    // Step 2 (WRITE): mark exactly the chosen ids as Processing. Keyed by primary id via json_each,
    // so the write lock is held only for this O(batchSize) update — never for the scan above. The
    // extra q_status = $pendingStatus guard keeps it idempotent if a job changed since step 1.
    private SqliteCommand PrepareMarkJobsProcessingCommand(
        SqliteConnection connection)
    {
        var command = connection.CreateCommand();

        command.CommandText = @"
            UPDATE q_queue
            SET
                q_processing_started_at = $now,
                q_status = $processingStatus,
                q_debounce_id = NULL
            WHERE
                q_id IN (SELECT value FROM json_each($jobIds))
                AND q_status = $pendingStatus
            RETURNING
                q_id,
                q_correlation_id,
                q_job_type,
                q_definition,
                q_enqueued_at,
                q_execute_after_date,
                q_failed_retries_count,
                q_soft_retries_left,
                q_batch_id
            ";

        return command;
    }

    private SqliteCommand PrepareAnySagaExistsCommand(
        SqliteConnection connection)
    {
        var command = connection.CreateCommand();

        command.CommandText = "SELECT EXISTS (SELECT 1 FROM qs_queue_sagas)";

        return command;
    }

    private SqliteCommand PrepareDeleteCompletedSagasCommand(
        SqliteConnection connection)
    {
        var command = connection.CreateCommand();

        command.CommandText = """
        DELETE FROM qs_queue_sagas
        WHERE NOT EXISTS (
            SELECT 1
            FROM q_queue
            WHERE q_saga_id = qs_id
        )
        RETURNING
            qs_id,
            qs_on_completed_queue_job_type,
            qs_on_completed_queue_job_definition,
            qs_correlation_id                        
        """;

        return command;
    }
    
    private SqliteCommand PrepareInsertQueueSagaJobsCommand(
        SqliteConnection connection)
    {
        var command = connection.CreateCommand();

        command.CommandText = @"
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
                q_job_category,
                q_job_priority
            )
            SELECT
                json_extract(value, '$.jobType'),
                json_extract(value, '$.definition'),
                json_extract(value, '$.status'),
                0,
                $now,
                $now,
                json_extract(value, '$.correlationId'),
                NULL,
                NULL,
                json_extract(value, '$.jobCategory'),
                json_extract(value, '$.jobPriority')
            FROM
                json_each($definitions)
            RETURNING
                q_id;
        ";

        return command;
    }
}