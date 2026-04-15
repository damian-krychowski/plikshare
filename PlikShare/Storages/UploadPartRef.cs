namespace PlikShare.Storages;

/// <summary>
/// Provider-agnostic reference to an uploaded multipart chunk.
/// For S3/S3-compatible: PartToken = ETag
/// For Azure Blob: PartToken = BlockId (base64)
/// For HardDrive: PartToken = ETag (guid)
/// </summary>
public readonly record struct UploadPartRef(
    int PartNumber,
    string PartToken);
