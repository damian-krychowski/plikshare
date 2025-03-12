using Microsoft.AspNetCore.Http.HttpResults;
using PlikShare.BoxExternalAccess.Authorization;
using PlikShare.BoxExternalAccess.Contracts;
using PlikShare.Core.Utils;
using PlikShare.Folders.Id;
using PlikShare.Folders.List;
using PlikShare.Folders.List.Contracts;

namespace PlikShare.BoxExternalAccess.Handler.GetContent;

public class GetBoxContentHandler(
    GetFolderContentQuery getFolderContentQuery)
{
    public Results<Ok<GetBoxDetailsAndContentResponseDto>, NotFound<HttpError>> GetDetailsAndContent(
        HttpContext httpContext,
        BoxAccess boxAccess,
        FolderExtId? folderExternalId)
    {
        if (boxAccess.IsOff && folderExternalId is not null)
            return HttpErrors.Folder.NotFound(
                folderExternalId.Value);

        if (boxAccess.IsOff && folderExternalId is null)
        {
            return TypedResults.Ok(new GetBoxDetailsAndContentResponseDto
            {
                Details = MapBoxDetails(boxAccess),
                Folder = null,
                Subfolders = [],
                Files = [],
                Uploads = []
            });
        }

        if (!boxAccess.IsOff && (boxAccess.Permissions.AllowList || folderExternalId is null))
        {
            var content = GetContent(
                boxAccess,
                folderExternalId);

            if(content is not null)
                return TypedResults.Ok(new GetBoxDetailsAndContentResponseDto
                {
                    Details = MapBoxDetails(boxAccess),
                    Folder = content.Folder,
                    Subfolders = content.Subfolders,
                    Files = content.Files,
                    Uploads = content.Uploads
                });
        }

        return HttpErrors.Folder.NotFound(
            folderExternalId);
    }

    private static BoxDetailsDto MapBoxDetails(BoxAccess boxAccess)
    {
        return new BoxDetailsDto
        {
            Name = boxAccess.IsAccessedThroughLink
                ? null
                : boxAccess.Box.Name,
            WorkspaceExternalId = boxAccess.IsBoxOwnedByUser()
                ? boxAccess.Box.Workspace.ExternalId.Value
                : null,
            AllowDownload = boxAccess.Permissions.AllowDownload,
            AllowUpload = boxAccess.Permissions.AllowUpload,
            AllowList = boxAccess.Permissions.AllowList,
            AllowDeleteFile = boxAccess.Permissions.AllowDeleteFile,
            AllowRenameFile = boxAccess.Permissions.AllowRenameFile,
            AllowMoveItems = boxAccess.Permissions.AllowMoveItems,
            AllowRenameFolder = boxAccess.Permissions.AllowRenameFolder,
            AllowDeleteFolder = boxAccess.Permissions.AllowDeleteFolder,
            AllowCreateFolder = boxAccess.Permissions.AllowCreateFolder,
            IsTurnedOn = !boxAccess.IsOff,
            OwnerEmail = boxAccess.IsAccessedThroughLink
                ? null
                : boxAccess.Box.Workspace.Owner.Email.Value,
        };
    }

    public Results<Ok<GetFolderContentResponseDto>, NotFound<HttpError>> GetContent(
        HttpContext httpContext,
        BoxAccess boxAccess,
        FolderExtId? folderExternalId)
    {
        if (!boxAccess.IsOff && (boxAccess.Permissions.AllowList || folderExternalId is null))
        {
            var folderContent = GetContent(
                boxAccess, 
                folderExternalId);

            if(folderContent is not null)
                return TypedResults.Ok(folderContent);
        }

        return HttpErrors.Folder.NotFound(
            folderExternalId);
    }

    private GetFolderContentResponseDto? GetContent(
        BoxAccess boxAccess, 
        FolderExtId? folderExternalId)
    {
        var executionFlags = boxAccess.Permissions.AllowList
            ? new GetFolderContentQuery.ExecutionFlags(
                GetCurrentFolder: true,
                GetSubfolders: true,
                GetFiles: GetFolderContentQuery.FilesExecutionFlag.All,
                GetUploads: true)
            : new GetFolderContentQuery.ExecutionFlags(
                GetCurrentFolder: true,
                GetSubfolders: false,
                GetFiles: GetFolderContentQuery.FilesExecutionFlag.UploadedByUserOnly,
                GetUploads: true);

        return getFolderContentQuery.Execute(
            workspace: boxAccess.Box.Workspace,
            folderExternalId: folderExternalId ?? boxAccess.Box.Folder!.ExternalId,
            boxFolderId: boxAccess.Box.Folder!.Id,
            userIdentity: boxAccess.UserIdentity,
            executionFlags: executionFlags);
    }
}