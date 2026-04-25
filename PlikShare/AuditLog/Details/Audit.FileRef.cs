using PlikShare.Core.Encryption;
using PlikShare.Files.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class FileRef
    {
        public required FileExtId ExternalId { get; init; }
        public required EncodedMetadataValue Name { get; init; }
        public required EncodedMetadataValue Extension { get; init; }
        public required long SizeInBytes { get; init; }
        public List<EncodedMetadataValue>? FolderPath { get; init; }
    }
}
