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
    private DateTimeOffset? _blockedJobsCheckedAt = null;

    private readonly SqliteConnection _connection;

    private readonly SqliteCommand _getJobsBatchCommand;
    private readonly SqliteCommand _deleteCompletedSagasCommand;
    private readonly SqliteCommand _insertQueueSagaJobsCommand;

    private readonly IClock _clock;
    private readonly IQueue _queue;
    private readonly QueueChannels _channels;
    private readonly IConfig _config;
    private readonly QueueJobInfoProvider _queueJobInfoProvider;
    private readonly CancellationTokenSource _gracefulShutdownCts;
    private bool _disposed;
    private QueueChannels.CapacitySnapshot _capacitySnapshot;

    public QueueProducer(
        PlikShareDb plikShareDb,
        IClock clock,
        IQueue queue,
        QueueChannels channels,
        IConfig config,
        QueueJobInfoProvider queueJobInfoProvider)
    {
        _clock = clock;
        _queue = queue;
        _channels = channels;
        _config = config;
        _queueJobInfoProvider = queueJobInfoProvider;
        _gracefulShutdownCts = new CancellationTokenSource();

        _connection = plikShareDb.OpenConnection();
        RegisterCustomSqliteFunctions(_connection);

        _getJobsBatchCommand = PrepareGetJobsBatchCommand(_connection);
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

            using var tickTimer = new PeriodicTimer(TimeSpan.FromSeconds(1));

            while (await tickTimer.WaitForNextTickAsync(linkedCts.Token))
            {
                if (linkedCts.Token.IsCancellationRequested)
                    break;

                try
                {
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
            _getJobsBatchCommand.Dispose();
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
        var areAllJobsProcessed = false;

        do
        {
            if (stoppingToken.IsCancellationRequested)
                return;

            ProcessQueueSagas();
            var jobsBatchResult = GetBatchOfJobsAndMarkAsProcessing();

            if (jobsBatchResult.Count == 0)
            {
                areAllJobsProcessed = true;
            }
            else
            {
                await PushAllJobsThroughChannel(
                    jobsBatch: jobsBatchResult, 
                    stoppingToken: stoppingToken);
            }
        } while (!areAllJobsProcessed);
    }

    private void ProcessQueueSagas()
    {
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
                    CorrelationId =  correlationId,
                    Status = _queue.GetNewJobStatus(jobType).Value
                };
            });

            _insertQueueSagaJobsCommand.Parameters.Clear();
            _insertQueueSagaJobsCommand.WithParameter("$now", _clock.UtcNow);
            _insertQueueSagaJobsCommand.WithParameter("$definitions", Json.Serialize(sagas));
            _insertQueueSagaJobsCommand.Transaction = transaction;

            var insertedSagaJobs = _insertQueueSagaJobsCommand.GetRows(reader =>
                reader.GetInt32(0));

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
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while processing completed Queue Sagas.");
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
                case QueueJobCategory.DbOnly:
                    await _channels.WriteDbOnlyJobAsync(
                        job: job,
                        cancellationToken: stoppingToken);
                    break;

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
        //we are using capacity in custom sqlite function, we need to update it before the query
        _capacitySnapshot = _channels.GetCapacitySnapshot();

        try
        {
            SetGetJobsBatchParameters(_getJobsBatchCommand);

            return _getJobsBatchCommand.GetRows(reader => new QueueJob(
                Id: reader.GetInt32(0),
                CorrelationId: reader.GetGuid(1),
                JobType: reader.GetString(2),
                DefinitionJson: reader.GetString(3),
                EnqueuedAt: reader.GetFieldValue<DateTimeOffset>(4),
                ExecuteAfterDate: reader.GetFieldValue<DateTimeOffset>(5),
                FailedRetriesCount: reader.GetInt32(6)));
        }
        catch (Exception e)
        {
            Log.Error(e, "Cannot get a batch of queue jobs to process");
            return [];
        }
    }

    //todo that probably would work better if job_category would be stored inside table rather than being calculated on the flight
    private SqliteCommand PrepareGetJobsBatchCommand(
        SqliteConnection connection)
    {
        var command = connection.CreateCommand();

        command.CommandText = @"
            WITH ranked_jobs AS (
                SELECT 
                    q_id,
                    ROW_NUMBER() OVER (
                        PARTITION BY app_get_job_category(q_job_type) 
                        ORDER BY app_get_job_priority(q_job_type) ASC
                    ) as category_rank,
                    app_get_capacity_left(app_get_job_category(q_job_type)) as capacity_left
                FROM q_queue
                WHERE 
                    q_status = $pendingStatus 
                    AND q_execute_after_date <= $now
                    AND app_get_capacity_left(app_get_job_category(q_job_type)) > 0
            ),
            eligible_jobs AS (
                SELECT q_id, q_job_type
                FROM ranked_jobs
                WHERE category_rank <= capacity_left
            )
            UPDATE q_queue
            SET 
                q_processing_started_at = $now, 
                q_status = $processingStatus,
                q_debounce_id = NULL
            WHERE q_id IN (
                SELECT q_id
                FROM (
                    SELECT q_id, ROW_NUMBER() OVER (ORDER BY q_id) as overall_rank
                    FROM eligible_jobs
                ) ranked
                WHERE overall_rank <= $batchSize
            )
            RETURNING
                q_id,
                q_correlation_id,
                q_job_type,
                q_definition,
                q_enqueued_at,
                q_execute_after_date,
                q_failed_retries_count
            ";

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
                q_saga_id
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
                NULL
            FROM
                json_each($definitions)
            RETURNING
                q_id;
        ";

        return command;
    }

    private void SetGetJobsBatchParameters(
        SqliteCommand command)
    {
        command.Parameters.Clear();
        command.WithParameter("$pendingStatus", QueueStatus.Pending);
        command.WithParameter("$processingStatus", QueueStatus.Processing);
        command.WithParameter("$now", _clock.UtcNow);
        command.WithParameter("$batchSize", _config.QueueProcessingBatchSize);
    }

    public void RegisterCustomSqliteFunctions(SqliteConnection connection)
    {
        connection.CreateFunction(
            "app_get_job_category",
            (string? jobType) =>
            {
                if (string.IsNullOrWhiteSpace(jobType))
                    return -1;

                return (int) _queueJobInfoProvider.GetJobCategory(
                    jobType);
            });

        connection.CreateFunction(
            "app_get_job_priority",
            (string? jobType) =>
            {
                if (string.IsNullOrWhiteSpace(jobType))
                    return -1;

                return (int)_queueJobInfoProvider.GetJobPriority(
                    jobType);
            });

        connection.CreateFunction(
            "app_get_capacity_left",
            (int? jobCategory) =>
            {
                return jobCategory switch
                {
                    (int) QueueJobCategory.DbOnly => _capacitySnapshot.DbOnlyJobs,
                    (int) QueueJobCategory.Normal => _capacitySnapshot.NormalJobs,
                    (int) QueueJobCategory.LongRunning => _capacitySnapshot.LongRunningJobs,
                    _ => 0
                };
            });
    }
}