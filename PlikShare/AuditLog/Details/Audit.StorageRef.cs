using PlikShare.Storages.Entities;
using PlikShare.Storages.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class StorageRef
    {
        public required StorageExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required StorageType Type { get; init; }
    }
}
