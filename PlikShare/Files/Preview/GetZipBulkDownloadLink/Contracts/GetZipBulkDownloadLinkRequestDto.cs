namespace PlikShare.Files.Preview.GetZipBulkDownloadLink.Contracts;

public record GetZipBulkDownloadLinkRequestDto(
    uint[] SelectedFolderIds,
    uint[] SelectedEntryIndices,
    uint[] ExcludedFolderIds,
    uint[] ExcludedEntryIndices);

public record GetZipBulkDownloadLinkResponseDto(
    string DownloadPreSignedUrl);
