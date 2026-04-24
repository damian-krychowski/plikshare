using PlikShare.Files.Id;
using PlikShare.Uploads.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class FileUploadRef
    {
        public required FileUploadExtId ExternalId { get; init; }
        public required FileExtId FileExternalId { get; init; }
        public required string Name { get; init; }
        public required string Extension { get; init; }
        public required long SizeInBytes { get; init; }
        public List<string>? FolderPath { get; init; }
    }
}
