using PlikShare.Core.Encryption;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;

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
    public required FileEncryptionMetadata? EncryptionMetadata { get; init; }
    public required FileRecordFolderAncestor[] FolderAncestors { get; init; }
}

static class FileRecordExtensions
{
    extension(FileRecord file)
    {
        public string FullName => $"{file.Name}{file.Extension}";

        public string? FolderPath => file.FolderAncestors.ToFolderPath();
    }

    extension(FileRecordFolderAncestor[] ancestors)
    {
        public string? ToFolderPath() => ancestors.Length == 0
            ? null
            : string.Join("/", ancestors.Select(a => a.Name));
    }
}

public class FileRecordFolderAncestor
{
    public required FolderExtId ExternalId { get; init; }
    public required string Name { get; init; }
}
