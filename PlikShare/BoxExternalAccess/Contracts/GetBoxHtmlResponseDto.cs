namespace PlikShare.BoxExternalAccess.Contracts;

public record GetBoxHtmlResponseDto(
    string? HeaderHtml, 
    string? FooterHtml);