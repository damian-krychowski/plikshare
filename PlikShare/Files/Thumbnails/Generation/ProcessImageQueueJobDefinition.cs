using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Users.Id;

namespace PlikShare.Files.Thumbnails.Generation;

public class ProcessImageQueueJobDefinition
{
    public required int WorkspaceId { get; init; }
    public required FileExtId ParentFileExternalId { get; init; }
    public required List<ThumbnailVariant> Variants { get; init; }

    /// <summary>
    /// Handle into <see cref="PlikShare.Core.Encryption.TemporaryWorkspaceEncryptionKeyStore"/>
    /// for full-encrypted workspaces. Null when the parent's workspace uses None or Managed
    /// encryption — the worker decrypts those without a user session.
    /// </summary>
    public required Guid? TempEncryptionKeyId { get; init; }

    /// <summary>
    /// User that clicked "Generate" — recorded as the thumbnail's <c>fi_uploader_identity</c>
    /// so attribution survives queue processing.
    /// </summary>
    public required UserExtId TriggeredByUserExternalId { get; init; }
}
