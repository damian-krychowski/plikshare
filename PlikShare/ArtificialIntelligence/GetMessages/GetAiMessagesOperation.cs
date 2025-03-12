using PlikShare.ArtificialIntelligence.AiIncludes;
using PlikShare.ArtificialIntelligence.Cache;
using PlikShare.ArtificialIntelligence.GetFileArtifact;
using PlikShare.ArtificialIntelligence.GetMessages.Contracts;
using PlikShare.ArtificialIntelligence.Id;
using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Id;
using PlikShare.Users.UserIdentityResolver;
using PlikShare.Workspaces.Cache;

namespace PlikShare.ArtificialIntelligence.GetMessages;

public class GetAiMessagesOperation(
    AiConversationCache aiConversationCache,
    PlikShareAiDb plikShareAiDb,
    UserIdentityResolver userIdentityResolver,
    GetFileArtifactWithAiConversationQuery getFileArtifactWithAiConversationQuery)
{
    public async ValueTask<Result> Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        FileArtifactExtId fileArtifactExternalId,
        int fromConversationCounter,
        CancellationToken cancellationToken)
    {
        var fileArtifact = getFileArtifactWithAiConversationQuery.Execute(
            workspace,
            fileExternalId,
            fileArtifactExternalId);

        if (fileArtifact is null)
        {
            return new Result
            {
                Code = ResultCode.NotFound
            };
        }

        var conversationContext = await aiConversationCache.TryGetAiConversation(
            externalId: fileArtifact.AiConversationEntity.AiConversationExternalId,
            cancellationToken: cancellationToken);

        if (conversationContext is null)
        {
            return new Result
            {
                Code = ResultCode.NotFound
            };
        }

        using var connection = plikShareAiDb.OpenConnection();

        var conversationName = connection
            .OneRowCmd(
                sql: """
                     SELECT aic_name
                     FROM aic_ai_conversations
                     WHERE aic_id = $id
                     """,
                readRowFunc: reader => reader.GetStringOrNull(0))
            .WithParameter("$id", conversationContext.Id)
            .Execute();

        if (conversationName.IsEmpty)
            return new Result
            {
                Code = ResultCode.NotFound
            };

        var messages = connection
            .Cmd(
                sql: """
                     SELECT 
                         aim_external_id,
                         aim_conversation_counter,
                         aim_message_encrypted,
                         aim_includes_encrypted,
                         aim_ai_model,
                         aim_user_identity_type,
                         aim_user_identity,
                         aim_created_at
                     FROM aim_ai_messages
                     WHERE aim_ai_conversation_id = $aicId
                         AND aim_conversation_counter >= $fromConversationCounter
                     ORDER BY aim_conversation_counter
                     """,
                readRowFunc: reader => new
                {
                    ExternalId = reader.GetExtId<AiMessageExtId>(0),
                    ConversationCounter = reader.GetInt32(1),
                    Message = conversationContext.DerivedEncryption.Decrypt(
                        reader.GetFieldValue<byte[]>(2)),
                    Includes = conversationContext.DerivedEncryption.DecryptJson<List<AiInclude>>(
                        reader.GetFieldValue<byte[]>(3)),
                    AiModel = reader.GetString(4),
                    User = new GenericUserIdentity(
                        IdentityType: reader.GetString(5),
                        Identity: reader.GetString(6)),
                    CreatedAt = reader.GetDateTimeOffset(7)
                })
            .WithParameter("$aicId", conversationContext.Id)
            .WithParameter("$fromConversationCounter", fromConversationCounter)
            .Execute();

        var resolvedIdentities = userIdentityResolver.Resolve(
            messages.Select(m => m.User).ToList());

        return new Result
        {
            Code = ResultCode.Ok,
            Response = new GetAiMessagesResponseDto
            {
                ConversationExternalId = fileArtifact.AiConversationEntity.AiConversationExternalId,
                ConversationName = conversationName.Value,

                Messages = messages
                    .Select(m => new AiMessageDto
                    {
                        AiModel = m.AiModel,
                        ConversationCounter = m.ConversationCounter,
                        CreatedAt = m.CreatedAt,
                        CreatedBy = resolvedIdentities.GetOrThrow(m.User).DisplayText,
                        ExternalId = m.ExternalId,
                        Includes = m.Includes,
                        Message = m.Message,
                        AuthorType = m.User.IdentityType == IntegrationUserIdentity.Type
                            ? AiMessageAuthorType.Ai
                            : AiMessageAuthorType.Human
                    })
                    .ToList()
            }
        };
    }

    public enum ResultCode
    {
        Ok = 0,
        NotFound
    }

    public class Result
    {
        public required ResultCode Code { get; init; }
        public GetAiMessagesResponseDto? Response { get; init; }
    }
}