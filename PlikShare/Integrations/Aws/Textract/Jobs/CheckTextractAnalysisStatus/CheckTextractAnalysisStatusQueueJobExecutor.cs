using Amazon.Textract;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Integrations.Aws.Textract.Jobs.DownloadTextractAnalysis;
using Serilog;

namespace PlikShare.Integrations.Aws.Textract.Jobs.CheckTextractAnalysisStatus;

public class CheckTextractAnalysisStatusQueueJobExecutor(
    PlikShareDb plikShareDb,
    DbWriteQueue dbWriteQueue,
    TextractClientStore textractClientStore,
    IQueue queue,
    IClock clock,
    TextractResultTemporaryStore textractResultTemporaryStore) : IQueueLongRunningJobExecutor
{
    public string JobType => CheckTextractAnalysisStatusQueueJobType.Value;
    public int Priority => QueueJobPriority.Normal;

    public async Task<QueueJobResult> Execute(
        string definitionJson, 
        Guid correlationId, 
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<CheckTextractAnalysisStatusQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(CheckTextractAnalysisStatusQueueJobDefinition)}'");
        }

        var textractJob = TryGetTextractJob(
            textractJobId: definition.TextractJobId);

        if (textractJob is null)
        {
            Log.Warning("Cannot check Textract analysis status because TextractJob#{TextractJobId} was not found. Status check is skipped.",
                definition.TextractJobId);

            return QueueJobResult.Success;
        }

        var textractClient = textractClientStore.TryGetClient(
            textractJob.TextractIntegrationId);

        if (textractClient is null)
        {
            Log.Warning("Could not poll for TextractJob#{TextractJobId} because TextractClient#{TextractIntegrationId} was not found. Status check is skipped",
                textractJob.Id,
                textractJob.TextractIntegrationId);

            return QueueJobResult.Success;
        }

        try
        {
            var analysisResult = await textractClient.GetAnalysisResult(
                analysisJobId: textractJob.AnalysisJobId,
                nextToken: null,
                cancellationToken: cancellationToken);

            var analysisStatus = analysisResult.JobStatus;

            if (analysisStatus == JobStatus.SUCCEEDED ||
                analysisStatus == JobStatus.PARTIAL_SUCCESS)
            {
                var id = textractResultTemporaryStore.Store(
                    analysisResult);

                await MarkTextractJobAsDownloadingResultsAndScheduleResultsDownload(
                    textractJobId: definition.TextractJobId,
                    textractTemporaryStoreId: id.Value,
                    correlationId: correlationId,
                    cancellationToken: cancellationToken);

                Log.Information("TextractJob#{TextractJobId} status in AWS is {TextractStatus}.",
                    textractJob.Id,
                    analysisResult);

                return QueueJobResult.Success;
            }

            if (analysisStatus == JobStatus.IN_PROGRESS)
            {
                Log.Information("TextractJob#{TextractJobId} is still in progress.",
                    textractJob.Id);

                return QueueJobResult.NeedsRetry(
                    delay: TimeSpan.FromSeconds(5));
            }

            if (analysisStatus == JobStatus.FAILED)
            {
                await MarkTextractJobAsFailed(
                    textractJobId: definition.TextractJobId,
                    cancellationToken: cancellationToken);

                Log.Warning("TextractJob#{TextractJobId} status in AWS is Failed.",
                    textractJob.Id);

                return QueueJobResult.Success;
            }

            throw new InvalidOperationException(
                $"TextractJob#{definition.TextractJobId} has unknown status '{analysisStatus.Value}'");
        }
        catch (AmazonTextractException e)
        {
            Log.Error(e, "Textract analysis status check for TextractJob#{TextractJobId} has failed on AWS Textract level.",
                textractJob.Id);

            throw;
        }
        catch (Exception e)
        {
            Log.Error(e, "Textract analysis status check for TextractJob#{TextractJobId} has failed.",
                textractJob.Id);

            throw;
        }
    }

    private TextractJob? TryGetTextractJob(
        int textractJobId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: @"
                        SELECT 
                            itj_id,
                            itj_textract_analysis_job_id,
                            itj_textract_integration_id
                        FROM itj_integrations_textract_jobs
                        WHERE 
                            itj_id = $textractJobId
                            AND itj_status = $processingStatus
                        ORDER BY itj_id ASC                            
                    ",
                readRowFunc: reader => new TextractJob
                {
                    Id = reader.GetInt32(0),
                    AnalysisJobId = reader.GetString(1),
                    TextractIntegrationId = reader.GetInt32(2)
                })
            .WithEnumParameter("$processingStatus", TextractJobStatus.Processing)
            .WithParameter("$textractJobId", textractJobId)
            .Execute();

        return result.IsEmpty
            ? null
            : result.Value;
    }

    private Task<UpdateResultCode> MarkTextractJobAsFailed(
        int textractJobId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                var result = context
                    .OneRowCmd(
                        sql: @" 
                            UPDATE itj_integrations_textract_jobs
                            SET itj_status = $failedStatus
                            WHERE itj_id = $textractJobId
                            RETURNING itj_id
                        ",
                        readRowFunc: reader => reader.GetInt32(0))
                    .WithEnumParameter("$failedStatus", TextractJobStatus.Failed)
                    .WithParameter("$textractJobId", textractJobId)
                    .Execute();

                return result.IsEmpty
                    ? UpdateResultCode.TextractJobNotFound
                    : UpdateResultCode.Ok;
            },
            cancellationToken: cancellationToken);
    }

    private Task<UpdateResultCode> MarkTextractJobAsDownloadingResultsAndScheduleResultsDownload(
        int textractJobId,
        Guid textractTemporaryStoreId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context =>
            {
                using var transaction = context.Connection.BeginTransaction();

                try
                {
                    var result = context
                        .OneRowCmd(
                            sql: @" 
                                UPDATE itj_integrations_textract_jobs
                                SET itj_status = $downloadingResults
                                WHERE itj_id = $textractJobId
                                RETURNING itj_id
                            ",
                            readRowFunc: reader => reader.GetInt32(0),
                            transaction: transaction)
                        .WithEnumParameter("$downloadingResults", TextractJobStatus.DownloadingResults)
                        .WithParameter("$textractJobId", textractJobId)
                        .Execute();

                    if (result.IsEmpty)
                        return UpdateResultCode.TextractJobNotFound;

                    queue.EnqueueOrThrow(
                        correlationId: correlationId,
                        jobType: DownloadTextractAnalysisQueueJobType.Value,
                        definition: new DownloadTextractAnalysisQueueJobDefinition
                        {
                            TextractJobId = textractJobId,
                            TextractTemporaryStoreId = textractTemporaryStoreId
                        },
                        executeAfterDate: clock.UtcNow,
                        debounceId: null,
                        sagaId: null,
                        dbWriteContext: context,
                        transaction: transaction);

                    transaction.Commit();

                    return result.IsEmpty
                        ? UpdateResultCode.TextractJobNotFound
                        : UpdateResultCode.Ok;
                }
                catch (Exception e)
                {
                    transaction.Rollback();

                    Log.Error(e, "Something went wrong while marking TextractJob#{TextractJobId} as {Statuss}",
                        textractJobId,
                        TextractJobStatus.DownloadingResults);

                    throw;
                }
            },
            cancellationToken: cancellationToken);
    }

    private class TextractJob
    {
        public required int Id { get; init; }
        public required string AnalysisJobId { get; init; }
        public required int TextractIntegrationId { get; init; }
    }

    private enum UpdateResultCode
    {
        Ok = 0,
        TextractJobNotFound
    }
}