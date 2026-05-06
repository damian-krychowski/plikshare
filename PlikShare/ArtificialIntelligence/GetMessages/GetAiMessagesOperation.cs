using PlikShare.ArtificialIntelligence.AiIncludes;
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
    PlikShareAiDb plikShareAiDb,
    IMasterDataEncryption masterDataEncryption,
    UserIdentityResolver userIdentityResolver,
    GetFileArtifactWithAiConversationQuery getFileArtifactWithAiConversationQuery)
{
    public Result Execute(
        WorkspaceContext workspace,
        FileExtId fileExternalId,
        FileArtifactExtId fileArtifactExternalId,
        int fromConversationCounter)
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

        using var connection = plikShareAiDb.OpenConnection();

        var conversation = connection
            .OneRowCmd(
                sql: """
                     SELECT aic_id, aic_name
                     FROM aic_ai_conversations
                     WHERE aic_external_id = $externalId
                     """,
                readRowFunc: reader => new
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetStringOrNull(1)
                })
            .WithParameter("$externalId", fileArtifact.AiConversationEntity.AiConversationExternalId.Value)
            .Execute();

        if (conversation.IsEmpty)
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
                    Message = masterDataEncryption.DecryptString(
                        reader.GetFieldValue<byte[]>(2)),
                    Includes = masterDataEncryption.DecryptJson<List<AiInclude>>(
                        reader.GetFieldValue<byte[]>(3)),
                    AiModel = reader.GetString(4),
                    User = new GenericUserIdentity(
                        IdentityType: reader.GetString(5),
                        Identity: reader.GetString(6)),
                    CreatedAt = reader.GetDateTimeOffset(7)
                })
            .WithParameter("$aicId", conversation.Value.Id)
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
                ConversationName = conversation.Value.Name,

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
