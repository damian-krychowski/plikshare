using PlikShare.ArtificialIntelligence.AiIncludes;
using PlikShare.ArtificialIntelligence.Id;
using PlikShare.Files.Id;
using PlikShare.Integrations.Id;

namespace PlikShare.ArtificialIntelligence.SendFileMessage.Contracts;

public class SendAiFileMessageRequestDto
{
    public required FileArtifactExtId FileArtifactExternalId { get; init; }
    public required AiConversationExtId ConversationExternalId { get; init; }
    public required AiMessageExtId MessageExternalId { get; init; }
    public required int ConversationCounter { get; init; }
    public required string Message { get; init; }
    public required List<AiInclude> Includes { get;init; }
    public required IntegrationExtId AiIntegrationExternalId { get; init; }
    public required string AiModel { get; init; }
}