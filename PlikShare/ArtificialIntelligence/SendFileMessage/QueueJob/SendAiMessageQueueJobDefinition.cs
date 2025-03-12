using PlikShare.ArtificialIntelligence.Id;

namespace PlikShare.ArtificialIntelligence.SendFileMessage.QueueJob;

public class SendAiMessageQueueJobDefinition
{
    public required AiMessageExtId AiMessageExternalId{ get; init; }
}