using PlikShare.ArtificialIntelligence.AiIncludes;
using PlikShare.ArtificialIntelligence.Cache;
using PlikShare.ArtificialIntelligence.Id;
using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Integrations.Id;

namespace PlikShare.ArtificialIntelligence.SendFileMessage;

public class GetFullAiConversationQuery(
    PlikShareAiDb plikShareAiDb,
    AiConversationCache aiConversationCache)
{
    public async ValueTask<Result> GetFullConversation(
        AiMessageExtId lastMessageExternalId,
        CancellationToken cancellationToken)
    {
        using var connection = plikShareAiDb.OpenConnection();

        var (isEmpty, conversation) = connection
            .OneRowCmd(
                sql: """
                    SELECT
                        aic_id,    
                        aic_external_id,
                        aic_integration_external_id,
                        aic_name
                    FROM aim_ai_messages
                    INNER JOIN aic_ai_conversations
                        ON aic_id = aim_ai_conversation_id
                    WHERE aim_external_id = $externalId
                    """,
                readRowFunc: reader => new
                {
                    Id = reader.GetInt32(0),
                    ExternalId = reader.GetExtId<AiConversationExtId>(1),
                    IntegrationExternalId = reader.GetExtId<IntegrationExtId>(2),
                    Name = reader.GetStringOrNull(3)
                })
            .WithParameter("$externalId", lastMessageExternalId.Value)
            .Execute();

        if (isEmpty)
            return new Result(Code: ResultCode.NotFound);

        var conversationContext = await aiConversationCache.TryGetAiConversation(
            externalId: conversation.ExternalId,
            cancellationToken: cancellationToken);

        if(conversationContext is null)
            return new Result(Code: ResultCode.NotFound);

        var allMessages = connection
            .Cmd(
                sql: """
                     SELECT
                         aim_external_id,
                         aim_message_encrypted,
                         aim_includes_encrypted,
                         aim_ai_model,
                         aim_conversation_counter,
                         aim_user_identity_type
                     FROM aim_ai_messages
                     WHERE aim_ai_conversation_id = $aicId
                     ORDER BY aim_conversation_counter
                     """,
                readRowFunc: reader => new AiMessage
                {
                    ExternalId = reader.GetExtId<AiMessageExtId>(0),
                    Message = conversationContext.DerivedEncryption.Decrypt(
                        reader.GetFieldValue<byte[]>(1)),
                    Includes = conversationContext.DerivedEncryption.DecryptJson<List<AiInclude>>(
                        reader.GetFieldValue<byte[]>(2)),
                    AiModel = reader.GetString(3),
                    ConversationCounter = reader.GetInt32(4),
                    SentByHuman = reader.GetString(5) != IntegrationUserIdentity.Type
                })
            .WithParameter("$aicId", conversation.Id)
            .Execute();

        if (allMessages.Count == 0)
            return new Result(Code: ResultCode.NotFound);

        var lastMessage = allMessages.Last();

        if (lastMessage.ExternalId != lastMessageExternalId)
            return new Result(Code: ResultCode.NewerMessagesFound);

        return new Result(
            Code: ResultCode.Ok,
            Conversation: new AiConversation
            {
                Id = conversation.Id,
                ExternalId = conversation.ExternalId,
                IntegrationExternalId = conversation.IntegrationExternalId,
                Name = conversation.Name,
                Messages = allMessages,
                DerivedEncryption = conversationContext.DerivedEncryption
            });
    }

    public readonly record struct Result(
        ResultCode Code,
        AiConversation? Conversation = null);

    public enum ResultCode
    {
        Ok,
        NotFound,
        NewerMessagesFound
    }

    public class AiConversation
    {
        public required int Id { get; init; }
        public required AiConversationExtId ExternalId { get; init; }
        public required IntegrationExtId IntegrationExternalId { get; init; }
        public required string? Name { get; init; }

        public required List<AiMessage> Messages { get; init; }
        public required IDerivedMasterDataEncryption DerivedEncryption { get; init; }
    }

    public class AiMessage
    {
        public required AiMessageExtId ExternalId { get; init; }
        public required string Message { get; init; }
        public required List<AiInclude> Includes { get; init; }
        public required string AiModel { get; init; }
        public required int ConversationCounter { get; init; }
        public bool SentByHuman { get; init; }
    }
}