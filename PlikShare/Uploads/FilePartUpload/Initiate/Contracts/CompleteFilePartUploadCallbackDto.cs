namespace PlikShare.Uploads.FilePartUpload.Initiate.Contracts;

/// <summary>
/// Tells the client what to do after the direct upload of a single part finishes.
/// <para>
/// Presence of this object on the initiate-part response means the client must call
/// the complete-part endpoint to mark the part as uploaded. <see cref="ETagSourceHeader"/>
/// names the response header that carries the verification token the backend needs
/// to commit the multipart upload — <c>"ETag"</c> for S3-compatible APIs, or
/// <c>null</c> when the backend reconstructs the join key server-side (e.g. Azure
/// Block Blob: deterministic block IDs from part numbers, no client token needed).
/// </para>
/// <para>
/// Absence of this object (the field is null on the parent DTO) means no callback at
/// all — the upload routes through PlikShare's own pre-signed endpoint, which records
/// the part directly.
/// </para>
/// </summary>
public record CompleteFilePartUploadCallbackDto(
    string? ETagSourceHeader);
