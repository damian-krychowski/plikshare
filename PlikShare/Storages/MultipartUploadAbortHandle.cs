using System.Text.Json.Serialization;

namespace PlikShare.Storages;

/// <summary>
/// Backend-specific state needed to abort an in-flight multipart upload. Each
/// storage backend declares its own variant — S3 needs the upload id, HardDrive
/// needs the list of part tokens to delete from disk, Azure needs nothing extra
/// because <see cref="AzureBlob.AzureBlobStorageClient.AbortMultiPartUpload"/>
/// just calls <c>DeleteIfExists</c> against the blob name. Producers obtain the
/// right variant via <see cref="IStorageClient.BuildAbortHandle"/>; queue jobs
/// persist it as a polymorphic JSON payload and replay it back into the storage
/// client at execute time.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(S3MultipartUploadAbortHandle), "s3")]
[JsonDerivedType(typeof(HardDriveMultipartUploadAbortHandle), "hardDrive")]
[JsonDerivedType(typeof(AzureMultipartUploadAbortHandle), "azure")]
public abstract record MultipartUploadAbortHandle;

public sealed record S3MultipartUploadAbortHandle(string UploadId) : MultipartUploadAbortHandle;

public sealed record HardDriveMultipartUploadAbortHandle(List<string> PartTokens) : MultipartUploadAbortHandle;

public sealed record AzureMultipartUploadAbortHandle : MultipartUploadAbortHandle;
