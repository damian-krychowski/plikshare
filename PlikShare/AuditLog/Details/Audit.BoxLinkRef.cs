using PlikShare.BoxLinks.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class BoxLinkRef
    {
        public required BoxLinkExtId ExternalId { get; init; }
        public required string Name { get; init; }
    }
}
