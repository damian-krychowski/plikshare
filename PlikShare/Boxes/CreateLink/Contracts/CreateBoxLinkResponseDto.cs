using PlikShare.BoxLinks.Id;

namespace PlikShare.Boxes.CreateLink.Contracts;

public record CreateBoxLinkResponseDto(
    BoxLinkExtId ExternalId, 
    string AccessCode);