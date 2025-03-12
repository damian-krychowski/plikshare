using Microsoft.Data.Sqlite;
using PlikShare.ArtificialIntelligence.AiIncludes;
using PlikShare.ArtificialIntelligence.Id;
using PlikShare.Core.Clock;
using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using Serilog;

namespace PlikShare.ArtificialIntelligence.SendFileMessage;

public class SaveAiChatCompletionQuery(
    AiDbWriteQueue aiDbWriteQueue,
    IClock clock)
{
    public Task<Result?> Execute(
        AiCompletion completion,
        string? newConversationName,
        GetFullAiConversationQuery.AiConversation conversation,
        GetFullAiConversationQuery.AiMessage queryMessage,
        CancellationToken cancellationToken)
    {
        return aiDbWriteQueue.Execute(
            operationToEnqueue: context => ExecuteSaveAiChatCompletion(
                dbWriteContext: context,
                completion: completion,
                newConversationName: newConversationName,
                conversation: conversation,
                queryMessage: queryMessage),
            cancellationToken: cancellationToken);
    }

    private Result? ExecuteSaveAiChatCompletion(
        AiDbWriteQueue.Context dbWriteContext,
        AiCompletion completion,
        string? newConversationName,
        GetFullAiConversationQuery.AiConversation conversation,
        GetFullAiConversationQuery.AiMessage queryMessage)
    {
        var aiMessageExternalId = AiMessageExtId.NewId();
        var conversationCounter = queryMessage.ConversationCounter + 1;

        using var transaction = dbWriteContext.Connection.BeginTransaction();

        try
        {
            var aiMessageId = dbWriteContext
                .OneRowCmd(
                    sql: """
                     INSERT INTO aim_ai_messages (
                        aim_external_id,
                        aim_ai_conversation_id,
                        aim_conversation_counter,
                        aim_message_encrypted,
                        aim_includes_encrypted,
                        aim_ai_model,
                        aim_user_identity_type,
                        aim_user_identity,
                        aim_created_at
                     ) 
                     VALUES (
                        $externalId,
                        $aicId,
                        $conversationCounter,
                        $message,
                        $includes,
                        $aiModel,
                        $userIdentityType,
                        $userIdentity,
                        $createdAt
                     )
                     RETURNING aim_id
                     """
            ,
            readRowFunc: reader => reader.GetInt32(0),
            transaction: transaction)
                .WithParameter("$externalId", aiMessageExternalId.Value)
                .WithParameter("$aicId", conversation.Id)
                .WithParameter("$conversationCounter", conversationCounter)
                .WithParameter("$message", conversation.DerivedEncryption.Encrypt(completion.Text))
                .WithParameter("$includes", conversation.DerivedEncryption.EncryptJson(new List<AiInclude>()))
                .WithParameter("$aiModel", queryMessage.AiModel)
                .WithParameter("$userIdentityType", IntegrationUserIdentity.Type)
                .WithParameter("$userIdentity", conversation.IntegrationExternalId.Value)
                .WithParameter("$createdAt", clock.UtcNow)
                .ExecuteOrThrow();

            dbWriteContext
                .OneRowCmd(
                    sql: """
                         UPDATE aic_ai_conversations
                         SET aic_is_waiting_for_ai_response = FALSE
                         WHERE aic_id = $aicId
                         RETURNING aic_id
                         """,
                    readRowFunc: reader => reader.GetInt32(0),
                    transaction: transaction)
                .WithParameter("$aicId", conversation.Id)
                .ExecuteOrThrow();

            if (newConversationName is not null)
            {
                dbWriteContext
                    .OneRowCmd(
                        sql: """
                             UPDATE aic_ai_conversations
                             SET aic_name = $name
                             WHERE aic_id = $aicId
                             RETURNING aic_id
                             """,
                        readRowFunc: reader => reader.GetInt32(0),
                        transaction: transaction)
                    .WithParameter("$aicId", conversation.Id)
                    .WithParameter("$name", newConversationName)
                    .ExecuteOrThrow();
            }

            transaction.Commit();

            return new Result
            {
                ExternalId = aiMessageExternalId,
                Id = aiMessageId,
                ConversationCounter = conversationCounter
            };
        }
        catch (SqliteException e)
        {
            transaction.Rollback();

            if (e.HasUniqueConstraintFailed(
                    tableName: "aim_ai_messages",
                    columnName: "aim_conversation_counter"))
            {
                Log.Warning(
                    "Could not save ChatCompletion '{ChatCompletionId}' with ConversationCounter {ConversationCounter} into the database " +
                    "for AiConversation '{AiConversationExternalId}' because there was already a higher counter saved in the meantime.",
                    completion.Id,
                    conversationCounter,
                    conversation.ExternalId);

                return null;
            }

            Log.Error(e, "Something went wrong while saving ChatCompletion '{ChatCompletionId}' into the database " +
                         "for AiConversation '{AiConversationExternalId}'",
                completion.Id,
                conversation.ExternalId);

            throw;
        }
        catch (Exception e)
        {
            transaction.Rollback();

            Log.Error(e, "Something went wrong while saving ChatCompletion '{ChatCompletionId}' into the database " +
                         "for AiConversation '{AiConversationExternalId}'",
                completion.Id,
                conversation.ExternalId);

            throw;
        }
    }
    
    public class Result
    {
        public required int Id { get; init; }
        public required AiMessageExtId ExternalId { get; init; }
        public required int ConversationCounter { get; init; }
    }
}