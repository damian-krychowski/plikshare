using PlikShare.Core.Authorization;
using PlikShare.Core.Clock;
using PlikShare.Core.Queue;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Processing.Contracts;
using PlikShare.Workspaces.Validation;
using static PlikShare.Files.Processing.GetProcessingFileJobsQuery;

namespace PlikShare.Files.Processing;

public static class FileProcessingEndpoints
{
    public static void MapFileProcessingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceExternalId}/files/processing")
            .WithTags("FileProcessing")
            .RequireAuthorization(policyNames: AuthPolicy.Internal)
            .AddEndpointFilter<ValidateWorkspaceFilter>();

        group.MapGet("/events", GetFileProcessingEvents)
            .WithName("GetFileProcessingEvents");
    }

    /// <summary>
    /// Server-Sent Events stream of per-file processing state in a workspace. Stateful per
    /// connection: the first event carries every file currently being processed; each later event
    /// carries only the DIFF against what this connection has already reported — files whose queue
    /// jobs finished since the last event (<c>processingFinished</c>) and files that (re)entered
    /// processing (<c>processing</c>). Tracking is per queue JOB: per signal the connection asks
    /// only for jobs NEWER than its cursor (keyset on the AUTOINCREMENT q_id) plus the liveness of
    /// the jobs it already reported (PK membership check) — never the full workspace state again.
    /// A job vanishing from q_queue covers completion and cancellation alike, and a failed one is
    /// filtered out by status, so every way a job can end clears its files. No changes = no event.
    /// </summary>
    private static async Task GetFileProcessingEvents(
        HttpContext httpContext,
        QueueWorkspaceNotifier workspaceNotifier,
        GetProcessingFileJobsQuery getProcessingFileJobsQuery,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();
        var workspace = workspaceMembership.Workspace;

        var response = httpContext.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Append("X-Accel-Buffering", "no");

        // Subscribe BEFORE the snapshot so a change landing between the two isn't missed.
        using var subscription = workspaceNotifier.Subscribe(
            workspace.Id);

        var snapshot = getProcessingFileJobsQuery.GetSnapshot(
            workspace.Id);

        var reportedJobs = snapshot.JobsById;
        var lastSeenQueueJobId = 0;

        foreach (var queueJobId in reportedJobs.Keys)
        {
            if (queueJobId > lastSeenQueueJobId)
                lastSeenQueueJobId = queueJobId;
        }

        await WriteEvent(
            response,
            new FileProcessingEventDto
            {
                Processing = snapshot.FilesByJobType,
                ProcessingFinished = []
            },
            cancellationToken);

        var keepAlive = TimeSpan.FromSeconds(20);
        var minPushInterval = TimeSpan.FromSeconds(1);
        var lastPushAt = clock.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            subscription.DrainPending();

            bool signalled;

            using (var keepAliveCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken))
            {
                keepAliveCts.CancelAfter(keepAlive);

                try
                {
                    signalled = await subscription.WaitForSignalAsync(
                        keepAliveCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    await response.WriteAsync(": keep-alive\n\n", cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                    continue;
                }
            }

            if (!signalled)
                break;

            var elapsed = clock.UtcNow - lastPushAt;

            if (elapsed < minPushInterval)
            {
                try
                {
                    await Task.Delay(
                        minPushInterval - elapsed,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                subscription.DrainPending();
            }

            var newJobs = getProcessingFileJobsQuery.GetNewJobs(
                workspaceId: workspace.Id,
                afterQueueJobId: lastSeenQueueJobId);

            var aliveJobIds = getProcessingFileJobsQuery.GetAliveJobIds(
                reportedJobs.Keys);

            var diff = ApplyChanges(
                reportedJobs,
                aliveJobIds,
                newJobs,
                ref lastSeenQueueJobId);

            if (diff is null)
                continue;

            await WriteEvent(
                response,
                diff,
                cancellationToken);

            lastPushAt = clock.UtcNow;
        }
    }

    private static FileProcessingEventDto? ApplyChanges(
        Dictionary<int, ProcessingJob> reportedJobs,
        HashSet<int> aliveJobIds,
        Dictionary<int, ProcessingJob> newJobs,
        ref int lastSeenQueueJobId)
    {
        Dictionary<QueueJobType, HashSet<FileExtId>>? processing = null;
        Dictionary<QueueJobType, HashSet<FileExtId>>? finished = null;

        List<int>? finishedJobIds = null;

        foreach (var (queueJobId, job) in reportedJobs)
        {
            if (aliveJobIds.Contains(queueJobId))
                continue;

            (finishedJobIds ??= []).Add(queueJobId);

            foreach (var fileExternalId in job.FileExternalIds)
            {
                Add(
                    ref finished,
                    job.JobType,
                    fileExternalId);
            }
        }

        if (finishedJobIds is not null)
        {
            foreach (var queueJobId in finishedJobIds)
                reportedJobs.Remove(queueJobId);
        }

        foreach (var (queueJobId, job) in newJobs)
        {
            reportedJobs[queueJobId] = job;

            if (queueJobId > lastSeenQueueJobId)
                lastSeenQueueJobId = queueJobId;

            foreach (var fileExternalId in job.FileExternalIds)
            {
                Add(
                    ref processing,
                    job.JobType,
                    fileExternalId);
            }
        }

        if (finished is not null)
        {
            Dictionary<QueueJobType, HashSet<FileExtId>>? stillActivePairs = null;

            foreach (var (jobType, files) in finished)
            {
                foreach (var fileExternalId in files)
                {
                    stillActivePairs ??= BuildActivePairs(reportedJobs);

                    if (stillActivePairs.GetValueOrDefault(jobType)?.Contains(fileExternalId) == true)
                    {
                        Add(
                            ref processing,
                            jobType,
                            fileExternalId);
                    }
                }
            }
        }

        if (processing is null && finished is null)
            return null;

        return new FileProcessingEventDto
        {
            Processing = processing ?? [],
            ProcessingFinished = finished ?? []
        };
    }

    private static Dictionary<QueueJobType, HashSet<FileExtId>> BuildActivePairs(
        Dictionary<int, ProcessingJob> reportedJobs)
    {
        Dictionary<QueueJobType, HashSet<FileExtId>>? pairs = null;

        foreach (var job in reportedJobs.Values)
        {
            foreach (var fileExternalId in job.FileExternalIds)
            {
                Add(
                    ref pairs,
                    job.JobType,
                    fileExternalId);
            }
        }

        return pairs ?? [];
    }

    private static void Add(
        ref Dictionary<QueueJobType, HashSet<FileExtId>>? dictionary,
        QueueJobType jobType,
        FileExtId fileExternalId)
    {
        dictionary ??= [];

        if (!dictionary.TryGetValue(jobType, out var files))
        {
            files = [];
            dictionary[jobType] = files;
        }

        files.Add(fileExternalId);
    }

    private static async Task WriteEvent(
        HttpResponse response,
        FileProcessingEventDto eventDto,
        CancellationToken cancellationToken)
    {
        await response.WriteAsync(
            text: $"data: {Json.Serialize(eventDto)}\n\n",
            cancellationToken: cancellationToken);

        await response.Body.FlushAsync(
            cancellationToken);
    }
}
