using System.Diagnostics;
using Serilog;

namespace PlikShare.Core.Queue;

public class NormalQueueConsumer : BackgroundService
{
    private readonly IQueue _queue;
    private readonly QueueChannels _channels;
    private readonly IEnumerable<IQueueNormalJobExecutor> _executors;
    private readonly int _consumerId;

    public NormalQueueConsumer(
        IQueue queue,
        QueueChannels channels,
        IEnumerable<IQueueNormalJobExecutor> executors,
        int consumerId)
    {
        _queue = queue;
        _channels = channels;
        _executors = executors;
        _consumerId = consumerId;

        var duplicatedExecutors = _executors
            .GroupBy(e => e.JobType)
            .Where(g => g.Count() > 1)
            .ToList();

        if (duplicatedExecutors.Any())
        {
            throw new InvalidOperationException(
                $"There are duplicated QueueJobExecutors for following JobTypes: {string.Join(", ", duplicatedExecutors.Select(g => g.Key))}");
        }
    }

    private string Identity => $"Normal Consumer: {_consumerId}";
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var stopWatch = new Stopwatch();

        Log.Information("Queue Consumer listening started. (Normal Consumer {ConsumerId})", _consumerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _channels.ReadNormalJobAsync(stoppingToken);

                Log.Debug("Queue message was obtained to processing (Normal Consumer {ConsumerId})", _consumerId);

                stopWatch.Restart();
                await ProcessJob(job, stopWatch, stoppingToken);
            }
            catch (OperationCanceledException ex)
            {
                Log.Warning(ex, "Normal Queue Consumer listening for queue jobs cancelled. (Normal Consumer {ConsumerId})", _consumerId);
                return;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing queue job (Normal Consumer {ConsumerId})", _consumerId);
            }
        }
    }

    private async Task ProcessJob(
        QueueJob job, 
        Stopwatch stopwatch, 
        CancellationToken cancellationToken)
    {
        try
        {
            if (TryGetJobExecutor(job, out var jobExecutor))
            {
                await ExecuteJob(
                    job, 
                    jobExecutor, 
                    stopwatch,
                    cancellationToken);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Cannot find QueueJobExecutor for job type: {job.JobType} (Normal Consumer {_consumerId})");
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

    private async Task ExecuteJob(
        QueueJob job,
        IQueueNormalJobExecutor jobExecutor,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var result = await jobExecutor.Execute(
            definitionJson: job.DefinitionJson,
            correlationId: job.CorrelationId,
            cancellationToken: cancellationToken);

        try
        {
            await _queue.HandleJobSuccess(
                job: job,
                result: result,
                consumerIdentity: Identity,
                cancellationToken: cancellationToken);
            
            if (result.Code == QueueJobResultCode.Success)
            {
                Log.Information("Job '{JobIdentity}' was completed (Duration: {JobDuration}ms, Normal Consumer: {ConsumerId}).",
                    job.Identity,
                    stopwatch.ElapsedMilliseconds,
                    _consumerId);
            }
            else if (result.Code == QueueJobResultCode.Blocked)
            {
                Log.Warning("Job '{JobIdentity}' could not be executed, new status: '{NewStatus}'. (Duration: {JobDuration}ms, Normal Consumer: {ConsumerId})",
                    job.Identity,
                    result,
                    stopwatch.ElapsedMilliseconds,
                    _consumerId);
            }
            else if (result.Code == QueueJobResultCode.NeedsRetry)
            {
                Log.Warning("Job '{JobIdentity}' needs retry with delay {RetryDelay}. (Duration: {JobDuration}ms, Running Consumer: {ConsumerId})",
                    job.Identity,
                    result.RetryDelay,
                    stopwatch.ElapsedMilliseconds,
                    _consumerId);
            }
        }
        catch (Exception e)
        {

            Log.Error(e, "Cannot mark job '{JobIdentity}' as Completed (Duration: {JobDuration}ms, Normal Consumer: {ConsumerId})",
                job.Identity,
                stopwatch.ElapsedMilliseconds,
                _consumerId);

            throw;
        }
    }
    
    private bool TryGetJobExecutor(in QueueJob job, out IQueueNormalJobExecutor executor)
    {
        var jobType = job.JobType;
        
        
        var jobExecutor = _executors.FirstOrDefault(
            ex => ex.JobType == jobType);

        executor = jobExecutor!; 

        return jobExecutor != null;
    }
}