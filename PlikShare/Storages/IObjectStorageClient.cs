using PlikShare.Core.Utils;
using ProtoBuf;

namespace PlikShare.Storages;

public interface IObjectStorageClient : IStorageClient
{
    ValueTask<InitiatedUpload> InitiateMultiPartUpload(
        string bucketName,
        FileKey key,
        CancellationToken cancellationToken);

    ValueTask<PreSignedUploadFullFileLink> GetPreSignedUploadFullFileLink(
        string bucketName,
        FileKey key,
        string contentType,
        DateTimeOffset expiresAt);

    ValueTask<PreSignedUploadFilePartLink> GetPreSignedUploadFilePartLink(
        string bucketName,
        FileKey key,
        string uploadId,
        int partNumber,
        string contentType,
        DateTimeOffset expiresAt);

    ValueTask<string> GetPreSignedDownloadFileLink(
        string bucketName,
        FileKey key,
        string contentType,
        ContentDispositionType contentDisposition,
        string fileName,
        DateTimeOffset expiresAt);
}

public record PreSignedUploadFullFileLink(
    string Url,
    List<RequiredHeader> RequiredHeaders);

/// <summary>
/// Pre-signed URL for uploading a single multipart part directly to the storage backend,
/// plus the name of the response header that carries the verification token the backend
/// needs to commit the multipart upload (e.g. <c>"ETag"</c> for S3-compatible APIs).
/// <para>
/// <see cref="ETagSourceHeader"/> is <c>null</c> when the backend doesn't need a token
/// from the client at all — for instance Azure Block Blob, where block IDs are
/// deterministic from the part number and <c>Put Block</c> doesn't return an ETag.
/// In that case the client still calls the complete-part endpoint to mark the part
/// as uploaded, but with a null eTag.
/// </para>
/// </summary>
public record PreSignedUploadFilePartLink(
    string Url,
    string? ETagSourceHeader);

[ProtoContract]
public class RequiredHeader
{
    [ProtoMember(1)]
    public required string Name { get; init; }

    [ProtoMember(2)]
    public required string Value { get; init; }
}

public readonly record struct InitiatedUpload(
    string MultipartUploadId);
