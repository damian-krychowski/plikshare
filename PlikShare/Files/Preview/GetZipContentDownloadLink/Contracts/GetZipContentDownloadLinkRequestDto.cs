using PlikShare.Core.Utils;
using PlikShare.Storages.Zip;

namespace PlikShare.Files.Preview.GetZipContentDownloadLink.Contracts;

public record GetZipContentDownloadLinkRequestDto(
    ZipFileDto Item,
    ContentDispositionType ContentDisposition);

public record GetZipContentDownloadLinkResponseDto(
    string DownloadPreSignedUrl);