using PlikShare.Core.Encryption;
using PlikShare.Folders.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class FolderRef
    {
        public required FolderExtId ExternalId { get; init; }
        public required EncodedMetadataValue Name { get; init; }
        public List<EncodedMetadataValue>? FolderPath { get; init; }
    }
}
