using PlikShare.Files.Id;

namespace PlikShare.Uploads.CompleteFileUpload.Contracts;

public record CompleteFileUploadResponseDto(
    FileExtId FileExternalId);