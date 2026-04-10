using PlikShare.Folders.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class FolderRef
    {
        public required FolderExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public string? FolderPath { get; init; }
    }
}
