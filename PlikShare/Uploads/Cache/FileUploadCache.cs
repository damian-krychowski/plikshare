using System.ComponentModel;
using Microsoft.Extensions.Caching.Hybrid;
using PlikShare.Core.Database.MainDatabase;
using PlikShare.Core.Encryption;
using PlikShare.Core.SQLite;
using PlikShare.Core.UserIdentity;
using PlikShare.Files.Id;
using PlikShare.Storages;
using PlikShare.Storages.Encryption;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Id;
using PlikShare.Workspaces.Cache;

namespace PlikShare.Uploads.Cache;

public class FileUploadCache(
    PlikShareDb plikShareDb,
    HybridCache cache,
    WorkspaceCache workspaceCache)
{
    private static string Key(FileUploadExtId externalId) => $"file-upload:external-id:{externalId}";

    public async ValueTask<FileUploadContext?> GetFileUpload(
        FileUploadExtId uploadExternalId,
        CancellationToken cancellationToken)
    {
        var cached = await cache.GetOrCreateAsync<FileUploadCached?>(
            key: Key(uploadExternalId),
            factory: _ =>
            {
                var upload = TryGetFileUpload(uploadExternalId);
                return ValueTask.FromResult(upload);
            },
            options: CacheOptions(),
            cancellationToken: cancellationToken);

        if (cached is null)
            return null;

        var workspace = await workspaceCache.TryGetWorkspace(
            workspaceId: cached.WorkspaceId,
            cancellationToken: cancellationToken);

        //means that workspace must have been deleted in the meantime
        if (workspace is null)
        {
            await Invalidate(cached.ExternalId, cancellationToken);
            return null;
        }

        var (uploadAlgorithm, partsCount) = workspace
            .Storage
            .ResolveUploadAlgorithm(
                fileSizeInBytes: cached.FileToUpload.SizeInBytes);

        return new FileUploadContext
        {
            Id = cached.Id,
            ExternalId = cached.ExternalId,

            FileToUpload = cached.FileToUpload,
            ContentType = cached.ContentType,
            OwnerIdentity = cached.OwnerIdentity,
            OwnerIdentityType = cached.OwnerIdentityType,

            PartsCount = partsCount,
            UploadAlgorithm = uploadAlgorithm,

            Workspace = workspace,
        };
    }

    public ValueTask PreInitialize(
        FileUploadCached toCache,
        CancellationToken cancellationToken)
    {
        return cache.SetAsync(
            key: Key(toCache.ExternalId),
            value: toCache,
            options: CacheOptions(),
            cancellationToken: cancellationToken);
    }

    public ValueTask Invalidate(
        FileUploadExtId uploadExternalId,
        CancellationToken cancellationToken)
    {
        return cache.RemoveAsync(
            key: Key(uploadExternalId),
            cancellationToken: cancellationToken);
    }

    private FileUploadCached? TryGetFileUpload(
        FileUploadExtId externalId)
    {
        using var connection = plikShareDb.OpenConnection();

        var result = connection
            .OneRowCmd(
                sql: @"
                    SELECT 
                        fu_id,
                        fu_file_external_id,
                        fu_file_content_type,
                        fu_file_size_in_bytes,
                        fu_file_s3_key_secret_part,
                        fu_s3_upload_id,
                        fu_encryption_key_version,
                        fu_encryption_salt,
                        fu_encryption_nonce_prefix,
                        fu_workspace_id,
                        fu_owner_identity,
                        fu_owner_identity_type                        
                    FROM fu_file_uploads
                    WHERE 
                        fu_external_id = $fileUploadExternalId
                ",
                readRowFunc: reader =>
                {
                    var encryptionKeyVersion = reader.GetByteOrNull(6);
                    
                    return new FileUploadCached
                    {
                        Id = reader.GetInt32(0),
                        ExternalId = externalId,
                        FileToUpload = new FileToUploadDetails
                        {
                            S3FileKey = new S3FileKey
                            {
                                FileExternalId = reader.GetExtId<FileExtId>(1),
                                S3KeySecretPart = reader.GetString(4),
                            },
                            Encryption = encryptionKeyVersion is null
                                ? new FileEncryption
                                {
                                    EncryptionType = StorageEncryptionType.None
                                }
                                : new FileEncryption
                                {
                                    EncryptionType = StorageEncryptionType.Managed,
                                    Metadata = new FileEncryptionMetadata
                                    {
                                        KeyVersion = encryptionKeyVersion.Value,
                                        Salt = reader.GetFieldValue<byte[]>(7),
                                        NoncePrefix = reader.GetFieldValue<byte[]>(8)
                                    }
                                },
                            SizeInBytes = reader.GetInt64(3),
                            S3UploadId = reader.GetString(5),
                        },
                        ContentType = reader.GetString(2),
                        WorkspaceId = reader.GetInt32(9),
                        OwnerIdentity = reader.GetString(10),
                        OwnerIdentityType = reader.GetString(11)
                    };
                })
            .WithParameter("$fileUploadExternalId", externalId.Value)
            .Execute();

        return result.IsEmpty ? null : result.Value;
    }

    private static HybridCacheEntryOptions CacheOptions()
    {
        return new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(5),
            LocalCacheExpiration = TimeSpan.FromMinutes(5),
        };
    }
    
    [ImmutableObject(true)]
    public sealed class FileUploadCached
    {
        public required int Id { get; init; }
        public required FileUploadExtId ExternalId { get; init; }
        public required FileToUploadDetails FileToUpload { get; init; }
        public required string ContentType { get; init; }
        public required int WorkspaceId { get; init; }
        public required string OwnerIdentity { get; init; }
        public required string OwnerIdentityType { get; init; }
    }
}

[ImmutableObject(true)]
public sealed class FileUploadContext
{
    public required int Id { get; init; }
    public required FileUploadExtId ExternalId { get; init; }
    public required FileToUploadDetails FileToUpload { get; init; }
    public required string ContentType { get; init; }
    public required string OwnerIdentity { get; init; }
    public required string OwnerIdentityType { get; init; }
    public required UploadAlgorithm UploadAlgorithm { get; init; }
    public required int PartsCount { get; init; }

    public required WorkspaceContext Workspace { get; init; }
}

[ImmutableObject(true)]
public sealed class FileToUploadDetails
{
    public required S3FileKey S3FileKey { get; init; }
    public required FileEncryption Encryption { get; init; }
    public required string S3UploadId { get; init; }
    public required long SizeInBytes { get; init; }
}

public static class FileUploadContextExtensions
{
    public static bool HasUserRight(
        this FileUploadContext fileUpload,
        int workspaceId,
        IUserIdentity userIdentity)
    {
        return fileUpload.Workspace.Id== workspaceId
               && userIdentity.IsEqual(
                   identity: fileUpload.OwnerIdentity,
                   identityType: fileUpload.OwnerIdentityType);
    }
}