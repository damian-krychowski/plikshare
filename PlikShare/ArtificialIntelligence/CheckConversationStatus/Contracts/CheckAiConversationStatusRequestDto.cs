using PlikShare.ArtificialIntelligence.Id;

namespace PlikShare.ArtificialIntelligence.CheckConversationStatus.Contracts;

public class CheckAiConversationStatusRequestDto
{
    public required List<AiConversationStateDto> Conversations { get; init; }
}

public class AiConversationStateDto
{
    public required AiConversationExtId ExternalId { get; init; }
    public required int ConversationCounter { get; init; }
}

public class CheckAiConversationStatusResponseDto
{
    public required List<AiConversationExtId> ConversationsWithNewMessages { get; init; }
}