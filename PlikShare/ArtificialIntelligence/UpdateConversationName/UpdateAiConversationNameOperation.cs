using PlikShare.ArtificialIntelligence.GetFileArtifact;
using PlikShare.ArtificialIntelligence.Id;
using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.SQLite;
using PlikShare.Files.Id;
using PlikShare.Workspaces.Cache;
using Serilog;

namespace PlikShare.ArtificialIntelligence.UpdateConversationName;

public class UpdateAiConversationNameOperation(
    GetFileArtifactWithAiConversationQuery getFileArtifactWithAiConversationQuery,
    AiDbWriteQueue aiDbWriteQueue)
{
    public async Task<ResultCode> Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        FileArtifactExtId fileArtifactExternalId,
        string name,
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

        return await RenameAiConversation(
            externalId: fileArtifact.AiConversationEntity.AiConversationExternalId,
            name: name,
            cancellationToken: cancellationToken);
    }

    private Task<ResultCode> RenameAiConversation(
        AiConversationExtId externalId,
        string name,
        CancellationToken cancellationToken)
    {
        return aiDbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteRenameAiConversation(
                dbWriteContext: context,
                externalId: externalId,
                name: name),
            cancellationToken: cancellationToken);
    }

    private ResultCode ExecuteRenameAiConversation(
        AiDbWriteQueue.Context dbWriteContext,
        AiConversationExtId externalId,
        string name)
    {
        var result = dbWriteContext
            .OneRowCmd(
                sql: """
                     UPDATE aic_ai_conversations
                     SET aic_name = $name
                     WHERE aic_external_id = $externalId
                     RETURNING aic_id
                     """,
                readRowFunc: reader => reader.GetInt32(0))
            .WithParameter("$name", name)
            .WithParameter("$externalId", externalId.Value)
            .Execute();

        if (result.IsEmpty)
        {
            Log.Warning("Could not rename AiConversation '{AiConversationExternalId}' because it was not found.",
                externalId);

            return ResultCode.AiConversationNotFound;
        }

        Log.Information("AiConversation '{AiConversationExternalId}' was renamed.",
            externalId);

        return ResultCode.Ok;
    }

    public enum ResultCode
    {
        Ok = 0,
        AiConversationNotFound
    }
}