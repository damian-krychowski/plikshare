using PlikShare.ArtificialIntelligence.DeleteConversation.QueueJob;
using PlikShare.ArtificialIntelligence.GetFileArtifact;
using PlikShare.ArtificialIntelligence.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.ArtificialIntelligence.DeleteConversation;

public class DeleteAiConversationOperation(
    GetFileArtifactWithAiConversationQuery getFileArtifactWithAiConversationQuery,
    DbWriteQueue dbWriteQueue,
    IQueue queue,
    IClock clock)
{
    public async Task<ResultCode> Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        FileArtifactExtId fileArtifactExternalId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var fileArtifact = getFileArtifactWithAiConversationQuery.Execute(
            workspace, 
            fileExternalId, 
            fileArtifactExternalId);

        if (fileArtifact is null)
        {
            Log.Warning("Could not rename AiConversation for FileArtifact '{FileArtifactExternalId}' because it was not found.",
                fileArtifactExternalId);

            return ResultCode.AiConversationNotFound;
        }

        await DeleteAiConversation(
            fileArtifactExternalId: fileArtifactExternalId,
            aiConversationExternalId: fileArtifact.AiConversationEntity.AiConversationExternalId,
            correlationId: correlationId,
            cancellationToken: cancellationToken);

        return ResultCode.Ok;
    }

    private Task DeleteAiConversation(
        FileArtifactExtId fileArtifactExternalId,
        AiConversationExtId aiConversationExternalId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        return dbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteDeleteAiConversation(
                dbWriteContext: context,
                fileArtifactExternalId: fileArtifactExternalId,
                aiConversationExternalId: aiConversationExternalId,
                correlationId: correlationId),
            cancellationToken: cancellationToken);
    }

    private void ExecuteDeleteAiConversation(
        DbWriteQueue.Context dbWriteContext,
        FileArtifactExtId fileArtifactExternalId,
        AiConversationExtId aiConversationExternalId,
        Guid correlationId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var deletedFileArtifactId = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        DELETE FROM fa_file_artifacts
                        WHERE fa_external_id = $externalId
                        RETURNING fa_id
                    ",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$externalId", fileArtifactExternalId.Value)
                .Execute();

            if (deletedFileArtifactId.IsEmpty)
            {
                transaction.Rollback();

                Log.Warning("FileArtifact '{FileArtifactExternalId}' could not be deleted because it was not found.",
                    fileArtifactExternalId);

                return;
            }

            var jobId = queue.EnqueueOrThrow(
                correlationId: correlationId,
                jobType: DeleteAiConversationQueueJobType.Value,
                definition: new DeleteAiConversationQueueJobDefinition
                {
                    AiConversationExternalId = aiConversationExternalId
                },
                executeAfterDate: clock.UtcNow,
                debounceId: null,
                sagaId: null,
                dbWriteContext: dbWriteContext,
                transaction: transaction);

            transaction.Commit();

            Log.Information(
                "FileArtifact '{FileArtifactExternalId}' was deleted and AiConversation '{AiConversationExternalId}' delete was scheduled (QueueJobId: {QueueJobId}).",
                fileArtifactExternalId,
                aiConversationExternalId,
                jobId);
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e,
                "Something went wrong while deleting AiConversation '{AiConversationExternalId}' for FileArtifact '{FileArtifactExternalId}'",
                aiConversationExternalId,
                fileArtifactExternalId);

            throw;
        }
    }

    public enum ResultCode
    {
        Ok = 0,
        AiConversationNotFound
    }
}