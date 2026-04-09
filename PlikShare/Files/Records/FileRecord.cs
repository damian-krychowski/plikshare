using PlikShare.Files.Id;
using PlikShare.Folders.Id;
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
    public required FileRecordFolderAncestor[] FolderAncestors { get; init; }
}

static class FileRecordExtensions
{
    extension(FileRecord file)
    {
        public string FullName => $"{file.Name}{file.Extension}";

        public string? FolderPath => file.FolderAncestors.Length == 0
            ? null
            : string.Join("/", file.FolderAncestors.Select(a => a.Name));
    }
}

public class FileRecordFolderAncestor
{
    public required FolderExtId ExternalId { get; init; }
    public required string Name { get; init; }
}
