using Microsoft.Data.Sqlite;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using PlikShare.Integrations.Aws.Textract.Jobs.InitiateTextractAnalysis;
using PlikShare.Storages.FileCopying.OnCompletedActionHandler;
using Serilog;

namespace PlikShare.Integrations.Aws.Textract.Jobs.UpdateJobTextractFileAndStatus
{
    public class UpdateTextractJobFileAndStatusOnCompletedFileCopyHandler(
        IQueue queue,
        IClock clock) : ICopyFileQueueCompletedActionHandler
    {
        public const string Type = "update-textract-job-file-and-status";

        public string HandlerType => Type;

        public void OnCopyFileCompleted(
            DbWriteQueue.Context dbWriteContext,
            string actionHandlerDefinition,
            int sourceFileId,
            int sourceWorkspaceId,
            int newFileId,
            int destinationWorkspaceId,
            Guid correlationId,
            SqliteTransaction transaction)
        {
            Log.Information(
                "Processing completed file copy for Textract job. CorrelationId: {CorrelationId}, Source: File {SourceFileId} (Workspace {SourceWorkspaceId}), " +
                "Destination: File {NewFileId} (Workspace {DestinationWorkspaceId})",
                correlationId,
                sourceFileId,
                sourceWorkspaceId,
                newFileId,
                destinationWorkspaceId);

            var definition = Json.Deserialize<Definition>(
                actionHandlerDefinition);

            if (definition is null)
            {
                var error = $"Job '{actionHandlerDefinition}' cannot be parsed to correct '{nameof(Definition)}'";
                Log.Error(
                    "Failed to deserialize action handler definition. CorrelationId: {CorrelationId}, Definition: {Definition}",
                    correlationId,
                    actionHandlerDefinition);
                throw new ArgumentException(error);
            }

            var itjId = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        UPDATE itj_integrations_textract_jobs
                        SET itj_textract_file_id = $textractFileId,
                            itj_status = $pendingStatus
                        WHERE itj_id = $textractJobId
                        RETURNING itj_id
                    ",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$textractFileId", newFileId)
                .WithEnumParameter("$pendingStatus", TextractJobStatus.Pending)
                .WithParameter("$textractJobId", definition.TextractJobId)
                .Execute();

            if (itjId.IsEmpty)
            {
                Log.Warning(
                    "Could not update Textract job {TextractJobId} - job not found. File copy completed: {NewFileId}, CorrelationId: {CorrelationId}",
                    definition.TextractJobId,
                    newFileId,
                    correlationId);

                return;
            }

            Log.Information(
                "Successfully updated Textract job {TextractJobId} with new file {NewFileId} and set status to Pending. CorrelationId: {CorrelationId}",
                definition.TextractJobId,
                newFileId,
                correlationId);

            var queueJob = queue.EnqueueOrThrow(
                correlationId: correlationId,
                jobType: InitiateTextractAnalysisQueueJobType.Value,
                definition: new InitiateTextractAnalysisQueueJobDefinition
                {
                    TextractJobId = definition.TextractJobId
                },
                executeAfterDate: clock.UtcNow,
                debounceId: null,
                sagaId: null,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            Log.Information(
                "Enqueued Textract analysis job for TextractJob {TextractJobId}. Queue job: {QueueJobId}, CorrelationId: {CorrelationId}",
                definition.TextractJobId,
                queueJob.Value,
                correlationId);
        }

        public class Definition
        {
            public required int TextractJobId { get; init; }
        }
    }
}