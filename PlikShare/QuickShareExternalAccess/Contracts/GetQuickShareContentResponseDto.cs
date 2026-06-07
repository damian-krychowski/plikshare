using PlikShare.Files.Id;
using PlikShare.Files.Metadata.Contracts;
using PlikShare.Folders.Id;

namespace PlikShare.QuickShareExternalAccess.Contracts;

public record GetQuickShareContentResponseDto(
    List<QuickShareContentFolderDto> Folders,
    List<QuickShareContentFileDto> Files,
    long TotalSizeInBytes);

public record QuickShareContentFolderDto(
    FolderExtId ExternalId,
    FolderExtId? ParentExternalId,
    string Name);

public record QuickShareContentFileDto(
    FileExtId ExternalId,
    FolderExtId? FolderExternalId,
    string Name,
    string Extension,
    long SizeInBytes,
    FileMetadataDto? Metadata);
