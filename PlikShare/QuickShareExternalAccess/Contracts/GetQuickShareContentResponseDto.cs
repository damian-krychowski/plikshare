using PlikShare.Files.Id;

namespace PlikShare.QuickShareExternalAccess.Contracts;

public record GetQuickShareContentResponseDto(
    List<QuickShareContentFileDto> Files,
    long TotalSizeInBytes);

public record QuickShareContentFileDto(
    FileExtId ExternalId,
    string FilePath,
    string Name,
    string Extension,
    long SizeInBytes);
