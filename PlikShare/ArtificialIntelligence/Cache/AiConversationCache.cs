using System.ComponentModel;
using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.ArtificialIntelligence.Id;
using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;

namespace PlikShare.ArtificialIntelligence.Cache;

public class AiConversationCache(
    IMasterDataEncryption masterDataEncryption,
    PlikShareAiDb plikShareAiDb,
    HybridCache cache)
{
    private static string ExternalIdKey(AiConversationExtId externalId) => $"ai-conversation:external-id:{externalId}";

    public async ValueTask<AiConversationContext?> TryGetAiConversation(
        AiConversationExtId externalId,
        CancellationToken cancellationToken)
    {
        var conversationCached = await cache.GetOrCreateAsync(
            key: ExternalIdKey(externalId),
            factory: _ => ValueTask.FromResult(Load(externalId)),
            cancellationToken: cancellationToken);

        if (conversationCached is null)
            return null;

        return new AiConversationContext
        {
            Id = conversationCached.Id,
            ExternalId = conversationCached.ExternalId,
            DerivedEncryption = masterDataEncryption.DeserializeDerived(
                conversationCached.DerivedEncryptionSerialized)
        };
    }

    public ValueTask InvalidateEntry(
        AiConversationExtId externalId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            ExternalIdKey(externalId),
            cancellationToken);
    }

    private AiConversationCached? Load(
        AiConversationExtId externalId)
    {
        using var connection = plikShareAiDb.OpenConnection();

        var (isEmpty, aiConversation) = connection
            .OneRowCmd(
                sql: """
                    SELECT
                     	aim_includes_encrypted,
                     	aic_id
                    FROM aim_ai_messages
                    INNER JOIN aic_ai_conversations
                        ON aic_id = aim_ai_conversation_id
                    WHERE aic_external_id = $aicExternalId
                    LIMIT 1
                    """,
                readRowFunc: reader =>
                {
                    var includesEncryptedBytes = reader.GetFieldValue<byte[]>(0);
                    var aicId = reader.GetInt32(1);

                    var derivedEncryption = masterDataEncryption.DerivedFrom(
                        includesEncryptedBytes);

                    return new AiConversationCached
                    {
                        Id = aicId,
                        ExternalId = externalId,
                        DerivedEncryptionSerialized = derivedEncryption.Serialize()
                    };
                })
            .WithParameter("$aicExternalId", externalId.Value)
            .Execute();

        return isEmpty ? null : aiConversation;
    }

    [ImmutableObject(true)]
    public sealed class AiConversationCached
    {
        public required int Id { get; init; }
        public required AiConversationExtId ExternalId { get; init; }
        public required byte[] DerivedEncryptionSerialized { get; init; }
    }
}

public class AiConversationContext
{
    public required int Id { get; init; }
    public required AiConversationExtId ExternalId { get; init; }
    public required IDerivedMasterDataEncryption DerivedEncryption { get; init; }
}