using PlikShare.Folders.Id;
using PlikShare.Uploads.Id;

namespace PlikShare.Uploads.List.Contracts;

public class GetUploadsListResponseDto
{
    public required List<Upload> Items { get; init; }
    
    public class Upload
    {
        public required FileUploadExtId ExternalId { get; init; }
        public required string FileName { get; init; }
        public required string FileExtension { get; init; }
        public required string FileContentType { get; init; }
        public required long FileSizeInBytes { get; init; }
        public required List<int> AlreadyUploadedPartNumbers { get; init; }
        public required FolderExtId? FolderExternalId { get; init; }
        public required string? FolderName { get; init; }
        public required List<string>? FolderPath { get; init; }
    }
}