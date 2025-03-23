using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PlikShare.Core.Authorization;
using PlikShare.Core.Protobuf;
using PlikShare.Core.UserIdentity;
using PlikShare.Core.Utils;
using PlikShare.Folders.Create;
using PlikShare.Folders.Create.Contracts;
using PlikShare.Folders.Id;
using PlikShare.Folders.List;
using PlikShare.Folders.List.Contracts;
using PlikShare.Folders.MoveToFolder;
using PlikShare.Folders.MoveToFolder.Contracts;
using PlikShare.Folders.Rename;
using PlikShare.Folders.Rename.Contracts;
using PlikShare.Workspaces.Validation;

namespace PlikShare.Folders;

public static class FoldersEndpoints
{
    public static void MapFoldersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workspaces/{workspaceExternalId}/folders")
            .WithTags("Folders")
            .RequireAuthorization(policyNames: AuthPolicy.Internal)
            .AddEndpointFilter<ValidateWorkspaceFilter>();

        group.MapPost("/", CreateFolder)
            .WithName("CreateFolder");

        group.MapPost("/bulk", BulkCreateFolder)
            .WithName("BulkCreateFolder")
            .WithProtobufResponse();

        group.MapGet("/", GetTopFolders)
            .WithName("GetTopFolders")
            .WithProtobufResponse();

        group.MapGet("/{folderExternalId}", GetFolder)
            .WithName("GetFolder")
            .WithProtobufResponse();

        group.MapPatch("/{folderExternalId}/name", UpdateFolderName)
            .WithName("UpdateFolderName");

