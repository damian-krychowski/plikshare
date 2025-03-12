using PlikShare.ArtificialIntelligence.Cache;
using PlikShare.ArtificialIntelligence.Id;
using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.Queue;
using PlikShare.Core.SQLite;
using PlikShare.Core.Utils;
using Serilog;

namespace PlikShare.ArtificialIntelligence.DeleteConversation.QueueJob;

public class DeleteAiConversationQueueJobExecutor(
    AiDbWriteQueue aiDbWriteQueue,
    AiConversationCache aiConversationCache): IQueueNormalJobExecutor
{
    public string JobType => DeleteAiConversationQueueJobType.Value;
    public int Priority => QueueJobPriority.Low;

    public async Task<QueueJobResult> Execute(
        string definitionJson, 
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var definition = Json.Deserialize<DeleteAiConversationQueueJobDefinition>(
            definitionJson);

        if (definition is null)
        {
            throw new ArgumentException(
                $"Job '{definitionJson}' cannot be parsed to correct '{nameof(DeleteAiConversationQueueJobDefinition)}'");
        }

        await aiDbWriteQueue.Execute(
            operationToEnqueue: context => DeleteAiConversation(
                dbWriteContext: context,
                externalId: definition.AiConversationExternalId),
            cancellationToken: cancellationToken);

        await aiConversationCache.InvalidateEntry(
            externalId: definition.AiConversationExternalId,
            cancellationToken: cancellationToken);

        return QueueJobResult.Success;
    }

    private void DeleteAiConversation(
        AiDbWriteQueue.Context dbWriteContext,
        AiConversationExtId externalId)
    {
        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var deletedMessages = dbWriteContext
                .Cmd(
                    sql: @"
                    DELETE FROM aim_ai_messages
                    WHERE aim_ai_conversation_id = (
                        SELECT aic_id
                        FROM aic_ai_conversations
                        WHERE aic_external_id = $aicExternalId
                    )
                    RETURNING aim_id
                ",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$aicExternalId", externalId.Value)
                .Execute();

            var deletedConversationId = dbWriteContext
                .OneRowCmd(
                    sql: @"
                        DELETE FROM aic_ai_conversations
                        WHERE aic_external_id = $aicExternalId
                        RETURNING aic_id
                    ",
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$aicExternalId", externalId.Value)
                .Execute();

            transaction.Commit();

            if (deletedConversationId.IsEmpty)
            {
                Log.Warning("AiConversation '{AiConversationExternalId}' could not be deleted because it was not found.",
                    externalId);
            }
            else
            {
                var deletedMessagesIds = IdsRange.GroupConsecutiveIds(
                    deletedMessages);

                Log.Information("AiConversation '{AiConversationExternalId} ({AiConversationId})' was deleted with following AiMessages ({AiMessagesCount}): [{AiMessageIds}].",
                    externalId,
                    deletedConversationId,
                    deletedMessages.Count,
                    deletedMessagesIds);
            }
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while deleting AiConversation '{AiConversationExternalId}'",
                externalId);

            throw;
        }
    }
}
