using PlikShare.Files.Id;
using PlikShare.Uploads.Id;

namespace PlikShare.Files.PreSignedLinks.Contracts;

public class MultiFileDirectUploadItemResponseDto
{
    public required FileExtId FileExternalId { get; init; }
    public required FileUploadExtId UploadExternalId { get; init; }
}