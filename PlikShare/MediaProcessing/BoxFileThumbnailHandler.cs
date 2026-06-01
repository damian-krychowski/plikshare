using Microsoft.AspNetCore.Http;
using PlikShare.BoxExternalAccess.Authorization;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.Records;
using PlikShare.Storages;
using PlikShare.Storages.Encryption.Authorization;
using PlikShare.Storages.Exceptions;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing;

/// <summary>
/// Streams a parent file's Mini thumbnail (decrypted) for a caller authenticated through
/// <see cref="BoxAccess"/> — either a box team-member (cookie) or an anonymous external-link
/// session. Mirrors the workspace-side <c>GetFileThumbnail</c> in <c>MediaProcessingEndpoints</c>,
/// but instead of trusting the workspace cookie it scopes the lookup to the box's folder subtree
/// via <see cref="GetThumbnailDownloadDetailsQuery"/>'s <c>boxFolderId</c> guard.
///
/// Full-encryption workspaces aren't reachable through box flows (see other box handlers'
/// <c>workspaceEncryptionSession: null</c>), so this path never threads a session.
/// </summary>
public class BoxFileThumbnailHandler(
    GetThumbnailDownloadDetailsQuery getThumbnailDownloadDetailsQuery)
{
    public async Task<IResult> Handle(
        FileExtId fileExternalId,
        BoxAccess boxAccess,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (boxAccess.IsOff || boxAccess.Box.Folder is null)
            return HttpErrors.File.NotFound(fileExternalId);

        var workspace = boxAccess.Box.Workspace;

        var thumbnail = getThumbnailDownloadDetailsQuery.Execute(
            workspace: workspace,
            parentFileExternalId: fileExternalId,
            variant: ThumbnailVariant.Mini,
            workspaceEncryptionSession: null,
            boxFolderId: boxAccess.Box.Folder.Id);

        if (thumbnail is null)
            return HttpErrors.File.NotFound(fileExternalId);

        var file = thumbnail.File;
        var response = httpContext.Response;

        var etag = $"\"{thumbnail.Etag}\"";
        response.Headers.CacheControl = "private, max-age=300";
        response.Headers.ETag = etag;

        if (string.Equals(
                httpContext.Request.Headers.IfNoneMatch.ToString(),
                etag,
                StringComparison.Ordinal))
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            return Results.Empty;
        }

        try
        {
            var encryptionMode = file.EncryptionMetadata.ToEncryptionMode(
                workspaceEncryptionSession: null,
                storageClient: workspace.Storage);

            await using var storageFile = await workspace.DownloadFile(
                fileDetails: new DownloadFileDetails(
                    FileKey: file.FileKey,
                    FileSizeInBytes: file.SizeInBytes,
                    EncryptionMode: encryptionMode),
                cancellationToken: cancellationToken);

            response.Headers.ContentType = file.ContentType;
            response.Headers.ContentLength = file.SizeInBytes;

            await storageFile.ReadTo(
                output: response.BodyWriter,
                cancellationToken: cancellationToken);

            return Results.Empty;
        }
        catch (OperationCanceledException)
        {
            return Results.Empty;
        }
        catch (FileNotFoundInStorageException)
        {
            if (response.HasStarted)
            {
                httpContext.Abort();
                return Results.Empty;
            }

            return HttpErrors.File.NotFound(fileExternalId);
        }
        finally
        {
            if (response.HasStarted)
                await response.BodyWriter.CompleteAsync();
        }
    }
}
