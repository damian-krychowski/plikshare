using Amazon.S3.Model;
using PlikShare.Core.Encryption;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Storages.Encryption;
using PlikShare.Storages.Id;
using PlikShare.Uploads.Algorithm;
using PlikShare.Uploads.Id;

namespace PlikShare.Storages;

public interface IStorageClient
{
    int StorageId { get; }
    StorageExtId ExternalId { get; }
    public StorageEncryptionType EncryptionType { get; }
    public EncryptionKeyProvider? EncryptionKeyProvider { get; }
    
    ValueTask DeleteFile(
        string bucketName,
        S3FileKey key,
        CancellationToken cancellationToken = default);

    ValueTask DeleteFiles(
        string bucketName,
        S3FileKey[] keys,
        CancellationToken cancellationToken = default);

    Task CompleteMultiPartUpload(
        string bucketName,
        S3FileKey key,
        string uploadId,
        List<PartETag> partETags,
        CancellationToken cancellationToken = default);
    
    ValueTask<PreSignedUploadLinkResult> GetPreSignedUploadFilePartLink(
        string bucketName,
        FileUploadExtId fileUploadExternalId,
        S3FileKey key,
        string uploadId,
        int partNumber,
        string contentType,
        int? boxLinkId,
        IUserIdentity userIdentity,
        bool enforceInternalPassThrough,
        CancellationToken cancellationToken = default);

    ValueTask<string> GetPreSignedDownloadFileLink(
        string bucketName,
        S3FileKey key,
        string contentType,
        string fileName,
        ContentDispositionType contentDisposition,
        int? boxLinkId,
        IUserIdentity userIdentity,
        bool enforceInternalPassThrough,
        CancellationToken cancellationToken = default);

    Task AbortMultiPartUpload(
        string bucketName,
        S3FileKey key,
        string uploadId,
        List<string> partETags,
        CancellationToken cancellationToken = default);
    
    Task CreateBucketIfDoesntExist(
        string bucketName,
        CancellationToken cancellationToken = default);

    Task DeleteBucket(
        string bucketName,
        CancellationToken cancellationToken = default);
    
    (UploadAlgorithm Algorithm, int FilePartsCount) ResolveUploadAlgorithm(
        long fileSizeInBytes);
    (UploadAlgorithm Algorithm, int FilePartsCount) ResolveCopyUploadAlgorithm(
        long fileSizeInBytes);

    string GenerateFileS3KeySecretPart();
}

public class PreSignedUploadLinkResult
{
    public required string Url { get; init; }
    public required bool IsCompleteFilePartUploadCallbackRequired { get; init; }
}

public static class StorageClientExtensions
{
    extension(IStorageClient storageClient)
    {
        public ManagedEncryptionKeyProvider GetManagedEncryptionKeyProviderOrThrow()
        {
            if (storageClient.EncryptionType != StorageEncryptionType.Managed)
                throw new InvalidOperationException(
                    $"Cannot get managed encryption key provider for storage '{storageClient.ExternalId}' " +
                    $"because encryption type is '{storageClient.EncryptionType}', not '{StorageEncryptionType.Managed}'.");

            return storageClient
                .EncryptionKeyProvider
                ?.Managed ?? throw new InvalidOperationException(
                $"Managed encryption key provider is not configured " +
                $"for storage '{storageClient.ExternalId}' " +
                $"despite encryption type being set to '{StorageEncryptionType.Managed}'.");
        }

        public FullEncryptionKeyProvider GetFullEncryptionKeyProviderOrThrow()
        {
            if (storageClient.EncryptionType != StorageEncryptionType.Full)
                throw new InvalidOperationException(
                    $"Cannot get full encryption key provider for storage '{storageClient.ExternalId}' " +
                    $"because encryption type is '{storageClient.EncryptionType}', not '{StorageEncryptionType.Full}'.");

            return storageClient
                .EncryptionKeyProvider
                ?.Full ?? throw new InvalidOperationException(
                $"Full encryption key provider is not configured " +
                $"for storage '{storageClient.ExternalId}' " +
                $"despite encryption type being set to '{StorageEncryptionType.Full}'.");
        }

        public Aes256GcmStreaming.GetEncryptionKey GetEncryptionKeyFunc(
            FullEncryptionSession? fullEncryptionAccess)
        {
            if (storageClient.EncryptionType == StorageEncryptionType.None)
            {
                throw new InvalidOperationException(
                    $"Cannot get encryption key function for storage '{storageClient.ExternalId}' " +
                    $"because encryption type is '{StorageEncryptionType.None}'.");
            }

            if (storageClient.EncryptionType == StorageEncryptionType.Managed)
            {
                var keyProvider = storageClient.GetManagedEncryptionKeyProviderOrThrow();

                return version => keyProvider.GetEncryptionKey(
                    version);
            }

            if (storageClient.EncryptionType == StorageEncryptionType.Full)
            {
                if (fullEncryptionAccess is null)
                {
                    throw new ArgumentNullException(
                        nameof(fullEncryptionAccess),
                        $"Full encryption access is required for storage '{storageClient.ExternalId}' " +
                        $"with encryption type '{StorageEncryptionType.Full}'.");
                }

                var keyProvider = storageClient.GetFullEncryptionKeyProviderOrThrow();

                return version => keyProvider.GetEncryptionKey(
                    version,
                    fullEncryptionAccess.Kek);
            }

            throw new InvalidOperationException(
                $"Unsupported encryption type '{storageClient.EncryptionType}' " +
                $"for storage '{storageClient.ExternalId}'.");
        }
    }
}