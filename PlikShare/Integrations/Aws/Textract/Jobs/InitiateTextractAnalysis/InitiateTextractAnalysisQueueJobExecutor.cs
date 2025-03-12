using Amazon.Textract;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Integrations.Aws.Textract.Jobs.CheckTextractAnalysisStatus;
using PlikShare.Storages;
using Serilog;

namespace PlikShare.Integrations.Aws.Textract.Jobs.InitiateTextractAnalysis;

//todo handle situation when textract client is not defined (should behave same as when email provider is not defined - be blocked)
public class InitiateTextractAnalysisQueueJobExecutor(
    PlikShareDb plikShareDb,
    DbWriteQueue dbWriteQueue,
    TextractClientStore textractClientStore,
    IQueue queue,
    IClock clock) : IQueueNormalJobExecutor
{
    public const int CheckStatusDelayInSeconds = 5;

    public string JobType => InitiateTextractAnalysisQueueJobType.Value;
    public int Priority => QueueJobPriority.Normal;

    public async Task<QueueJobResult> Execute(
        string definitionJson, 
        Guid correlationId, 
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<InitiateTextractAnalysisQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(InitiateTextractAnalysisQueueJobDefinition)}'");
        }

        var textractJob = TryGetTextractJob(
            textractJobId: definition.TextractJobId);

        if (textractJob is null)
        {
            Log.Warning("Cannot initiate Textract analysis because TextractJob#{TextractJobId} was not found. Textract analysis skipped.",
                definition.TextractJobId);

            return QueueJobResult.Success;
        }
        
        var textractClient = textractClientStore.TryGetClient(
            textractJob.TextractIntegrationId);

        if (textractClient is null)
        {
            Log.Warning("Cannot initiate Textract analysis for TextractJob#{TextractJobId} because TextractClient#{TextractIntegrationId} was not found. Textract analysis skipped.",
                textractJob.Id,
                textractJob.TextractIntegrationId);

            return QueueJobResult.Success;
        }

        try
        {
            var startDocumentAnalysisResponse = await textractClient.InitiateAnalysis(
                fileWorkspaceId: textractJob.TextractWorkspaceId,
                fileKey: new S3FileKey
                {
                    FileExternalId = textractJob.FileExternalId,
                    S3KeySecretPart = textractJob.S3KeySecretPart
                },
                features: textractJob.Definition.Features,
                cancellationToken: cancellationToken);

            //todo handle errors

            var updateResult = await MarkTextractJobAsProcessingAndSchedulePolling(
                textractJobId: textractJob.Id,
                textractAnalysisJobId: startDocumentAnalysisResponse.JobId,
                correlationId: correlationId,
                cancellationToken: cancellationToken);

            if (updateResult == UpdateResultCode.TextractJobNotFound)
            {
                Log.Warning(
                    "Textract analysis for TextractJob#{TextractJobId} initiation was successful however TextractJob was deleted in the meantime. Textract analysis skipped.",
                    textractJob.Id);
            }
            else
            {
                Log.Information("Textract analysis for TextractJob#{TextractJobId} was initiated successfully.",
                    textractJob.Id);
            }

            return QueueJobResult.Success;
        }
        catch (AmazonTextractException e)
        {
            Log.Error(e, "Textract analysis for TextractJob#{TextractJobId} has failed on AWS Textract level.",
                textractJob.Id);

            throw;
        }
        catch (Exception e)
        {
            Log.Error(e, "Textract analysis for TextractJob#{TextractJobId} has failed.",
                textractJob.Id);

            throw;
        }
    }
    
    private TextractJob? TryGetTextractJob(int textractJobId)
    {
        using var connection = plikShareDb.OpenConnection();

        var textractJob = connection
            .OneRowCmd(
                sql: """
                     SELECT
                         itj_textract_workspace_id,
                         itj_textract_integration_id,
                         itj_definition,
                         fi_external_id,
                         fi_s3_key_secret_part
                     FROM itj_integrations_textract_jobs
                     INNER JOIN fi_files
                         ON fi_id = itj_textract_file_id
                     WHERE
                         itj_id = $itjId
                         AND itj_status = $pendingStatus
                     """,
                readRowFunc: reader => new TextractJob
                {
                    Id = textractJobId,
                    TextractWorkspaceId = reader.GetInt32(0),
                    TextractIntegrationId = reader.GetInt32(1),
                    Definition = reader.GetFromJson<TextractJobDefinitionEntity>(2),
                    FileExternalId = reader.GetExtId<FileExtId>(3),
                    S3KeySecretPart = reader.GetString(4)
                })
            .WithParameter("$itjId", textractJobId)
            .WithEnumParameter("$pendingStatus", TextractJobStatus.Pending)
            .Execute();

        return textractJob.IsEmpty
            ? null
            : textractJob.Value;
    }

    private Task<UpdateResultCode> MarkTextractJobAsProcessingAndSchedulePolling(
        int textractJobId,
        string textractAnalysisJobId,
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
                            sql: """
                                 UPDATE itj_integrations_textract_jobs
                                 SET itj_textract_analysis_job_id = $textractAnalysisJobId,
                                     itj_status = $processingStatus
                                 WHERE itj_id = $itjId
                                 RETURNING itj_id
                                 """,
                            readRowFunc: reader => reader.GetInt32(0),
                            transaction: transaction)
                        .WithParameter("$textractAnalysisJobId", textractAnalysisJobId)
                        .WithEnumParameter("$processingStatus", TextractJobStatus.Processing)
                        .WithParameter("$itjId", textractJobId)
                        .Execute();

                    if (result.IsEmpty)
                    {
                        transaction.Rollback();
                        return UpdateResultCode.TextractJobNotFound;
                    }

                    queue.EnqueueOrThrow(
                        correlationId: correlationId,
                        jobType: CheckTextractAnalysisStatusQueueJobType.Value,
                        definition: new CheckTextractAnalysisStatusQueueJobDefinition
                        {
                            TextractJobId = textractJobId
                        },
                        executeAfterDate: clock.UtcNow.Add(
                            TimeSpan.FromSeconds(CheckStatusDelayInSeconds)),
                        debounceId: null,
                        sagaId: null,
                        dbWriteContext: context,
                        transaction: transaction);

                    transaction.Commit();
                    return UpdateResultCode.Ok;
                }
                catch (Exception e)
                {
                    transaction.Rollback();

                    Log.Error(e, "Something went wrong while marking TextractJob#{TextractJobId} as processing and scheduling result polling",
                        textractJobId);

                    throw;
                }
            },
            cancellationToken: cancellationToken);
    }

    private class TextractJob
    {
        public required int Id { get; init; }
        public required int TextractWorkspaceId { get; init; }
        public required int TextractIntegrationId { get; init; }
        public required TextractJobDefinitionEntity Definition { get; init; }
        public required FileExtId FileExternalId { get; init; }
        public required string S3KeySecretPart { get; init; }
    }

    private enum UpdateResultCode
    {
        Ok = 0,
        TextractJobNotFound
    }
}