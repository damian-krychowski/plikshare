using PlikShare.Boxes.Id;
using PlikShare.BoxLinks.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class BoxAccessRef
    {
        public required BoxExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public BoxLinkAccessRef? BoxLink { get; init; }
    }

    public class BoxLinkAccessRef
    {
        public required BoxLinkExtId ExternalId { get; init; }
        public required string Name { get; init; }
    }
}
