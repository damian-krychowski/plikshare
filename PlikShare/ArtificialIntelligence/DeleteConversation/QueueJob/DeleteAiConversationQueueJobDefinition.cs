using PlikShare.ArtificialIntelligence.Id;

namespace PlikShare.ArtificialIntelligence.DeleteConversation.QueueJob;

public class DeleteAiConversationQueueJobDefinition
{
    public required AiConversationExtId AiConversationExternalId { get; init; }
}