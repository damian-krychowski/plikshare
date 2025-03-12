using System.Diagnostics;
using PlikShare.Core.Database.MainDatabase;
using Serilog;

namespace PlikShare.Core.Queue;

public class DbOnlyQueueConsumer : BackgroundService
{
    private const string Identity = "DbOnly Queue Consumer";

    private readonly DbWriteQueue _dbWriteQueue;
    private readonly QueueChannels _channels;
    private readonly IEnumerable<IQueueDbOnlyJobExecutor> _dbOnlyExecutors;
    private readonly IQueue _queue;

    public DbOnlyQueueConsumer(
        IQueue queue,
        DbWriteQueue dbWriteQueue,
        QueueChannels channels,
        IEnumerable<IQueueDbOnlyJobExecutor> dbOnlyExecutors)
    {
        _dbWriteQueue = dbWriteQueue;
        _channels = channels;
        _dbOnlyExecutors = dbOnlyExecutors;
        _queue = queue;

        var duplicatedExecutors = _dbOnlyExecutors
            .GroupBy(e => e.JobType)
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicatedExecutors.Any())
        {
            throw new InvalidOperationException(
                $"There are duplicated DbOnlyQueueJobExecutors for following JobTypes: {string.Join(", ", duplicatedExecutors.Select(g => g.Key))}");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var stopWatch = new Stopwatch();

        Log.Information("DbOnly Queue Consumer listening started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _channels.ReadDbOnlyJobAsync(stoppingToken);

                Log.Debug("Queue message was obtained to processing by DbOnly Queue Consumer.");

                stopWatch.Restart();
                await ProcessJob(job, stopWatch, stoppingToken);
            }
            catch (OperationCanceledException ex)
            {
                Log.Warning(ex, "DbOnly Queue Consumer listening for queue jobs cancelled.");
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing queue job by DbOnly Queue Consumer");
            }
        }
    }

    private async ValueTask ProcessJob(
        QueueJob job, 
        Stopwatch stopwatch, 
        CancellationToken cancellationToken)
    {
        try
        {
            if (TryGetDbOnlyJobExecutor(job, out var dbOnlyJobExecutor))
            {
                await ExecuteDbOnlyJob(
                    job, 
                    dbOnlyJobExecutor, 
                    stopwatch,
                    cancellationToken);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Cannot find DbOnlyQueueJobExecutor for job type: {job.JobType}");
            }
        }
        catch (Exception exception)
        {
            await _queue.HandleJobFailure(
                job: job,
                exception: exception,
                consumerIdentity: Identity,
                cancellationToken: cancellationToken);
        }
    }

    private async Task ExecuteDbOnlyJob(
        QueueJob job, 
        IQueueDbOnlyJobExecutor dbOnlyJobExecutor,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var sideEffectsToRun = await _dbWriteQueue.Execute(
            operationToEnqueue: dbWriteContext =>
            {
                using var transaction = dbWriteContext.Connection.BeginTransaction();

                try
                {
                    var (result, sideEffectsToRun) = dbOnlyJobExecutor.Execute(
                        job.DefinitionJson,
                        job.CorrelationId,
                        dbWriteContext,
                        transaction);

                    _queue.HandleJobSuccess(
                        job: job,
                        result: result,
                        dbWriteContext: dbWriteContext,
                        transaction: transaction,
                        consumerIdentity: Identity);

                    transaction.Commit();

                    if (result.Code == QueueJobResultCode.Success)
                    {
                        Log.Information("Job '{JobIdentity}' was completed (Duration: {JobDuration}ms, DbOnly Queue Consumer).",
                            job.Identity,
                            stopwatch.ElapsedMilliseconds);
                    }
                    else if (result.Code == QueueJobResultCode.Blocked)
                    {
                        Log.Warning("Job '{JobIdentity}' could not be executed, new status: '{NewStatus}'. (Duration: {JobDuration}ms, DbOnly Queue Consumer)",
                            job.Identity,
                            result.Code,
                            stopwatch.ElapsedMilliseconds);
                    }
                    else if (result.Code == QueueJobResultCode.NeedsRetry)
                    {
                        Log.Warning("Job '{JobIdentity}' needs retry with delay {RetryDelay}. (Duration: {JobDuration}ms, DbOnly Queue Consumer)",
                            job.Identity,
                            result.RetryDelay,
                            stopwatch.ElapsedMilliseconds);
                    }

                    return sideEffectsToRun;
                }
                catch (Exception e)
                {
                    transaction.Rollback();

                    Log.Error(e, "Something went wrong while executing '{JobIdentity}' (Duration: {JobDuration}ms, DbOnly Queue Consumer)", 
                        job.Identity, 
                        stopwatch.ElapsedMilliseconds);

                    throw;
                }
            },
            cancellationToken: cancellationToken);

        await sideEffectsToRun(cancellationToken);
    }
    
    private bool TryGetDbOnlyJobExecutor(in QueueJob job, out IQueueDbOnlyJobExecutor executor)
    {
        var jobType = job.JobType;
        
        var dbOnlyExecutor = _dbOnlyExecutors.FirstOrDefault(
            ex => ex.JobType == jobType);

        executor = dbOnlyExecutor!;

        return dbOnlyExecutor != null;
    }
}