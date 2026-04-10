using PlikShare.Boxes.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class BoxRef
    {
        public required BoxExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public FolderRef? Folder { get; init; }
    }
}
