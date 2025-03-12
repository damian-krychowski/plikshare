namespace PlikShare.Files.Download.Contracts;

public record GetFileDownloadLinkResponseDto(
    string DownloadPreSignedUrl);