using PlikShare.Files.Id;
using PlikShare.Storages;

namespace PlikShare.Files.Records;

public class FileRecord
{
    public required FileExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required string ContentType { get; init; }
    public required string Extension { get; init; }
    public required string S3KeySecretPart { get; init; }
    public required long SizeInBytes { get; init; }
    public required int WorkspaceId { get; init; }
    public required FileEncryption Encryption { get; init; }

    public string FullName => $"{Name}{Extension}";
}
