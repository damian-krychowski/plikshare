using PlikShare.ArtificialIntelligence.CheckConversationStatus.Contracts;
using PlikShare.ArtificialIntelligence.Id;
using PlikShare.Core.Database.AiDatabase;
using PlikShare.Core.SQLite;

namespace PlikShare.ArtificialIntelligence.CheckConversationStatus;

public class CheckAiConversationsStatusQuery(PlikShareAiDb plikShareAiDb)
{
    public CheckAiConversationStatusResponseDto Execute(
        CheckAiConversationStatusRequestDto request)
    {
        using var connection = plikShareAiDb.OpenConnection();

        var externalIds = request
            .Conversations
            .Select(x => x.ExternalId)
            .ToList();

        var currentConversations = connection
            .Cmd(
                sql: """
                     SELECT
                        aic_external_id,
                        MAX(aim_conversation_counter) AS max_counter
                     FROM aim_ai_messages
                     INNER JOIN aic_ai_conversations
                        ON aic_id = aim_ai_conversation_id
                     WHERE
                        aic_external_id IN (
                            SELECT value FROM json_each($externalIds)
                        )
                     GROUP BY
                        aic_external_id
                     """,
                readRowFunc: reader => new
                {
                    ExternalId = reader.GetExtId<AiConversationExtId>(0),
                    CurrentCounter = reader.GetInt32(1)
                })
            .WithJsonParameter("$externalIds", externalIds)
            .Execute()
            .ToDictionary(
                keySelector: x => x.ExternalId,
                elementSelector: x => x.CurrentCounter);

        var conversationsWithNewMessages = new List<AiConversationExtId>();

        for (var i = 0; i < request.Conversations.Count; i++)
        {
            var conversation = request.Conversations[i];

            if (!currentConversations.TryGetValue(conversation.ExternalId, out var currentCounter))
                continue;


            if (conversation.ConversationCounter < currentCounter)
            {
                conversationsWithNewMessages.Add(conversation.ExternalId);
            }
        }

        return new CheckAiConversationStatusResponseDto
        {
            ConversationsWithNewMessages = conversationsWithNewMessages
        };
    }
}