namespace PlikShare.BoxExternalAccess.Contracts;

public record GetBoxFileDownloadLinkResponseDto(
    string DownloadPreSignedUrl);