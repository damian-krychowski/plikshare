using PlikShare.ArtificialIntelligence.AiIncludes;
using PlikShare.ArtificialIntelligence.Id;

namespace PlikShare.ArtificialIntelligence.GetMessages.Contracts;

public class GetAiMessagesResponseDto
{
    public AiConversationExtId ConversationExternalId { get; init; }
    public string? ConversationName { get; init; }

    public required List<AiMessageDto> Messages { get; init; }
}

public class AiMessageDto
{
    public required AiMessageExtId ExternalId { get; init; }
    public required int ConversationCounter { get; init; }
    public required string Message { get; init; }
    public required List<AiInclude> Includes { get; init; }
    public required string AiModel { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
    public required string CreatedBy { get; init; }
    public required AiMessageAuthorType AuthorType { get; init; }
}