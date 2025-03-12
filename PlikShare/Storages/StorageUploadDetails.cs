using PlikShare.Uploads.Algorithm;

namespace PlikShare.Storages;

public class StorageUploadDetails
{
    public required UploadAlgorithm Algorithm { get; init; }
    public required int FilePartsCount { get; init; }
    public required string S3UploadId { get; init; }
    public required string? PreSignedUploadLink { get; init; }
    public required bool WasMultiPartUploadInitiated { get; init; }
    public required FileEncryption FileEncryption { get; init; }
}