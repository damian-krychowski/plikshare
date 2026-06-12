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
/// Streams a parent file's thumbnail (decrypted) for a caller authenticated through
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
        ThumbnailVariant variant,
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
            variant: variant,
            workspaceEncryptionSession: null,
            boxFolderId: boxAccess.Box.Folder.Id);

        if (thumbnail is null)
            return HttpErrors.File.NotFound(fileExternalId);

        var file = thumbnail.File;
        var response = httpContext.Response;

        var etag = $"\"{thumbnail.Etag}\"";

        // URLs carrying the ?v={etag} cache-buster are content-addressed — the bytes under such a
        // URL never change (regeneration changes the etag, hence the URL), so the browser may cache
        // them forever without revalidation. Unversioned URLs keep the short TTL + ETag fallback.
        response.Headers.CacheControl = httpContext.Request.Query.ContainsKey("v")
            ? "private, max-age=31536000, immutable"
            : "private, max-age=300";

        response.Headers.Vary = "Origin";

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
            var encryptionMode = workspace.GetFileEncryptionMode(
                fileEncryptionMetadata: file.EncryptionMetadata,
                workspaceEncryptionSession: null);

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
