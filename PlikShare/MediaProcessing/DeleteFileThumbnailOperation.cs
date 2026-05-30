using PlikShare.Core.Encryption;
using PlikShare.Files.Id;
using PlikShare.Files.Metadata;
using PlikShare.Files.PreSignedLinks.Validation;
using PlikShare.Workspaces.Cache;

namespace PlikShare.MediaProcessing;

public class DeleteFileThumbnailOperation(
    GetFilePreSignedDownloadLinkDetailsQuery getParentFileDetailsQuery,
    GetThumbnailsQuery getThumbnailsQuery,
    DeleteThumbnailsQuery deleteThumbnailsQuery)
{
    public async Task<Result> Execute(
        WorkspaceContext workspace,
        FileExtId parentFileExternalId,
        ThumbnailVariant variant,
        WorkspaceEncryptionSession? workspaceEncryptionSession,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var parentLookup = getParentFileDetailsQuery.Execute(
            fileExternalId: parentFileExternalId,
            workspaceEncryptionSession: workspaceEncryptionSession);

        if (parentLookup.Code == GetFilePreSignedDownloadLinkDetailsQuery.ResultCode.NotFound
            || parentLookup.Details?.WorkspaceId != workspace.Id)
        {
            return new Result(Code: ResultCode.ParentNotFound);
        }

        var existingThumbnails = getThumbnailsQuery.Execute(
            workspace: workspace,
            parentFileExternalId: parentFileExternalId,
            workspaceEncryptionSession: workspaceEncryptionSession);

        // All matching thumbnails of this variant get nuked — self-heals from any past races
        // that left duplicates around. Idempotent: returns Ok even if nothing to delete.
        var thumbnailFileIds = existingThumbnails
            .Where(t => t.Variant == variant)
            .Select(t => t.Id)
            .ToList();

        await deleteThumbnailsQuery.Execute(
            workspace: workspace,
            thumbnailFileIds: thumbnailFileIds,
            correlationId: correlationId,
            cancellationToken: cancellationToken);

        return new Result(Code: ResultCode.Ok);
    }

    public record Result(ResultCode Code);

    public enum ResultCode
    {
        Ok = 0,
        ParentNotFound
    }
}
