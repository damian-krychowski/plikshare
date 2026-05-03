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
    
    ValueTask<string> GetPreSignedUploadFilePartLink(
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
