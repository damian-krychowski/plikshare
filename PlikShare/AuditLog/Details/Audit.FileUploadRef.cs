using PlikShare.Core.Encryption;
using PlikShare.Files.Id;
using PlikShare.Uploads.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class FileUploadRef
    {
        public required FileUploadExtId ExternalId { get; init; }
        public required FileExtId FileExternalId { get; init; }
        public required EncodedMetadataValue Name { get; init; }
        public required EncodedMetadataValue Extension { get; init; }
        public required long SizeInBytes { get; init; }
        public List<EncodedMetadataValue>? FolderPath { get; init; }
    }
}
