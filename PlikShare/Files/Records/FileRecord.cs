using PlikShare.Core.Encryption;
using PlikShare.Files.Id;
using PlikShare.Folders.Id;
using PlikShare.Storages;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Files.Records;

public class FileRecord
{
    public required FileExtId ExternalId { get; init; }
    public required string Name { get; init; }
    public required string ContentType { get; init; }
    public required string Extension { get; init; }
    public required string KeySecretPart { get; init; }
    public required long SizeInBytes { get; init; }
    public required int WorkspaceId { get; init; }
    public required FileRecordFolderAncestor[] FolderAncestors { get; init; }

    public required FileEncryptionMetadata? EncryptionMetadata { get; init; }
}

static class FileRecordExtensions
{
    extension(FileRecord file)
    {
        public string FullName => $"{file.Name}{file.Extension}";

        public List<string>? FolderPath => file.FolderAncestors.ToFolderPath();

        public FileKey FileKey => new()
        {
            KeySecretPart = file.KeySecretPart,
            FileExternalId = file.ExternalId,
        };
    }

    extension(FileRecordFolderAncestor[] ancestors)
    {
        public List<string>? ToFolderPath() => ancestors.Length == 0
            ? null
            : ancestors.Select(a => a.Name).ToList();
    }
}

public class FileRecordFolderAncestor
{
    public required FolderExtId ExternalId { get; init; }

    [EncryptedMetadata]
    public required string Name { get; init; }
}