        group.MapPatch("/move-items", MoveItemsToFolder)
            .WithName("MoveItemsToFolder");
    }

    private static async Task<Results<Ok<BulkCreateFolderResponseDto>, BadRequest<HttpError>, NotFound<HttpError>>> BulkCreateFolder(
        HttpContext httpContext,
        GetOrCreateFolderQuery getOrCreateFolderQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var request = httpContext.GetProtobufRequest<BulkCreateFolderRequestDto>();

        FolderExtId? parentFolderExternalId = request.ParentExternalId is null
            ? null
            : FolderExtId.Parse(request.ParentExternalId);

        var result = await getOrCreateFolderQuery.Execute(
            workspace: workspaceMembership.Workspace,
            parentFolderExternalId: parentFolderExternalId,
            boxFolderId: null,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            folderTreeItems: request.FolderTrees ?? [],
            ensureUniqueNames: request.EnsureUniqueNames,
            cancellationToken: cancellationToken);

        return result.Code switch
        {
            GetOrCreateFolderQuery.ResultCode.Ok => 
                TypedResults.Ok(
                    result.Response),

            GetOrCreateFolderQuery.ResultCode.ParentFolderNotFound =>
                HttpErrors.Folder.NotFound(
                    parentFolderExternalId!.Value),

            GetOrCreateFolderQuery.ResultCode.DuplicatedNamesFound =>
                HttpErrors.Folder.DuplicatedNamesOnInput(
                    result.TemporaryIdsWithDuplications ?? []),

            GetOrCreateFolderQuery.ResultCode.DuplicatedTemporaryIds =>
                HttpErrors.Folder.DuplicatedTemporaryIds(
                    result.TemporaryIdsWithDuplications ?? []),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(GetOrCreateFolderQuery),
                resultValueStr: result.Code.ToString())
        };
    }

    private static async Task<Results<Ok<CreateFolderResponseDto>, NotFound<HttpError>>> CreateFolder(
        [FromBody] CreateFolderRequestDto request,
        HttpContext httpContext,
        CreateFolderQuery createFolderQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var result = await createFolderQuery.Execute(
            workspace: workspaceMembership.Workspace,
            folderExternalId: request.ExternalId,
            parentFolderExternalId: request.ParentExternalId,
            name: request.Name,
            boxFolderId: null,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            cancellationToken: cancellationToken);

        return result switch
        {
            CreateFolderQuery.ResultCode.Ok => 
                TypedResults.Ok(new CreateFolderResponseDto
                {
                    ExternalId = request.ExternalId
                }),

            CreateFolderQuery.ResultCode.ParentFolderNotFound =>
                HttpErrors.Folder.NotFound(
                    request.ParentExternalId!.Value),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(CreateFolderQuery),
                resultValueStr: result.ToString())
        };
    }

    private static GetTopFolderContentResponseDto GetTopFolders(
        HttpContext httpContext,
        GetTopFolderContentQuery getTopFolderContentQuery)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var response = getTopFolderContentQuery.Execute(
            workspace: workspaceMembership.Workspace,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId));

        return response;
    }

    private static Results<Ok<GetFolderContentResponseDto>, NotFound<HttpError>> GetFolder(
        [FromRoute] FolderExtId folderExternalId,
        HttpContext httpContext,
        GetFolderContentQuery getFolderContentQuery)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var response = getFolderContentQuery.Execute(
            workspace: workspaceMembership.Workspace,
            folderExternalId: folderExternalId,
            boxFolderId: null,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            executionFlags: new GetFolderContentQuery.ExecutionFlags(
                GetCurrentFolder: true,
                GetSubfolders: true,
                GetFiles: GetFolderContentQuery.FilesExecutionFlag.All,
                GetUploads: true));

        return response switch
        {
            null => HttpErrors.Folder.NotFound(folderExternalId),
            _ => TypedResults.Ok(response)
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>>> UpdateFolderName(
        [FromRoute] FolderExtId folderExternalId,
        [FromBody] UpdateFolderNameRequestDto request,
        HttpContext httpContext,
        UpdateFolderNameQuery updateFolderNameQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var resultCode = await updateFolderNameQuery.Execute(
            workspace: workspaceMembership.Workspace,
            folderExternalId: folderExternalId,
            name: request.Name,
            boxFolderId: null,
            userIdentity: new UserIdentity(workspaceMembership.User.ExternalId),
            isOperationAllowedByBoxPermissions: true,
            cancellationToken: cancellationToken);

        return resultCode switch
        {
            UpdateFolderNameQuery.ResultCode.Ok =>
                TypedResults.Ok(),

            UpdateFolderNameQuery.ResultCode.FolderNotFound =>
                HttpErrors.Folder.NotFound(
                    folderExternalId),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(UpdateFolderNameQuery),
                resultValueStr: resultCode.ToString())
        };
    }

    private static async Task<Results<Ok, NotFound<HttpError>, BadRequest<HttpError>>> MoveItemsToFolder(
        [FromBody] MoveItemsToFolderRequestDto request,
        HttpContext httpContext,
        MoveItemsToFolderQuery moveItemsToFolderQuery,
        CancellationToken cancellationToken)
    {
        var workspaceMembership = httpContext.GetWorkspaceMembershipDetails();

        var resultCode = await moveItemsToFolderQuery.Execute(
            workspace: workspaceMembership.Workspace,
            folderExternalIds: request.FolderExternalIds,
            fileExternalIds: request.FileExternalIds,
            fileUploadExternalIds: request.FileUploadExternalIds,
            destinationFolderExternalId: request.DestinationFolderExternalId,
            boxFolderId: null,
            cancellationToken: cancellationToken);

        return resultCode switch
        {
            MoveItemsToFolderQuery.ResultCode.Ok => 
                TypedResults.Ok(),

            MoveItemsToFolderQuery.ResultCode.DestinationFolderNotFound =>
                HttpErrors.Folder.NotFound(
                    request.DestinationFolderExternalId!.Value),

            MoveItemsToFolderQuery.ResultCode.FoldersNotFound =>
                HttpErrors.Folder.SomeFolderNotFound(),

            MoveItemsToFolderQuery.ResultCode.FilesNotFound =>
                HttpErrors.Folder.SomeFileNotFound(),

            MoveItemsToFolderQuery.ResultCode.UploadsNotFound =>
                HttpErrors.Folder.SomeFileUploadNotFound(),

            MoveItemsToFolderQuery.ResultCode.FoldersMovedToOwnSubfolder =>
                HttpErrors.Folder.CannotMoveFoldersToOwnSubfolders(),

            _ => throw new UnexpectedOperationResultException(
                operationName: nameof(MoveItemsToFolderQuery),
                resultValueStr: resultCode.ToString())
        };
    }
}