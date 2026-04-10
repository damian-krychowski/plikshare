using PlikShare.Files.Id;

namespace PlikShare.AuditLog.Details;

public static partial class Audit
{
    public class FileRef
    {
        public required FileExtId ExternalId { get; init; }
        public required string Name { get; init; }
        public required long SizeInBytes { get; init; }
        public string? FolderPath { get; init; }
    }
}
