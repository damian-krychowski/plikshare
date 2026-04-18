using PlikShare.Core.Encryption;
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
    public required FileRecordFolderAncestor[] FolderAncestors { get; init; }

    public required FileEncryptionMetadata? EncryptionMetadata { get; init; }
}

public class ResolvedFileRecord
{
    public required S3FileKey S3FileKey { get; init; }
    public required string Name { get; init; }
    public required string ContentType { get; init; }
    public required string Extension { get; init; }
    public required long SizeInBytes { get; init; }
    public required int WorkspaceId { get; init; }
    public required FileRecordFolderAncestor[] FolderAncestors { get; init; }

    public required FileEncryptionMode EncryptionMode { get; init; }
}

static class FileRecordExtensions
{
    extension(FileRecord file)
    {
        public string FullName => $"{file.Name}{file.Extension}";

        public string? FolderPath => file.FolderAncestors.ToFolderPath();
        
        public S3FileKey S3FileKey => new()
        {
            S3KeySecretPart = file.S3KeySecretPart,
            FileExternalId = file.ExternalId,
        };

        public ResolvedFileRecord Resolve(
            WorkspaceEncryptionSession? workspaceEncryptionSession,
            IStorageClient storageClient) =>
            new()
            {
                S3FileKey = new S3FileKey
                {
                    S3KeySecretPart = file.S3KeySecretPart,
                    FileExternalId = file.ExternalId,
                },
                Name = file.Name,
                ContentType = file.ContentType,
                Extension = file.Extension,
                SizeInBytes = file.SizeInBytes,
                WorkspaceId = file.WorkspaceId,
                FolderAncestors = file.FolderAncestors,
                EncryptionMode = file.EncryptionMetadata.ToEncryptionMode(
                    workspaceEncryptionSession,
                    storageClient)
            };
    }

    extension(ResolvedFileRecord file)
    {
        public string FullName => $"{file.Name}{file.Extension}";

        public string? FolderPath => file.FolderAncestors.ToFolderPath();

        public FileExtId ExternalId => file.S3FileKey.FileExternalId;
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