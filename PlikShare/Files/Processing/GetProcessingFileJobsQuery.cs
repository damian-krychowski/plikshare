using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;

namespace PlikShare.Files.Processing;

public class GetProcessingFileJobsQuery(PlikShareDb plikShareDb)
{
    public sealed class ProcessingFileJobs
    {
        public required Dictionary<int, ProcessingJob> JobsById { get; init; }
        public required Dictionary<QueueJobType, HashSet<FileExtId>> FilesByJobType { get; init; }
    }

    public sealed record ProcessingJob(
        QueueJobType JobType,
        List<FileExtId> FileExternalIds);

    public ProcessingFileJobs GetSnapshot(
        int workspaceId)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .AggregateRows(
                sql: """
                     SELECT
                         q_id,
                         fi_external_id,
                         q_job_type
                     FROM q_queue
                     INNER JOIN qfj_queue_file_jobs ON qfj_queue_job_id = q_id
                     INNER JOIN fi_files ON fi_id = qfj_file_id
                     WHERE
                         q_workspace_id = $workspaceId
                         AND q_status != $failedStatus
                     """,
                seed: new ProcessingFileJobs
                {
                    JobsById = [],
                    FilesByJobType = []
                },
                aggregateRowFunc: (acc, reader) =>
                {
                    var queueJobId = reader.GetInt32(0);
                    var fileExternalId = reader.GetExtId<FileExtId>(1);
                    var jobType = new QueueJobType(reader.GetString(2));

                    if (!acc.JobsById.TryGetValue(queueJobId, out var job))
                    {
                        job = new ProcessingJob(
                            JobType: jobType,
                            FileExternalIds: []);

                        acc.JobsById[queueJobId] = job;
                    }

                    job.FileExternalIds.Add(fileExternalId);

                    if (!acc.FilesByJobType.TryGetValue(jobType, out var files))
                    {
                        files = [];
                        acc.FilesByJobType[jobType] = files;
                    }

                    files.Add(fileExternalId);

                    return acc;
                })
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$failedStatus", QueueStatus.Failed)
            .Execute();
    }

    public Dictionary<int, ProcessingJob> GetNewJobs(
        int workspaceId,
        int afterQueueJobId)
    {
        using var connection = plikShareDb.OpenConnection();

        return connection
            .AggregateRows(
                sql: """
                     SELECT
                         q_id,
                         fi_external_id,
                         q_job_type
                     FROM q_queue
                     INNER JOIN qfj_queue_file_jobs ON qfj_queue_job_id = q_id
                     INNER JOIN fi_files ON fi_id = qfj_file_id
                     WHERE
                         q_workspace_id = $workspaceId
                         AND q_status != $failedStatus
                         AND q_id > $afterQueueJobId
                     """,
                seed: new Dictionary<int, ProcessingJob>(),
                aggregateRowFunc: (acc, reader) =>
                {
                    var queueJobId = reader.GetInt32(0);
                    var fileExternalId = reader.GetExtId<FileExtId>(1);
                    var jobType = new QueueJobType(reader.GetString(2));

                    if (!acc.TryGetValue(queueJobId, out var job))
                    {
                        job = new ProcessingJob(
                            JobType: jobType,
                            FileExternalIds: []);

                        acc[queueJobId] = job;
                    }

                    job.FileExternalIds.Add(fileExternalId);

                    return acc;
                })
            .WithParameter("$workspaceId", workspaceId)
            .WithParameter("$failedStatus", QueueStatus.Failed)
            .WithParameter("$afterQueueJobId", afterQueueJobId)
            .Execute();
    }

    public HashSet<int> GetAliveJobIds(
        IReadOnlyCollection<int> queueJobIds)
    {
        if (queueJobIds.Count == 0)
            return [];

        using var connection = plikShareDb.OpenConnection();

        return connection
            .AggregateRows(
                sql: """
                     SELECT q_id
                     FROM q_queue
                     WHERE
                         q_id IN (SELECT value FROM json_each($queueJobIds))
                         AND q_status != $failedStatus
                     """,
                seed: new HashSet<int>(),
                aggregateRowFunc: (acc, reader) =>
                {
                    acc.Add(reader.GetInt32(0));
                    return acc;
                })
            .WithJsonParameter("$queueJobIds", queueJobIds)
            .WithParameter("$failedStatus", QueueStatus.Failed)
            .Execute();
    }
}
